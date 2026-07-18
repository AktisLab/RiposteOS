import assert from 'node:assert/strict'
import test from 'node:test'
import {
  consultationDocumentKindLabels,
  formatConsultationDeadline,
  formatFileSize,
  nextConsultationAction,
  normalizeConsultationForm,
} from './presentation.ts'

test('presents consultation workflow values', () => {
  assert.equal(nextConsultationAction(0), 'Ajouter le DCE')
  assert.equal(nextConsultationAction(2), 'Consulter les documents')
  assert.equal(formatConsultationDeadline(null), 'Non renseignée')
  assert.equal(formatFileSize(1536), '1,5 Ko')
  assert.equal(consultationDocumentKindLabels.TechnicalSpecifications, 'CCTP')
})

test('normalizes the manual consultation form', () => {
  const normalized = normalizeConsultationForm({
    title: '  Marché de travaux ',
    buyer: ' Ville de Lyon  ',
    responseDeadline: '',
    noticeUrl: '  ',
  })

  assert.deepEqual(normalized, {
    title: 'Marché de travaux',
    buyer: 'Ville de Lyon',
    responseDeadline: null,
    noticeUrl: null,
  })
})
