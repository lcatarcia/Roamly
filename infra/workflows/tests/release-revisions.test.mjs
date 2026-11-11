import assert from 'node:assert/strict'
import { execFileSync, spawnSync } from 'node:child_process'
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { runInNewContext } from 'node:vm'
import test from 'node:test'

const workflow = readFileSync(new URL('../../../.github/workflows/release.yml', import.meta.url), 'utf8')
const app = '/subscriptions/00000000-0000-0000-0000-000000000001/resourceGroups/rg-fixture/providers/Microsoft.App/containerApps/ca-roamly-prod'
const endpoint = `https://management.azure.com${app}/revisions`
const first = `${endpoint}?api-version=2024-03-01`
const second = `${first}&$skipToken=page2`
const image = `ghcr.io/example/roamly@sha256:${'a'.repeat(64)}`
const revision = (suffix = 'fixture', active = true) => ({
  id: `${app}/revisions/ca-roamly-prod--${suffix}`,
  name: `ca-roamly-prod--${suffix}`,
  properties: { active, template: { containers: [{ image }] } },
})
const scripts = () => [...workflow.matchAll(/node - "\$app_id" <<'REVISIONS_NODE'\r?\n([\s\S]*?)\r?\n {10}REVISIONS_NODE/g)]
  .map(match => match[1].split(/\r?\n/).map(line => line.replace(/^ {10}/, '')).join('\n'))
const jq = process.env.ROAMLY_TEST_JQ ?? 'jq'
const jqAvailable = spawnSync(jq, ['--version'], { encoding: 'utf8' }).status === 0
const gate = workflow.split('if revision_check=')[1]
  .split("--arg expected_image \"$RELEASE_IMAGE\" '")[1].split("' 2>&1)")[0]
const passesGate = value => spawnSync(jq, ['-e', '--arg', 'expected_image', image, gate], {
  input: value, encoding: 'utf8',
}).status === 0

// Execute the workflow's actual Node collector. Only the external executable seam
// is replaced: each authenticated read launches a local, deterministic az process.
function collect(pages, { script = scripts()[0], clock = () => 0 } = {}) {
  assert.ok(script, 'The workflow must collect all revision pages before either consumer evaluates them')
  const directory = mkdtempSync(join(tmpdir(), 'roamly-revisions-'))
  const calls = []
  const output = []
  const errors = []
  let exitCode = 0
  try {
    const mock = join(directory, 'az.mjs')
    const fixtures = join(directory, 'pages.json')
    writeFileSync(fixtures, JSON.stringify(pages))
    writeFileSync(mock, `
import { readFileSync } from 'node:fs'
const args = process.argv.slice(2)
const pages = JSON.parse(readFileSync(process.env.REVISION_FIXTURES, 'utf8'))
const page = pages[args[args.indexOf('--url') + 1]]
if (!page) process.exit(99)
if (page.hang) await new Promise(resolve => setTimeout(resolve, 30_000))
if (page.stderr) process.stderr.write(page.stderr)
const stdout = page.raw !== undefined ? page.raw : JSON.stringify(page.body)
if (page.deferStdout) setImmediate(() => process.stdout.write(stdout))
else process.stdout.write(stdout)
// Let stdout/stderr drain; process.exit() truncates asynchronous Linux pipe writes.
process.exitCode = page.status ?? 0
`)
    const external = (command, args, options) => {
      assert.equal(command, 'az')
      assert.deepEqual(Array.from(args).slice(0, 3), ['rest', '--method', 'get'])
      assert.ok(args.includes('--only-show-errors'))
      assert.equal(args[args.indexOf('--output') + 1], 'json')
      assert.ok(options.timeout > 0 && options.timeout <= 10_000)
      calls.push(args[args.indexOf('--url') + 1])
      return execFileSync(process.execPath, [mock, ...args], {
        ...options,
        env: { ...process.env, REVISION_FIXTURES: fixtures },
      })
    }
    try {
      runInNewContext(script, {
        require: name => {
          assert.equal(name, 'node:child_process')
          return { execFileSync: external }
        },
        process: {
          argv: ['node', '-', app],
          stdout: { write: value => output.push(value) },
          stderr: { write: value => errors.push(value) },
          exit: code => { exitCode = code; throw new Error('collector exit') },
        },
        URL,
        Date: { now: clock },
      })
    } catch (error) {
      if (error.message !== 'collector exit') throw error
    }
    return { exitCode, calls, output: output.join(''), errors: errors.join('') }
  } finally {
    rmSync(directory, { recursive: true, force: true })
  }
}

test('pagination includes a second active revision instead of admitting the first page alone', () => {
  const result = collect({
    [first]: { body: { value: [revision()], nextLink: second } },
    [second]: { body: { value: [revision('other')] } },
  })
  assert.equal(result.exitCode, 0, result.errors)
  assert.deepEqual(result.calls, [first, second])
  assert.equal(JSON.parse(result.output).value.filter(item => item.properties.active).length, 2)
})

test('preflight and smoke execute the same collector with the exact app ID', () => {
  assert.equal(scripts().length, 2)
  assert.equal(scripts()[0], scripts()[1])
  assert.match(workflow, /read_revisions > \/dev\/null \|\| fail /)
  assert.match(workflow, /if revisions_json="\$\(read_revisions 2>&1\)"; then/)
  for (const script of scripts()) {
    const result = collect({
      [first]: { body: { value: [revision()], nextLink: second } },
      [second]: { body: { value: [revision('old', false)], nextLink: null } },
    }, { script })
    assert.equal(result.exitCode, 0, result.errors)
    assert.equal(JSON.parse(result.output).value.length, 2)
    assert.deepEqual(result.calls, [first, second])
  }
})

test('real smoke predicate rejects another active revision on page two', {
  skip: !jqAvailable && 'Install jq or set ROAMLY_TEST_JQ to execute the real gate predicate',
}, () => {
  const result = collect({
    [first]: { body: { value: [revision()], nextLink: second } },
    [second]: { body: { value: [revision('other')] } },
  })
  assert.equal(result.exitCode, 0, result.errors)
  assert.equal(passesGate(result.output), false)
})

test('real smoke predicate accepts paginated history only with one active exact digest', {
  skip: !jqAvailable && 'Install jq or set ROAMLY_TEST_JQ to execute the real gate predicate',
}, () => {
  const result = collect({
    [first]: { body: { value: [revision('old', false)], nextLink: second } },
    [second]: { body: { value: [revision()] } },
  })
  assert.equal(result.exitCode, 0, result.errors)
  assert.equal(passesGate(result.output), true)
  const body = JSON.parse(result.output)
  body.value[1].properties.template.containers[0].image = image.slice(0, -1) + 'b'
  assert.equal(passesGate(JSON.stringify(body)), false)
  body.value[1].properties.active = false
  assert.equal(passesGate(JSON.stringify(body)), false)
})

test('a single unpaginated page is accepted; revision env/secret data is never returned', () => {
  const item = revision()
  item.properties.template.containers[0].env = [{ name: 'secret', value: 'DO-NOT-LOG' }]
  const result = collect({ [first]: { body: { value: [item] } } })
  assert.equal(result.exitCode, 0, result.errors)
  assert.equal(JSON.parse(result.output).value.length, 1)
  assert.doesNotMatch(result.output + result.errors, /DO-NOT-LOG/)
})

test('large revision response is reduced to gate metadata without exposing environment data', () => {
  const item = revision()
  item.properties.template.containers[0].env = [{ name: 'secret', value: 'DO-NOT-LOG'.repeat(150_000) }]
  // Exercise queued pipe output deterministically, including on Windows.
  const result = collect({ [first]: { body: { value: [item] }, deferStdout: true } })
  assert.equal(result.exitCode, 0, result.errors)
  assert.ok(result.output.length < 1_000)
  assert.doesNotMatch(result.output + result.errors, /DO-NOT-LOG/)
})

test('opaque encoded skip tokens and query reordering stay on the exact ARM endpoint', () => {
  const continuation = `${endpoint}?$skiptoken=opaque%2B%2F%3D&api-version=2024-03-01`
  const result = collect({
    [first]: { body: { value: [revision()], nextLink: continuation } },
    [continuation]: { body: { value: [revision('old', false)] } },
  })
  assert.equal(result.exitCode, 0, result.errors)
  assert.deepEqual(result.calls, [first, continuation])
})

for (const target of [
  'https://evil.example/next',
  second.replace('https:', 'http:'),
  second.replace('management.azure.com', 'management.azure.com.evil.example'),
  second.replace('management.azure.com', 'management.azure.com@evil.example'),
  second.replace('management.azure.com', 'user:password@management.azure.com'),
  second.replace('management.azure.com', 'management.azure.com:443'),
  second.replace('ca-roamly-prod/revisions', 'other-app/revisions'),
  second.replace('/revisions?', '/revisions/../revisions?'),
  second.replace('/revisions?', '/%72evisions?'),
  second.replace('2024-03-01', '2025-01-01'),
  `${second}&api-version=2024-03-01`,
  `${second}&unexpected=parameter`,
  `${second}#fragment`,
  `${second}%invalid`,
  `${first}&$skipToken=`,
  first, '', true, { next: second },
]) {
  test(`invalid continuation is rejected before any authenticated follow-up: ${JSON.stringify(target)}`, () => {
    const result = collect({ [first]: { body: { value: [revision()], nextLink: target } } })
    assert.notEqual(result.exitCode, 0)
    assert.deepEqual(result.calls, [first])
    assert.equal(result.output, '', 'A partial list must never reach either consumer')
    assert.match(result.errors, /ContinuationTargetInvalid/)
    assert.doesNotMatch(result.errors, /password|evil\.example/)
  })
}

for (const [name, body] of [
  ['empty collection', { value: [] }],
  ['null collection', { value: null }],
  ['missing collection', { error: { message: 'DO-NOT-LOG' } }],
  ['array envelope', [revision()]],
  ['null envelope', null],
  ['foreign revision', { value: [{ ...revision(), id: '/another/app/revisions/x' }] }],
  ['missing ID', { value: [{ ...revision(), id: undefined }] }],
  ['invalid name', { value: [{ ...revision(), name: 'other-app--fixture' }] }],
  ['invalid active flag', { value: [{ ...revision(), properties: { active: 'true' } }] }],
  ['null revision', { value: [null] }],
]) {
  test(`malformed ${name} fails closed even when on a later page`, () => {
    const result = collect({
      [first]: { body: { value: [revision()], nextLink: second } },
      [second]: { body },
    })
    assert.notEqual(result.exitCode, 0)
    assert.equal(result.output, '')
    assert.doesNotMatch(result.errors, /DO-NOT-LOG/)
  })
}

test('duplicate revisions across pages are not silently deduplicated', () => {
  const result = collect({
    [first]: { body: { value: [revision()], nextLink: second } },
    [second]: { body: { value: [{ ...revision(), id: revision().id.toUpperCase() }] } },
  })
  assert.notEqual(result.exitCode, 0)
  assert.equal(result.output, '')
  assert.match(result.errors, /DuplicateRevision/)
})

test('a cyclic continuation stops before requesting the same page again', () => {
  const result = collect({
    [first]: { body: { value: [revision()], nextLink: second } },
    [second]: { body: { value: [revision('old', false)], nextLink: second } },
  })
  assert.notEqual(result.exitCode, 0)
  assert.deepEqual(result.calls, [first, second])
  assert.match(result.errors, /ContinuationCycle/)
  assert.equal(result.output, '')
})

test('pagination stops at one hundred pages without publishing a truncated list', () => {
  const pages = {}
  for (let index = 0; index <= 100; index++) {
    pages[index === 0 ? first : `${first}&$skipToken=${index}`] = { body: {
      value: [revision(`page-${index}`, false)], nextLink: `${first}&$skipToken=${index + 1}`,
    } }
  }
  const result = collect(pages)
  assert.notEqual(result.exitCode, 0)
  assert.equal(result.calls.length, 100)
  assert.match(result.errors, /PageLimitExceeded/)
  assert.equal(result.output, '')
})

test('collection deadline stops further requests even with valid continuation pages', () => {
  let time = -10_000
  const result = collect({
    [first]: { body: { value: [revision()], nextLink: second } },
  }, { clock: () => (time += 10_000) })
  assert.equal(result.exitCode, 124)
  assert.deepEqual(result.calls, [first])
  assert.equal(result.output, '')
  assert.match(result.errors, /Timeout/)
})

test('a hung az process is actually terminated at the remaining request deadline', () => {
  let firstClockRead = true
  const result = collect({ [first]: { hang: true } }, {
    clock: () => {
      if (firstClockRead) { firstClockRead = false; return 0 }
      return 19_950
    },
  })
  assert.equal(result.exitCode, 124)
  assert.equal(result.output, '')
  assert.match(result.errors, /Timeout/)
})

for (const page of [
  { raw: '{"secret":"DO-NOT-LOG"' },
  { raw: '{}\n{}' },
  { body: { value: [revision('old', false)] }, status: 17, stderr: 'AuthorizationFailed DO-NOT-LOG' },
  { body: { value: [revision('old', false)] }, status: 124, stderr: 'DO-NOT-LOG' },
]) {
  test(`later page failure returns no partial list and no response/token data: ${JSON.stringify(page).slice(0, 60)}`, () => {
    const result = collect({
      [first]: { body: { value: [revision()], nextLink: second } },
      [second]: page,
    })
    assert.notEqual(result.exitCode, 0)
    assert.equal(result.output, '')
    assert.doesNotMatch(result.errors, /DO-NOT-LOG/)
  })
}
