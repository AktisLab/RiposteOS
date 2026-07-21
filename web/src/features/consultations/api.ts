import { apiRequest } from '@/lib/api-client'
import { type ConsultationListQuery } from './gridify'

export const consultationDocumentKinds = [
  'FullDce',
  'ConsultationRules',
  'TechnicalSpecifications',
  'AdministrativeSpecifications',
  'CommitmentAct',
  'Pricing',
  'Appendix',
  'Other',
] as const

export type ConsultationDocumentKind =
  (typeof consultationDocumentKinds)[number]

export type Consultation = {
  id: string
  opportunityId: string | null
  title: string
  buyer: string
  responseDeadline: string | null
  noticeUrl: string | null
  source: string | null
  sourceId: string | null
  documentCount: number
  createdAt: string
  updatedAt: string
}

export type ConsultationPage = {
  items: Consultation[]
  totalCount: number
  page: number
  pageSize: number
}

export type ConsultationDocument = {
  id: string
  originalFileName: string
  contentType: string
  size: number
  createdAt: string
  kind: ConsultationDocumentKind
  kindOrigin: 'Automatic' | 'Manual'
  addedAt: string
  downloadUrl: string
  analysis: DocumentAnalysis
  classification: DocumentClassification
  embedding: DocumentEmbedding
}

export type DocumentAnalysisStatus =
  | 'NotStarted'
  | 'NotSupported'
  | 'Queued'
  | 'Running'
  | 'Completed'
  | 'Failed'

export type DocumentAnalysis = {
  status: DocumentAnalysisStatus
  queuedAt: string | null
  startedAt: string | null
  completedAt: string | null
  failedAt: string | null
  pageCount: number
  passageCount: number
  errorMessage: string | null
}

export type DocumentClassificationStatus =
  | 'NotStarted'
  | 'Queued'
  | 'Running'
  | 'Completed'
  | 'Failed'
  | 'NotConfigured'

export type DocumentClassification = {
  status: DocumentClassificationStatus
  proposedKind: ConsultationDocumentKind | null
  confidence: 'High' | 'Medium' | 'Low' | null
  queuedAt: string | null
  startedAt: string | null
  completedAt: string | null
  failedAt: string | null
  providerName: string | null
  model: string | null
  errorMessage: string | null
}

export type DocumentEmbeddingStatus =
  | 'NotStarted'
  | 'Queued'
  | 'Running'
  | 'Completed'
  | 'Failed'

export type DocumentEmbedding = {
  status: DocumentEmbeddingStatus
  indexedPassageCount: number
  passageCount: number
  errorMessage: string | null
}

export type DocumentAnalysisPassage = {
  ordinal: number
  text: string
  pageNumber: number | null
  sectionTitle: string | null
  sourceLocation: string | null
}

export type DocumentUploadResponse = {
  id: string
  originalFileName: string
  contentType: string
  size: number
  createdAt: string
}

export type CreateConsultationRequest = {
  title: string
  buyer: string
  responseDeadline: string | null
  noticeUrl: string | null
}

export const consultationsQueryRoot = ['consultations'] as const

export const consultationsQueryKey = (query: ConsultationListQuery) =>
  [...consultationsQueryRoot, 'list', query] as const

export const consultationQueryKey = (id: string) =>
  [...consultationsQueryRoot, 'detail', id] as const

export const consultationDocumentsQueryKey = (id: string) =>
  [...consultationsQueryRoot, 'documents', id] as const

export function getConsultations(query: ConsultationListQuery) {
  const search = new URLSearchParams({
    page: String(query.page),
    pageSize: String(query.pageSize),
  })
  if (query.filter) search.set('filter', query.filter)
  if (query.orderBy) search.set('orderBy', query.orderBy)

  return apiRequest<ConsultationPage>(`/api/consultations?${search}`, {
    errorMessage: 'Impossible de charger les consultations.',
  })
}

export const getConsultation = (id: string) =>
  apiRequest<Consultation>(`/api/consultations/${id}`, {
    errorMessage: 'Impossible de charger la consultation.',
  })

export const createConsultation = (request: CreateConsultationRequest) =>
  apiRequest<Consultation>('/api/consultations', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
    errorMessage: 'Impossible de créer la consultation.',
  })

export const getConsultationDocuments = (consultationId: string) =>
  apiRequest<ConsultationDocument[]>(
    `/api/consultations/${consultationId}/documents`,
    { errorMessage: 'Impossible de charger les documents.' }
  )

export const uploadDocument = (file: File) => {
  const form = new FormData()
  form.set('file', file)
  return apiRequest<DocumentUploadResponse>('/api/documents', {
    method: 'POST',
    body: form,
    errorMessage: 'Impossible de téléverser le document.',
  })
}

export const attachDocument = (consultationId: string, documentId: string) =>
  apiRequest<ConsultationDocument>(
    `/api/consultations/${consultationId}/documents`,
    {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ documentId }),
      errorMessage: 'Impossible de rattacher le document à la consultation.',
    }
  )

export const updateConsultationDocumentKind = (
  consultationId: string,
  documentId: string,
  kind: ConsultationDocumentKind
) =>
  apiRequest<ConsultationDocument>(
    `/api/consultations/${consultationId}/documents/${documentId}`,
    {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ kind }),
      errorMessage: 'Impossible de modifier la catégorie du document.',
    }
  )

export const detachConsultationDocument = (
  consultationId: string,
  documentId: string
) =>
  apiRequest<void>(
    `/api/consultations/${consultationId}/documents/${documentId}`,
    {
      method: 'DELETE',
      errorMessage: 'Impossible de détacher le document.',
    }
  )

export const requestDocumentAnalysis = (
  consultationId: string,
  documentId: string
) =>
  apiRequest<ConsultationDocument>(
    `/api/consultations/${consultationId}/documents/${documentId}/analysis`,
    {
      method: 'POST',
      errorMessage: 'Impossible de relancer l’analyse du document.',
    }
  )

export const retryDocumentClassification = (
  consultationId: string,
  documentId: string
) =>
  apiRequest<ConsultationDocument>(
    `/api/consultations/${consultationId}/documents/${documentId}/classification`,
    {
      method: 'POST',
      errorMessage: 'Impossible de relancer le classement du document.',
    }
  )

export const retryDocumentEmbedding = (
  consultationId: string,
  documentId: string
) =>
  apiRequest<ConsultationDocument>(
    `/api/consultations/${consultationId}/documents/${documentId}/embedding`,
    {
      method: 'POST',
      errorMessage: 'Impossible de relancer l’indexation du document.',
    }
  )

export const documentAnalysisPassagesQueryKey = (
  consultationId: string,
  documentId: string
) =>
  [
    ...consultationsQueryRoot,
    'documents',
    consultationId,
    documentId,
    'passages',
  ] as const

export const getDocumentAnalysisPassages = (
  consultationId: string,
  documentId: string
) =>
  apiRequest<DocumentAnalysisPassage[]>(
    `/api/consultations/${consultationId}/documents/${documentId}/analysis/passages`,
    { errorMessage: 'Impossible de charger le détail de l’analyse.' }
  )
