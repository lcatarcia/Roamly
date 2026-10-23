import assert from 'node:assert/strict'
import test from 'node:test'

import { formatCombGuid } from '../../src/lib/comb-guid.mjs'

test('COMB bytes match the .NET Guid and counter layout', () => {
  assert.equal(
    formatCombGuid(0x0102030405060708n, '00112233-4455-4677-8899-aabbccddeeff'),
    '00112233-4455-4677-0708-010203040506',
  )
})

test('malformed random Guid input is rejected', () => {
  assert.throws(
    () => formatCombGuid(1n, 'not-a-guid'),
    { name: 'TypeError', message: 'A valid Guid is required.' },
  )
})
