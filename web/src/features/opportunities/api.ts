import { apiRequest } from '@/lib/api-client'
import { type SourcingSource } from '@/lib/sourcing-source'
import { type Consultation } from '@/features/consultations/api'
import { type OpportunityListQuery } from './gridify'

export type Opportunity = {
  id: string
  source: string
  sourceId: string
  title: string
  description: string | null
  buyer: string
  procedureType: string | null
  contractNature: string | null
  estimatedValue: number | null
  currency: string | null
  executionDuration: string | null
  matchScore: number
  status: OpportunityStatus
  publicationDate: string
  responseDeadline: string | null
  departmentCodes: string[]
  cpvCodes: string[]
  descriptorLabels: string[]
  matchReasons: string[]
  noticeUrl: string
  documentUrl: string | null
  updatedAt: string
}

export type OpportunityStatus = 'ToQualify' | 'Retained' | 'Dismissed'

export type OpportunityPage = {
  items: Opportunity[]
  totalCount: number
  page: number
  pageSize: number
}

export type ImportRunStatus =
  | 'Queued'
  | 'Running'
  | 'Succeeded'
  | 'PartiallyFailed'
  | 'Failed'

export type ImportRun = {
  id: string
  source: string
  status: ImportRunStatus
  queuedAt: string
  startedAt: string | null
  finishedAt: string | null
  currentPublicationDate: string | null
  fetched: number
  created: number
  changed: number
  unchanged: number
  skipped: number
  errorMessage: string | null
}

export type ImportRunPage = {
  items: ImportRun[]
  totalCount: number
  page: number
  pageSize: number
}

export type ImportRunListQuery = {
  page: number
  pageSize: number
}

export type OpportunitySource = SourcingSource

export const opportunitiesQueryRoot = ['opportunities'] as const
export const importRunsQueryRoot = ['sourcing-imports'] as const

export const opportunitiesQueryKey = (query: OpportunityListQuery) =>
  [...opportunitiesQueryRoot, query] as const

export const getOpportunities = (query: OpportunityListQuery) => {
  const search = new URLSearchParams({
    page: String(query.page),
    pageSize: String(query.pageSize),
  })
  if (query.filter) search.set('filter', query.filter)
  if (query.orderBy) search.set('orderBy', query.orderBy)
  query.departments?.forEach((department) =>
    search.append('departments', department)
  )
  if (query.cpv) search.set('cpv', query.cpv)

  return apiRequest<OpportunityPage>(`/api/opportunities?${search}`, {
    errorMessage: 'Impossible de charger les opportunités.',
  })
}

export const updateOpportunityStatus = (
  id: string,
  status: OpportunityStatus
) =>
  apiRequest<Opportunity>(`/api/opportunities/${id}/status`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ status }),
    errorMessage: "Impossible de modifier le statut de l'opportunité.",
  })

export const promoteOpportunity = (id: string) =>
  apiRequest<Consultation>(`/api/opportunities/${id}/consultation`, {
    method: 'POST',
    errorMessage: "Impossible d'ouvrir l'étude de cette opportunité.",
  })

export const importSource = (source: OpportunitySource) =>
  apiRequest<ImportRun>(`/api/sourcing/${source}/import`, {
    method: 'POST',
    errorMessage: `Impossible de planifier l'import ${source.toUpperCase()}.`,
  })

export const importRunsQueryKey = (query: ImportRunListQuery) =>
  [...importRunsQueryRoot, query] as const

export const getImportRuns = (query: ImportRunListQuery) =>
  apiRequest<ImportRunPage>(
    `/api/sourcing/imports?page=${query.page}&pageSize=${query.pageSize}`,
    { errorMessage: 'Impossible de suivre les synchronisations.' }
  )
