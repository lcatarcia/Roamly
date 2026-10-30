import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'

const active = readFileSync(new URL('../../../.github/workflows/release.yml', import.meta.url), 'utf8')
const review = readFileSync(new URL('../release.yml', import.meta.url), 'utf8')

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
}
