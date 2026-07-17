import assert from 'node:assert/strict'
import test from 'node:test'
import {
  formatEstimatedValue,
  presentContractNature,
  presentExecutionDuration,
  presentProcedureType,
} from './opportunity-presentation.ts'

test('formats an estimated value with its normalized currency', () => {
  assert.equal(formatEstimatedValue(1250000, ' eur '), '1 250 000 EUR')
})

test('does not invent an amount when the source has none or is not enriched yet', () => {
  assert.equal(formatEstimatedValue(null, 'EUR'), null)
  assert.equal(formatEstimatedValue(undefined, undefined), null)
})

test('presents source codes with French business labels', () => {
  assert.equal(presentProcedureType('comp-dial'), 'Dialogue compétitif')
  assert.equal(presentContractNature('works'), 'Travaux')
  assert.equal(presentExecutionDuration('1 DAY'), '1 jour')
  assert.equal(presentExecutionDuration('48 MONTH'), '48 mois')
})

test('keeps unknown source values readable', () => {
  assert.equal(presentProcedureType(' Procédure adaptée '), 'Procédure adaptée')
  assert.equal(presentContractNature(null), null)
  assert.equal(
    presentExecutionDuration("Jusqu'au 31 décembre"),
    "Jusqu'au 31 décembre"
  )
})
