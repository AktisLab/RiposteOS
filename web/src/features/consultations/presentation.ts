import type {
  DocumentAnalysis,
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
        label: `Analysé · ${analysis.pageCount} pages · ${analysis.passageCount} passages`,
        isActive: false,
      }
    case 'Failed':
      return {
        label: analysis.errorMessage || 'L’analyse a échoué.',
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

export function hasActiveDocumentAnalysis(
  documents: {
    analysis: DocumentAnalysis
  }[]
) {
  return documents.some(
    (document) => getDocumentAnalysisPresentation(document.analysis).isActive
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
