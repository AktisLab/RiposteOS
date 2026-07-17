import assert from 'node:assert/strict'
import test from 'node:test'
import { parseCountryCodes } from './country-codes.ts'

test('normalizes the explicit TED country selection', () => {
  assert.deepEqual(parseCountryCodes(' fra\nBEL\nFRA\n\n'), ['FRA', 'BEL'])
  assert.deepEqual(parseCountryCodes(''), [])
})
