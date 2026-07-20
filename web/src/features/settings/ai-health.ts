type AiProviderHealthTarget = {
  isEnabled: boolean
  healthStatus: 'Unknown' | 'Available' | 'Unavailable'
}

export const aiProviderHealthPresentation = {
  Available: { label: 'En ligne', tone: 'success' },
  Unavailable: { label: 'Indisponible', tone: 'danger' },
  Unknown: { label: 'Vérification en attente', tone: 'muted' },
} as const

export const isAiProviderUnavailable = (provider: AiProviderHealthTarget) =>
  provider.isEnabled && provider.healthStatus === 'Unavailable'
