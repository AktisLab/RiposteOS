import assert from 'node:assert/strict'
import test from 'node:test'
import {
  aiProviderHealthPresentation,
  isAiProviderUnavailable,
} from './ai-health.ts'

type HealthStatus = 'Unknown' | 'Available' | 'Unavailable'

const provider = (healthStatus: HealthStatus) => ({
  id: 'provider-id',
  name: 'Serveur local',
  protocol: 'OpenAiCompatible',
  baseUrl: 'http://localhost:11434/v1/',
  model: 'gpt-oss:20b',
  apiKeyEnvironmentVariableName: null,
  hasStoredApiKey: false,
  isEnabled: true,
  healthStatus,
  healthCheckedAt: null,
  createdAt: '2026-07-20T10:00:00Z',
  updatedAt: '2026-07-20T10:00:00Z',
})

test('presents every AI provider health state', () => {
  assert.deepEqual(aiProviderHealthPresentation.Available, {
    label: 'En ligne',
    tone: 'success',
  })
  assert.deepEqual(aiProviderHealthPresentation.Unavailable, {
    label: 'Indisponible',
    tone: 'danger',
  })
  assert.deepEqual(aiProviderHealthPresentation.Unknown, {
    label: 'Vérification en attente',
    tone: 'muted',
  })
})

test('warns only for an enabled unavailable provider', () => {
  assert.equal(isAiProviderUnavailable(provider('Unavailable')), true)
  assert.equal(isAiProviderUnavailable(provider('Available')), false)
  assert.equal(
    isAiProviderUnavailable({ ...provider('Unavailable'), isEnabled: false }),
    false
  )
})
