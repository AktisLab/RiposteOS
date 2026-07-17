import assert from 'node:assert/strict'
import test from 'node:test'
import {
  addCpvCode,
  findCpvLabel,
  findCpvMatches,
  toCpvPrefix,
  type CpvCatalogEntry,
} from './cpv-catalog.ts'

const entries: CpvCatalogEntry[] = [
  [
    '72000000',
    "Services de technologies de l'information, conseil, développement de logiciels, internet et appui",
  ],
  ['72200000', 'Services de programmation et de conseil en logiciels'],
  ['72212000', "Services de programmation de logiciels d'application"],
]

test('converts an official CPV code to its hierarchical prefix', () => {
  assert.equal(toCpvPrefix('72000000'), '72')
  assert.equal(toCpvPrefix('72212000'), '72212')
  assert.equal(toCpvPrefix('72212211'), '72212211')
})

test('adds a CPV prefix without keeping redundant children', () => {
  assert.deepEqual(addCpvCode(['722'], '72000000'), ['72'])
  assert.deepEqual(addCpvCode(['72'], '72212000'), ['72'])
})

test('searches CPV codes and French labels without accents', () => {
  assert.deepEqual(findCpvMatches(entries, 'developpement logiciels'), [
    entries[0],
  ])
  assert.deepEqual(findCpvMatches(entries, '72212'), [entries[2]])
  assert.deepEqual(findCpvMatches(entries, '7'), [])
})

test('resolves the label represented by a prefix', () => {
  assert.equal(
    findCpvLabel(entries, '722'),
    'Services de programmation et de conseil en logiciels'
  )
})
