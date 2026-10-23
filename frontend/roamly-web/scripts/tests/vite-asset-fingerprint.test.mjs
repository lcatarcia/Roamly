import assert from 'node:assert/strict'
import test from 'node:test'

import { isFingerprintedViteWoff2 } from '../lib/vite-asset-fingerprint.mjs'

test('accepts Vite fingerprinted WOFF2 assets', () => {
  assert.equal(isFingerprintedViteWoff2('assets/inter-subset-Bx6tNKWZ.woff2'), true)
})

test('rejects WOFF2 files outside Vite assets', () => {
  assert.equal(isFingerprintedViteWoff2('fonts/inter-subset-Bx6tNKWZ.woff2'), false)
  assert.equal(isFingerprintedViteWoff2('inter-subset-Bx6tNKWZ.woff2'), false)
})

test('rejects WOFF2 assets without a content fingerprint', () => {
  assert.equal(isFingerprintedViteWoff2('assets/inter-subset.woff2'), false)
  assert.equal(isFingerprintedViteWoff2('assets/inter-subset-woff2'), false)
})
