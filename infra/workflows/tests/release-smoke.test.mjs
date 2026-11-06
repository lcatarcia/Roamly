import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

const workflow = readFileSync(new URL('../../../.github/workflows/release.yml', import.meta.url), 'utf8')
const smoke = workflow.split('      - name: Smoke test pubblico HTTPS post-deploy')[1]
  .split('        run: |')[1]
  .split(/\r?\n/).slice(1).map(line => line.replace(/^ {10}/, '')).join('\n')
const bash = process.env.ROAMLY_TEST_BASH ??
  (process.platform === 'win32' ? 'C:/Program Files/Git/bin/bash.exe' : 'bash')
const shellPath = path => path.replaceAll('\\', '/')
  .replace(/^([a-zA-Z]):/, (_, drive) => `/${drive.toLowerCase()}`)
const discovery = spawnSync(bash, ['-c', 'command -v jq'], { encoding: 'utf8' })
const realJq = shellPath(process.env.ROAMLY_TEST_JQ ?? discovery.stdout.trim())
assert.ok(realJq, 'Install jq or set ROAMLY_TEST_JQ; these tests execute the real predicate')
const image = `ghcr.io/example/roamly@sha256:${'a'.repeat(64)}`
const fixture = {
  value: [{
    name: 'ca-roamly-prod--fixture',
    properties: {
      active: true,
      template: { containers: [{ name: 'roamly-api', image }] },
    },
  }],
}

test('smoke metadata budget stays at six attempts, ten-second timeout and five-second delay', () => {
  assert.match(smoke, /metadata_max_attempts=6\n/)
  assert.match(smoke, /metadata_timeout_seconds=10\n/)
  assert.match(smoke, /metadata_retry_delay_seconds=5\n/)
  assert.match(smoke, /max_attempts=5\n/)
  assert.match(smoke, /retry_delay_seconds=10\n/)
  assert.match(smoke, /--max-time 10/)
})

function runSmoke({
  revisions = fixture, rawRevisions, azExit = 0, fqdnExit = 0, cliError = '',
  transitionUntilRevisionProbed = false, healthStatus = 200,
} = {}) {
  const directory = mkdtempSync(join(tmpdir(), 'roamly-release-smoke-'))
  const files = {
    fixture: join(directory, 'fixture.json'),
    calls: join(directory, 'calls'),
    summary: join(directory, 'summary'),
    script: join(directory, 'smoke.sh'),
    transition: join(directory, 'transition.json'),
    ready: join(directory, 'ready'),
  }
  try {
    writeFileSync(files.fixture, rawRevisions ?? JSON.stringify(revisions))
    writeFileSync(files.transition, JSON.stringify({
      value: [fixture.value[0], { properties: {
        active: true, template: { containers: [{ image: image.slice(0, -64) + 'b'.repeat(64) }] },
      } }],
    }))
    writeFileSync(files.calls, '')
    writeFileSync(files.summary, '')
    const mocks = {
      az: `#!/usr/bin/env bash
set -eu
case "$1" in
  containerapp)
    printf 'fqdn\\n' >> "$CALLS"
    case "$*" in
      *"properties.latestRevisionFqdn"*) printf 'ca-roamly-prod--fixture.example.test\\n' ;;
      *) printf 'app.example.test\\n' ;;
    esac
    exit "$FQDN_EXIT" ;;
  rest)
    printf 'revisions\\n' >> "$CALLS"
    if [[ "$TRANSITION" == "true" && ! -f "$READY" ]]; then cat "$TRANSITION_FIXTURE"; else cat "$FIXTURE"; fi
    printf '%s' "$CLI_ERROR" >&2
    exit "$AZ_EXIT" ;;
  *) exit 99 ;;
esac
`,
      jq: '#!/usr/bin/env bash\nprintf "jq\\n" >> "$CALLS"\nexec "$REAL_JQ" "$@"\n',
      curl: `#!/usr/bin/env bash
printf 'probe\\n' >> "$CALLS"
case "\${@: -1}" in *ca-roamly-prod--fixture.example.test/health/*) printf ready > "$READY" ;; esac
printf '%s' "$HEALTH_STATUS"
`,
      sleep: '#!/usr/bin/env bash\nexit 0\n',
    }
    for (const [name, content] of Object.entries(mocks)) {
      writeFileSync(join(directory, name), content, { mode: 0o755 })
    }
    writeFileSync(files.script, `
export PATH="$MOCK_BIN:$PATH"
[[ "$(command -v az)" == "$MOCK_BIN/az" ]] || exit 98
${smoke}`)
    const result = spawnSync(bash, ['-e', '-o', 'pipefail', shellPath(files.script)], {
      encoding: 'utf8',
      timeout: 15_000,
      env: {
        ...process.env,
        MOCK_BIN: shellPath(directory),
        RELEASE_IMAGE: image,
        AZURE_RESOURCE_GROUP: 'rg-fixture',
        AZURE_SUBSCRIPTION_ID: '00000000-0000-0000-0000-000000000001',
        GITHUB_STEP_SUMMARY: shellPath(files.summary),
        FIXTURE: shellPath(files.fixture),
        CALLS: shellPath(files.calls),
        AZ_EXIT: String(azExit),
        FQDN_EXIT: String(fqdnExit),
        CLI_ERROR: cliError,
        REAL_JQ: realJq,
        TRANSITION: String(transitionUntilRevisionProbed),
        TRANSITION_FIXTURE: shellPath(files.transition),
        READY: shellPath(files.ready),
        HEALTH_STATUS: String(healthStatus),
      },
    })
    assert.equal(result.error, undefined,
      `${result.error?.message}\n${result.stdout}\n${result.stderr}\n${readFileSync(files.calls, 'utf8')}`)
    return {
      ...result,
      calls: readFileSync(files.calls, 'utf8').trim().split('\n').filter(Boolean),
      summary: readFileSync(files.summary, 'utf8'),
    }
  } finally {
    rmSync(directory, { recursive: true, force: true })
  }
}

test('real Bash gate requires new revision health and then one active revision with the exact digest', () => {
  const result = runSmoke()
  assert.equal(result.status, 0, result.stdout + result.stderr)
  assert.deepEqual(result.calls, ['fqdn', 'probe', 'probe', 'revisions', 'jq'])
})

test('Single rollout waits for the new revision readiness before requiring old revision deactivation', () => {
  const result = runSmoke({ transitionUntilRevisionProbed: true })
  assert.equal(result.status, 0, result.stdout + result.stderr)
  assert.deepEqual(result.calls, ['fqdn', 'probe', 'probe', 'revisions', 'jq'])
})

test('failed revision CLI cannot admit its stdout even when it contains the expected digest', () => {
  const result = runSmoke({ azExit: 17 })
  assert.equal(result.status, 1, result.stdout + result.stderr)
  assert.equal(result.calls.filter(call => call === 'revisions').length, 6)
  assert.equal(result.calls.filter(call => call === 'probe').length, 2)
  assert.equal(result.calls.filter(call => call === 'jq').length, 0)
  assert.match(result.stdout, /CLI exit=17; errore=redacted/)
})

test('FQDN CLI failure cannot admit its stdout or start the revision and health probes', () => {
  const result = runSmoke({ fqdnExit: 17 })
  assert.equal(result.status, 1, result.stdout + result.stderr)
  assert.deepEqual(result.calls, Array(6).fill('fqdn'))
  assert.match(result.stdout, /Metadata ACA FQDN: CLI exit=17/)
})

test('CLI diagnostics expose only an allowlisted error category, never raw error details', () => {
  const marker = 'DO-NOT-LOG-test-credential'
  const result = runSmoke({
    azExit: 1,
    cliError: `ERROR: (AuthorizationFailed) credential=${marker}\n::error::${marker}`,
  })
  assert.equal(result.status, 1)
  assert.match(result.stdout, /CLI exit=1; errore=AuthorizationFailed/)
  assert.doesNotMatch(result.stdout + result.stderr + result.summary, new RegExp(marker))
  assert.equal(result.calls.filter(call => call === 'probe').length, 2)
})

test('metadata timeout retains the six-attempt budget and is never admitted', () => {
  const result = runSmoke({ azExit: 124 })
  assert.equal(result.status, 1)
  assert.match(result.stdout, /CLI exit=124; errore=Timeout/)
  assert.equal(result.calls.filter(call => call === 'revisions').length, 6)
  assert.equal(result.calls.filter(call => call === 'probe').length, 2)
})

for (const [name, revisions] of [
  ['wrong digest', {
    value: [{ ...fixture.value[0], properties: {
      active: true, template: { containers: [{ image: image.slice(0, -64) + 'b'.repeat(64) }] },
    } }],
  }],
  ['two active revisions', { value: [fixture.value[0], fixture.value[0]] }],
  ['no active revision', { value: [] }],
  ['paginated response', { ...fixture, nextLink: 'https://example.test/next' }],
  ['error JSON', { error: { code: 'AuthorizationFailed', message: 'DO-NOT-LOG-error-body' } }],
  ['array instead of ARM envelope', fixture.value],
  ['null revision collection', { value: null }],
]) {
  test(`healthy revision endpoints cannot override the final gate rejecting ${name}`, () => {
    const result = runSmoke({ revisions })
    assert.equal(result.status, 1, result.stdout + result.stderr)
    assert.equal(result.calls.filter(call => call === 'revisions').length, 6)
    assert.equal(result.calls.filter(call => call === 'probe').length, 2)
    assert.match(result.stdout, /CLI exit=0; jq exit=/)
    assert.doesNotMatch(result.stdout + result.stderr + result.summary, /DO-NOT-LOG/)
  })
}

test('malformed JSON fails with parser status, without disclosing the response body', () => {
  const result = runSmoke({ rawRevisions: '{"secret": "DO-NOT-LOG-invalid-json"' })
  assert.equal(result.status, 1)
  assert.match(result.stdout, /CLI exit=0; jq exit=[45]/)
  assert.equal(result.calls.filter(call => call === 'revisions').length, 6)
  assert.equal(result.calls.filter(call => call === 'probe').length, 2)
  assert.doesNotMatch(result.stdout + result.stderr + result.summary, /DO-NOT-LOG/)
})

test('real gate accepts a large response without logging revision environment data', () => {
  const result = runSmoke({ revisions: {
    value: [fixture.value[0], ...Array.from({ length: 13 }, () => ({
      properties: { active: false, template: { containers: [{
        image, env: [{ name: 'unused', value: 'DO-NOT-LOG'.repeat(10_000) }],
      }] } },
    }))],
  } })
  assert.equal(result.status, 0, result.stdout + result.stderr)
  assert.deepEqual(result.calls, ['fqdn', 'probe', 'probe', 'revisions', 'jq'])
  assert.doesNotMatch(result.stdout + result.stderr + result.summary, /DO-NOT-LOG/)
})

test('unhealthy new revision exhausts only the existing health budget and cannot reach success', () => {
  const result = runSmoke({ healthStatus: 503 })
  assert.equal(result.status, 1)
  assert.equal(result.calls.filter(call => call === 'probe').length, 10)
  assert.equal(result.calls.filter(call => call === 'revisions').length, 0)
  assert.match(result.stdout, /Smoke test della nuova revisione fallito/)
})
