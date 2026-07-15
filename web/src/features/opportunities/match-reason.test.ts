import assert from 'node:assert/strict'
import test from 'node:test'
import { type CpvCatalogEntry } from '../../lib/cpv-catalog.ts'
import { presentMatchReason } from './match-reason.ts'

const entries: CpvCatalogEntry[] = [
  ['72200000', 'Services de programmation et de conseil en logiciels'],
]

test('adds the French CPV label to a score reason', () => {
  assert.deepEqual(presentMatchReason('+25 CPV ciblé : 72200000', entries), {
    weight: '+25',
    label: 'CPV ciblé',
    detail: '72200000 · Services de programmation et de conseil en logiciels',
  })
})

test('keeps non-CPV score reasons unchanged', () => {
  assert.deepEqual(presentMatchReason('+15 Signal positif : api', entries), {
    weight: '+15',
    label: 'Signal positif : api',
  })
})
