import type { AiExecutionOperation, AiExecutionStatus } from './api'

export const aiExecutionOperationPresentation: Record<
  AiExecutionOperation,
  string
> = {
  DocumentAnalysis: 'Analyse documentaire',
  DocumentClassification: 'Classement',
}

export const aiExecutionStatusPresentation: Record<
  AiExecutionStatus,
  { label: string; className: string }
> = {
  Running: { label: 'En cours', className: 'text-muted-foreground' },
  Completed: {
    label: 'Terminé',
    className: 'text-emerald-700 dark:text-emerald-400',
  },
  Failed: { label: 'Échec', className: 'text-destructive' },
  NotConfigured: {
    label: 'Non configuré',
    className: 'text-amber-700 dark:text-amber-400',
  },
}

export function formatAiExecutionDuration(
  startedAt: string,
  completedAt: string | null,
  failedAt: string | null
) {
  const endedAt = completedAt ?? failedAt
  if (!endedAt) return null

  const milliseconds =
    new Date(endedAt).getTime() - new Date(startedAt).getTime()
  if (milliseconds < 1_000) return '< 1 s'
  if (milliseconds < 60_000) return `${Math.round(milliseconds / 1_000)} s`
  return `${Math.floor(milliseconds / 60_000)} min ${Math.round((milliseconds % 60_000) / 1_000)} s`
}

export function buildAiExecutionFilter(
  operation: AiExecutionOperation | 'all',
  status: AiExecutionStatus | 'all'
) {
  const filters = [
    operation === 'all' ? null : `operation=${operation}`,
    status === 'all' ? null : `status=${status}`,
  ].filter((value): value is string => value !== null)

  return filters.length > 0 ? filters.join(',') : undefined
}

export function formatAiExecutionPayload(payload: string) {
  try {
    return JSON.stringify(JSON.parse(payload), null, 2)
  } catch {
    return payload
  }
}

export function parseDocumentAnalysisInput(payload: string) {
  try {
    const value: unknown = JSON.parse(payload)
    if (
      typeof value !== 'object' ||
      value === null ||
      Array.isArray(value) ||
      !('Id' in value) ||
      !('Size' in value) ||
      !('Sha256' in value) ||
      !('ContentType' in value) ||
      !('OriginalFileName' in value) ||
      typeof value.Id !== 'string' ||
      typeof value.Size !== 'number' ||
      typeof value.Sha256 !== 'string' ||
      typeof value.ContentType !== 'string' ||
      typeof value.OriginalFileName !== 'string'
    ) {
      return null
    }

    return {
      id: value.Id,
      fileName: value.OriginalFileName,
      contentType: value.ContentType,
      size: new Intl.NumberFormat('fr-FR', {
        style: 'unit',
        unit: 'byte',
        unitDisplay: 'short',
      }).format(value.Size),
      sha256: value.Sha256,
    }
  } catch {
    return null
  }
}
