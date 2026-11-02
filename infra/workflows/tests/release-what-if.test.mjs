import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'

const active = readFileSync(new URL('../../../.github/workflows/release.yml', import.meta.url), 'utf8')
const review = readFileSync(new URL('../release.yml', import.meta.url), 'utf8')
const containerApp = readFileSync(new URL('../../container-app.bicep', import.meta.url), 'utf8')

test('active and review release workflows remain identical', () => {
  assert.equal(active, review)
})

for (const [name, source] of [['active', active], ['review', review]]) {
  test(`${name}: both what-if captures request machine-readable JSON`, () => {
    const calls = [...source.matchAll(/what_if_output="\$\((az deployment group what-if \\\r?\n[\s\S]*?)\)"\r?\n/g)]
    assert.equal(calls.length, 2, 'Preparation and pre-deploy verification must both preview the plan')
    for (const [, command] of calls) {
      assert.match(command, /--no-pretty-print\s+\\\r?\n/, 'Generic --output json does not disable the what-if text renderer')
      assert.match(command, /--output json$/)
    }
  })

  test(`${name}: both plan parsers retain fail-closed validation`, () => {
    const parsers = [...source.matchAll(/plan_changes="\$\(([\s\S]*?)\)"\r?\n/g)]
    assert.equal(parsers.length, 2)
    for (const [, parser] of parsers) {
      assert.match(parser, /jq -e -S -c/)
      assert.match(parser, /\.status == "Succeeded" and \(\.changes \| type == "array"\)/)
      assert.match(parser, /error\("what-if non riuscito o output non valido"\)/)
    }
  })

  test(`${name}: release and rollback use the owner-scoped GHCR repository`, () => {
    assert.match(source, /IMAGE_REGISTRY: ghcr\.io/)
    assert.equal([...source.matchAll(/image_repository="\$\{GITHUB_REPOSITORY,,\}"/g)].length, 2)
    assert.doesNotMatch(source, /ACR_LOGIN_SERVER|ACR_NAME|az acr login|registryServer=/)
    const prepare = source.split('\n  prepare:')[1].split('\n  deploy:')[0]
    const deploy = source.split('\n  deploy:')[1]
    assert.match(prepare, /packages: write/)
    assert.doesNotMatch(deploy, /packages: write/)
    assert.match(prepare, /GHCR_TOKEN: \$\{\{ secrets\.GITHUB_TOKEN \}\}/)
    assert.match(prepare, /printf '%s' "\$GHCR_TOKEN" \| docker login .* --password-stdin/)
  })

  test(`${name}: public pull is checked without the publisher credentials`, () => {
    const anonymousStep = source.split('      - name: Verifica accesso anonimo al digest GHCR')[1]
      .split('      - name: Generazione del what-if Bicep')[0]
    assert.match(anonymousStep, /set -euo pipefail/)
    assert.match(anonymousStep, /anonymous_config="\$\(mktemp -d\)"/)
    assert.match(anonymousStep, /DOCKER_CONFIG="\$anonymous_config" docker manifest inspect "\$RELEASE_IMAGE"/)
    assert.doesNotMatch(anonymousStep, /\|\| true|continue-on-error|docker login/)
  })

  test(`${name}: all previews and deployment preserve the live SQL and HMAC references`, () => {
    assert.match(source, /id: preflight/)
    assert.match(source, /sql_secret_url: \$\{\{ steps\.preflight\.outputs\.sql_secret_url \}\}/)
    assert.match(source, /login_hmac_secret_url: \$\{\{ steps\.preflight\.outputs\.login_hmac_secret_url \}\}/)
    assert.match(source, /roamly-sql-connection\/\[0-9a-fA-F\]\{32\}\$/)
    assert.equal([...source.matchAll(/sqlConnectionSecretUrl="\$SQL_SECRET_URL"/g)].length, 3)
    assert.equal([...source.matchAll(/loginHmacSecretUrl="\$LOGIN_HMAC_SECRET_URL"/g)].length, 3)
    assert.equal([...source.matchAll(/SQL_SECRET_URL: \$\{\{ needs\.prepare\.outputs\.sql_secret_url \}\}/g)].length, 2)
    assert.equal([...source.matchAll(/LOGIN_HMAC_SECRET_URL: \$\{\{ needs\.prepare\.outputs\.login_hmac_secret_url \}\}/g)].length, 2)
    assert.doesNotMatch(source, /az keyvault secret show|listSecrets/)
  })
}

test('public image deployment does not configure registry credentials', () => {
  assert.match(containerApp, /registries: \[\]/)
  assert.doesNotMatch(containerApp, /param registryServer|passwordSecretRef/)
})
