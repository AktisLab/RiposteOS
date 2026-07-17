const amountFormatter = new Intl.NumberFormat('fr-FR', {
  maximumFractionDigits: 0,
})

const procedureLabels: Record<string, string> = {
  open: 'Procédure ouverte',
  restricted: 'Procédure restreinte',
  'neg-w-call': 'Procédure négociée avec publication',
  'neg-wo-call': 'Procédure négociée sans publication',
  'comp-dial': 'Dialogue compétitif',
  innovation: 'Partenariat d’innovation',
}

const contractNatureLabels: Record<string, string> = {
  services: 'Services',
  supplies: 'Fournitures',
  works: 'Travaux',
}

export function formatEstimatedValue(
  value: number | null | undefined,
  currency: string | null | undefined
) {
  if (value == null) return null

  const amount = amountFormatter.format(value)
  const normalizedCurrency = currency?.trim().toUpperCase()
  return normalizedCurrency ? `${amount} ${normalizedCurrency}` : amount
}

export function presentProcedureType(value: string | null | undefined) {
  if (!value) return null
  return procedureLabels[value.trim().toLowerCase()] ?? value.trim()
}

export function presentContractNature(value: string | null | undefined) {
  if (!value) return null
  return contractNatureLabels[value.trim().toLowerCase()] ?? value.trim()
}

export function presentExecutionDuration(value: string | null | undefined) {
  if (!value) return null

  const normalized = value.trim()
  const match = /^(\d+(?:[.,]\d+)?)\s+(DAY|WEEK|MONTH|YEAR)S?$/i.exec(
    normalized
  )
  if (!match) return normalized

  const amount = match[1].replace('.', ',')
  const singular = Number(match[1].replace(',', '.')) === 1
  const unit = {
    DAY: singular ? 'jour' : 'jours',
    WEEK: singular ? 'semaine' : 'semaines',
    MONTH: 'mois',
    YEAR: singular ? 'an' : 'ans',
  }[match[2].toUpperCase()]

  return `${amount} ${unit}`
}
