import assert from 'node:assert/strict'
import test from 'node:test'
import {
  consultationDocumentKindLabels,
  formatConsultationDeadline,
  formatFileSize,
  getDocumentAnalysisPresentation,
  getDocumentProcessingPresentation,
  hasActiveDocumentProcessing,
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

test('presents the document processing pipeline', () => {
  assert.deepEqual(
    getDocumentProcessingPresentation(
      {
        status: 'Completed',
        queuedAt: null,
        startedAt: null,
        completedAt: null,
        failedAt: null,
        pageCount: 3,
        passageCount: 20,
        errorMessage: null,
      },
      {
        status: 'Running',
        proposedKind: null,
        confidence: null,
        queuedAt: null,
        startedAt: null,
        completedAt: null,
        failedAt: null,
        providerName: null,
        model: null,
        errorMessage: null,
      }
    ),
    {
      label: 'Classement en cours…',
      isActive: true,
    }
  )
  assert.equal(
    hasActiveDocumentProcessing([
      {
        analysis: {
          status: 'Completed',
          queuedAt: null,
          startedAt: null,
          completedAt: null,
          failedAt: null,
          pageCount: 3,
          passageCount: 20,
          errorMessage: null,
        },
        classification: {
          status: 'Queued',
          proposedKind: null,
          confidence: null,
          queuedAt: null,
          startedAt: null,
          completedAt: null,
          failedAt: null,
          providerName: null,
          model: null,
          errorMessage: null,
        },
      },
    ]),
    true
  )
})

test('presents document analysis states', () => {
  assert.deepEqual(
    getDocumentAnalysisPresentation({
      status: 'Completed',
      queuedAt: null,
      startedAt: null,
      completedAt: null,
      failedAt: null,
      pageCount: 12,
      passageCount: 42,
      errorMessage: null,
    }),
    { label: 'Analysé', isActive: false }
  )
  assert.equal(
    getDocumentAnalysisPresentation({
      status: 'Failed',
      queuedAt: null,
      startedAt: null,
      completedAt: null,
      failedAt: null,
      pageCount: 0,
      passageCount: 0,
      errorMessage: 'Docling indisponible.',
    }).actionLabel,
    'Réessayer'
  )
  assert.equal(
    hasActiveDocumentProcessing([
      {
        analysis: {
          status: 'Running',
          queuedAt: null,
          startedAt: null,
          completedAt: null,
          failedAt: null,
          pageCount: 0,
          passageCount: 0,
          errorMessage: null,
        },
      },
    ]),
    true
  )
})

test('presents a single document status through indexing', () => {
  assert.deepEqual(
    getDocumentProcessingPresentation(
      {
        status: 'Completed',
        queuedAt: null,
        startedAt: null,
        completedAt: null,
        failedAt: null,
        pageCount: 3,
        passageCount: 20,
        errorMessage: null,
      },
      {
        status: 'Completed',
        proposedKind: null,
        confidence: null,
        queuedAt: null,
        startedAt: null,
        completedAt: null,
        failedAt: null,
        providerName: null,
        model: null,
        errorMessage: null,
      },
      {
        status: 'Running',
        indexedPassageCount: 7,
        passageCount: 20,
        errorMessage: null,
      }
    ),
    { label: 'Indexation en cours…', isActive: true }
  )
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
