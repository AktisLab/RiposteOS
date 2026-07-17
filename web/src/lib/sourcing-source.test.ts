import assert from 'node:assert/strict'
import test from 'node:test'
import { findSourcingSource, sourcingSources } from './sourcing-source.ts'

test('exposes every supported sourcing source and resolves API casing', () => {
  assert.deepEqual(
    sourcingSources.map((source) => source.value),
    ['boamp', 'ted', 'place']
  )
  assert.equal(findSourcingSource('PLACE')?.label, 'PLACE')
  assert.equal(findSourcingSource('unknown'), null)
})
