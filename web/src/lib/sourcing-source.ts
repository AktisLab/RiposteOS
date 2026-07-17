export const sourcingSources = [
  {
    value: 'boamp',
    label: 'BOAMP',
    description: 'Avis de marchés publics français',
  },
  {
    value: 'ted',
    label: 'TED',
    description: 'Avis de marchés publics européens',
  },
  {
    value: 'place',
    label: 'PLACE',
    description: 'Consultations de l’État et de ses établissements publics',
  },
] as const

export type SourcingSource = (typeof sourcingSources)[number]['value']

export function findSourcingSource(source: string) {
  const normalizedSource = source.toLocaleLowerCase('fr')
  return sourcingSources.find((item) => item.value === normalizedSource) ?? null
}
