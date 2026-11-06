import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync } from 'node:fs'
import { categorySymbols, mapCamera, positionedPlaces } from '../../src/features/places/map-model.mjs'
import { localMapsStyles } from '../../src/features/places/map-sdk-style.mjs'

test('vendor stylesheet keeps attribution/layout but cannot load global remote fonts', () => {
  const vendor = readFileSync(new URL('../../node_modules/azure-maps-control/dist/atlas.min.css', import.meta.url), 'utf8')
  assert.match(vendor, /@font-face/)
  const local = localMapsStyles(vendor)
  assert.doesNotMatch(local, /@font-face|url\(\s*['"]?https?:\/\//)
  assert.match(local, /copyright/)
  assert.match(local, /atlas-map/)
})

test('only valid coordinate pairs are mapped, including zero', () => {
  const places = [null, { latitude: 0, longitude: 0 }, { latitude: 91, longitude: 0 },
    { latitude: 1, longitude: NaN }, { latitude: null, longitude: 0 }]
    .map((position, id) => ({ id, position }))
  assert.deepEqual(positionedPlaces(places).map(p => p.id), [1])
})

test('all fourteen categories have a distinct non-colour symbol', () => {
  assert.equal(categorySymbols.length, 14)
  assert.equal(new Set(categorySymbols).size, 14)
})

test('camera handles empty, single and short antimeridian arc', () => {
  assert.equal(mapCamera([]), null)
  assert.deepEqual(mapCamera([{ position: { latitude: 0, longitude: 0 } }]),
    { center: [0, 0], zoom: 12 })
  const camera = mapCamera([179, -179].map(longitude => ({ position: { latitude: 10, longitude } })))
  assert.deepEqual(camera.bounds, [179, 10, 181, 10])
  assert.equal(camera.maxZoom, 12)
})
