import type {
  DocumentAnalysis,
  DocumentClassification,
  DocumentEmbedding,
  ConsultationDocumentKind,
  CreateConsultationRequest,
} from './api.ts'

export const consultationDocumentKindLabels: Record<
  ConsultationDocumentKind,
  string
> = {
  FullDce: 'DCE complet',
  ConsultationRules: 'Règlement de consultation',
  TechnicalSpecifications: 'CCTP',
  AdministrativeSpecifications: 'CCAP',
  CommitmentAct: 'Acte d’engagement',
  Pricing: 'Pièce financière',
  Appendix: 'Annexe',
  Other: 'Autre',
}

const dateTimeFormatter = new Intl.DateTimeFormat('fr-FR', {
  day: '2-digit',
  month: 'short',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
})

const sizeFormatter = new Intl.NumberFormat('fr-FR', {
  maximumFractionDigits: 1,
})

type DocumentProcessingPresentation = {
  label: string
  isActive: boolean
  actionLabel?: string
  retryTarget?: 'analysis' | 'classification' | 'embedding'
}

export function nextConsultationAction(documentCount: number) {
  return documentCount === 0 ? 'Ajouter le DCE' : 'Consulter les documents'
}

export function formatConsultationDeadline(value: string | null) {
  return value ? formatDateTime(value) : 'Non renseignée'
}

export function formatDateTime(value: string) {
  return dateTimeFormatter.format(new Date(value))
}

export function formatFileSize(size: number) {
  if (size < 1024) return `${size} o`
  if (size < 1024 * 1024) return `${sizeFormatter.format(size / 1024)} Ko`
  return `${sizeFormatter.format(size / (1024 * 1024))} Mo`
}

export function getDocumentAnalysisPresentation(analysis: DocumentAnalysis) {
  switch (analysis.status) {
    case 'Queued':
      return { label: 'Analyse en attente', isActive: true }
    case 'Running':
      return { label: 'Analyse en cours…', isActive: true }
    case 'Completed':
      return {
        label: 'Analysé',
        isActive: false,
      }
    case 'Failed':
      return {
        label: 'Analyse à reprendre',
        actionLabel: 'Réessayer',
        isActive: false,
      }
    case 'NotStarted':
      return {
        label: 'Analyse non lancée',
        actionLabel: 'Analyser',
        isActive: false,
      }
    case 'NotSupported':
      return {
        label: 'Analyse non disponible pour ce format',
        isActive: false,
      }
  }
}

function getDocumentClassificationPresentation(
  classification: DocumentClassification | undefined
) {
  if (!classification) {
    return { label: null, isActive: false }
  }

  switch (classification.status) {
    case 'NotStarted':
    case 'NotConfigured':
      return { label: null, isActive: false }
    case 'Queued':
      return { label: 'Classement en attente', isActive: true }
    case 'Running':
      return { label: 'Classement en cours…', isActive: true }
    case 'Completed':
      return { label: null, isActive: false }
    case 'Failed':
      return {
        label: 'Classement à reprendre',
        actionLabel: 'Réessayer',
        isActive: false,
      }
  }
}

function getDocumentEmbeddingPresentation(embedding: DocumentEmbedding) {
  switch (embedding.status) {
    case 'NotStarted':
      return {
        label: 'Indexation à lancer',
        actionLabel: 'Indexer',
        isActive: false,
      }
    case 'Queued':
      return { label: 'Indexation en attente', isActive: true }
    case 'Running':
      return { label: 'Indexation en cours…', isActive: true }
    case 'Completed':
      return {
        label: `Indexé · ${embedding.indexedPassageCount}/${embedding.passageCount}`,
        isActive: false,
      }
    case 'Failed':
      return {
        label: 'Indexation à reprendre',
        actionLabel: 'Réessayer',
        isActive: false,
      }
  }
}

export function getDocumentProcessingPresentation(
  analysis: DocumentAnalysis,
  classification: DocumentClassification | undefined,
  embedding?: DocumentEmbedding
): DocumentProcessingPresentation {
  const analysisPresentation = getDocumentAnalysisPresentation(analysis)
  if (analysis.status !== 'Completed') {
    return analysisPresentation.actionLabel
      ? { ...analysisPresentation, retryTarget: 'analysis' as const }
      : analysisPresentation
  }

  const classificationPresentation =
    getDocumentClassificationPresentation(classification)
  if (classificationPresentation.label) {
    return classificationPresentation.actionLabel
      ? {
          ...classificationPresentation,
          retryTarget: 'classification' as const,
        }
      : classificationPresentation
  }

  if (!embedding) return analysisPresentation

  const embeddingPresentation = getDocumentEmbeddingPresentation(embedding)
  return embeddingPresentation.actionLabel
    ? { ...embeddingPresentation, retryTarget: 'embedding' as const }
    : embeddingPresentation
}

export function hasActiveDocumentProcessing(
  documents: {
    analysis: DocumentAnalysis
    classification?: DocumentClassification
    embedding?: DocumentEmbedding
  }[]
) {
  return documents.some(
    (document) =>
      getDocumentProcessingPresentation(
        document.analysis,
        document.classification,
        document.embedding
      ).isActive
  )
}

export function normalizeConsultationForm(values: {
  title: string
  buyer: string
  responseDeadline: string
  noticeUrl: string
}): CreateConsultationRequest {
  return {
    title: values.title.trim(),
    buyer: values.buyer.trim(),
    responseDeadline: values.responseDeadline
      ? new Date(values.responseDeadline).toISOString()
      : null,
    noticeUrl: values.noticeUrl.trim() || null,
  }
}
