import assert from 'node:assert/strict'
import test from 'node:test'
import { googleMapsPosition, linkPositionFields } from '../../src/features/places/google-maps-position.mjs'

const bunker = 'https://www.google.com/maps/place/Bunker+Soratte/@42.2370291,12.5091909,805m/data=!3m2!1e3!4b1!4m6!3m5!1s0x132f14154ace8c1d:0x881d17f0b2739244!8m2!3d42.2370252!4d12.5117658!16s%2Fg%2F11gbfd5xfy?authuser=0&entry=tts'

test('extracts exact place, never viewport, and standard encoded numeric queries', () => {
  assert.deepEqual(googleMapsPosition(bunker), { latitude: 42.2370252, longitude: 12.5117658 })
  for (const url of [
    'https://www.google.it/maps/search/?api=1&query=0%2C-180',
    'https://maps.google.com/?q=0,-180',
    'https://www.google.com/maps/data=%213d0%214d-180',
  ]) assert.deepEqual(googleMapsPosition(url), { latitude: 0, longitude: -180 })
})

test('untrusted, ambiguous, malformed, viewport and short links never guess a POI', () => {
  for (const url of [
    'https://google.com.evil.test/maps/?q=1,2', 'https://evil@google.com/maps/?q=1,2',
    'https://www.google.com/maps/@1,2,12z', 'https://maps.app.goo.gl/example',
    'https://www.google.com/maps/data=!3d1!4d2!3d3!4d4',
    'https://www.google.com/maps/data=!3d1', 'https://www.google.com/maps/data=!4d2',
    'https://www.google.com/maps/data=!3d91!4d2',
    'https://www.google.com/maps/?q=NaN,2', 'https://www.google.com/maps/?q=1,Infinity',
    'https://www.google.com/maps/?q=1,181', 'https://www.google.com/maps/?q=Place+1,2',
    'https://www.google.com/maps/?q=1,2&q=3,4', 'https://www.google.com/maps/?q=1,2&query=3,4&api=1',
    'https://www.google.com/maps/?q=%2531%252C2', 'https://www.google.com/maps/?q=%zz',
    'https://www.google.com/maps/data=!3d1!4d2?q=3,4',
    'http://www.google.com/maps/?q=1,2', 'not a URL',
    'https:////google.com/maps/?q=1,2', 'https://%67oogle.com/maps/?q=1,2',
    'https://google.com/maps/%zz/data=!3d1!4d2',
    'https://www.google.com/maps/dir/First/Second/data=!3d1!4d2',
    'https://www.google.com/maps/?query=1,2',
    'https://www.google.com/maps/?api=1&api=2&query=1,2',
  ]) assert.equal(googleMapsPosition(url), null, url)
})

test('link edits update only untouched automatically derived fields', () => {
  const old = { latitude: '1', longitude: '2' }
  const next = 'https://maps.google.com/?q=3,4'
  assert.deepEqual(linkPositionFields(old, old, next), { latitude: '3', longitude: '4' })
  assert.deepEqual(linkPositionFields(old, old, ''), { latitude: '', longitude: '' })
  assert.deepEqual(linkPositionFields({ latitude: '9', longitude: '2' }, old, next),
    { latitude: '9', longitude: '4' })
  assert.deepEqual(linkPositionFields({ latitude: '9', longitude: '8' }, null, next),
    { latitude: '9', longitude: '8' })
  assert.deepEqual(linkPositionFields({ latitude: '', longitude: '' }, null, next),
    { latitude: '3', longitude: '4' })
  assert.deepEqual(linkPositionFields({ latitude: '', longitude: '2' }, old, next, { latitude: true }),
    { latitude: '', longitude: '4' }, 'a deliberately cleared manual field stays empty')
})
