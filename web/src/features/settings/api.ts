import { apiRequest } from '@/lib/api-client'

export type SourcingSettings = {
  keywords: string[]
  excludedKeywords: string[]
  positiveSignals: string[]
  negativeSignals: string[]
  allowedCountryCodes: string[]
  preferredDepartmentCodes: string[]
  cpvWhitelistPrefixes: string[]
  cpvWatchPrefixes: string[]
  cpvExcludedPrefixes: string[]
  pageSize: number
  positiveSignalWeight: number
  negativeSignalPenalty: number
  preferredDepartmentBoost: number
  cpvWhitelistBoost: number
  cpvWatchBoost: number
  cpvExclusionPenalty: number
  urgentDeadlineDays: number
  urgentDeadlinePenalty: number
  highRelevanceThreshold: number
  boampCron: string
  tedCron: string
  placeCron: string
  updatedAt: string
}

export const sourcingSettingsQueryKey = ['sourcing-settings'] as const

export const getSourcingSettings = () =>
  apiRequest<SourcingSettings | null>('/api/sourcing/settings', {
    errorMessage: 'Impossible de charger le profil de sourcing.',
  })

export const updateSourcingSettings = (
  settings: Omit<SourcingSettings, 'updatedAt'>
) =>
  apiRequest<SourcingSettings>('/api/sourcing/settings', {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(settings),
    errorMessage: "L'enregistrement du profil a échoué.",
  })

export type AiProviderProtocol = 'OpenAiCompatible'
export type AiProviderHealthStatus = 'Unknown' | 'Available' | 'Unavailable'

export type AiProvider = {
  id: string
  name: string
  protocol: AiProviderProtocol
  baseUrl: string
  model: string
  apiKeyEnvironmentVariableName: string | null
  isEnabled: boolean
  healthStatus: AiProviderHealthStatus
  healthCheckedAt: string | null
  createdAt: string
  updatedAt: string
}

export type AiProviderRequest = Omit<
  AiProvider,
  'id' | 'healthStatus' | 'healthCheckedAt' | 'createdAt' | 'updatedAt'
>

export type AiTaskAssignment = {
  task: 'DocumentClassification'
  providerId: string
  updatedAt: string
}

export type AiExecutionOperation = 'DocumentAnalysis' | 'DocumentClassification'

export type AiExecutionStatus =
  | 'Running'
  | 'Completed'
  | 'Failed'
  | 'NotConfigured'

export type AiExecutionLog = {
  id: string
  operation: AiExecutionOperation
  status: AiExecutionStatus
  subjectKind: string
  subjectId: string
  subjectLabel: string
  correlationId: string | null
  providerId: string | null
  providerName: string | null
  model: string | null
  startedAt: string
  completedAt: string | null
  failedAt: string | null
  errorMessage: string | null
}

export type AiExecutionLogList = {
  items: AiExecutionLog[]
  totalCount: number
  page: number
  pageSize: number
}

export type AiExecutionLogDetails = {
  execution: AiExecutionLog
  input: string | null
  output: string | null
}

export type AiExecutionLogListQuery = {
  page: number
  pageSize: number
  filter?: string
}

export const aiProvidersQueryKey = ['ai-providers'] as const
export const documentClassificationAssignmentQueryKey = [
  'ai-task-assignment',
  'DocumentClassification',
] as const

export const aiExecutionLogsQueryKey = (query: AiExecutionLogListQuery) =>
  ['ai-execution-logs', query] as const

export const aiExecutionLogDetailsQueryKey = (id: string) =>
  ['ai-execution-log-details', id] as const

export const getAiProviders = () =>
  apiRequest<AiProvider[]>('/api/settings/ai/providers', {
    errorMessage: 'Impossible de charger les fournisseurs IA.',
  })

export const createAiProvider = (provider: AiProviderRequest) =>
  apiRequest<AiProvider>('/api/settings/ai/providers', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(provider),
    errorMessage: 'Impossible d’ajouter le fournisseur IA.',
  })

export const updateAiProvider = (id: string, provider: AiProviderRequest) =>
  apiRequest<AiProvider>(`/api/settings/ai/providers/${id}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(provider),
    errorMessage: 'Impossible de modifier le fournisseur IA.',
  })

export const deleteAiProvider = (id: string) =>
  apiRequest<void>(`/api/settings/ai/providers/${id}`, {
    method: 'DELETE',
    errorMessage:
      'Ce fournisseur ne peut pas être supprimé tant qu’il est utilisé.',
  })

export const testAiProviderConnection = (id: string) =>
  apiRequest<void>(`/api/settings/ai/providers/${id}/test`, {
    method: 'POST',
    errorMessage: 'Impossible de joindre ce fournisseur IA.',
  })

export const getDocumentClassificationAssignment = () =>
  apiRequest<AiTaskAssignment>(
    '/api/settings/ai/tasks/DocumentClassification',
    {
      allowNotFound: true,
      errorMessage: 'Impossible de charger l’affectation de classement.',
    }
  )

export const assignDocumentClassificationProvider = (providerId: string) =>
  apiRequest<void>('/api/settings/ai/tasks/DocumentClassification', {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ providerId }),
    errorMessage: 'Impossible d’affecter le fournisseur au classement.',
  })

export const getAiExecutionLogs = (query: AiExecutionLogListQuery) => {
  const params = new URLSearchParams({
    page: String(query.page),
    pageSize: String(query.pageSize),
  })
  if (query.filter) {
    params.set('filter', query.filter)
  }

  return apiRequest<AiExecutionLogList>(
    `/api/settings/ai/executions?${params.toString()}`,
    { errorMessage: 'Impossible de charger le journal IA.' }
  )
}

export const getAiExecutionLogDetails = (id: string) =>
  apiRequest<AiExecutionLogDetails>(`/api/settings/ai/executions/${id}`, {
    errorMessage: 'Impossible de charger le détail de l’exécution IA.',
  })
