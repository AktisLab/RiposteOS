import { findCpvLabel, type CpvCatalogEntry } from '../../lib/cpv-catalog.ts'

const cpvReasonPattern = /^(CPV (ciblé|surveillé|exclu)) : (\d{2,8})$/

export function presentMatchReason(
  reason: string,
  cpvEntries: readonly CpvCatalogEntry[] = []
) {
  const [weight = '', ...labelParts] = reason.split(' ')
  const label = labelParts.join(' ')
  const cpvMatch = cpvReasonPattern.exec(label)

  if (!cpvMatch) {
    return { weight, label }
  }

  const [, , kind, code] = cpvMatch
  const cpvLabel = findCpvLabel(cpvEntries, code)

  return {
    weight,
    label: kind === 'exclu' ? 'CPV pénalisé' : `CPV ${kind}`,
    detail: cpvLabel ? `${code} · ${cpvLabel}` : code,
  }
}
