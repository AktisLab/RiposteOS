import { apiRequest } from '@/lib/api-client'

export type SourcingSettings = {
  keywords: string[]
  excludedKeywords: string[]
  positiveSignals: string[]
  negativeSignals: string[]
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
