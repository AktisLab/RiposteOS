export type CpvCatalogEntry = readonly [code: string, label: string]

export type CpvCatalog = {
  source: string
  version: string
  language: string
  items: CpvCatalogEntry[]
}

export const cpvCatalogQueryKey = ['cpv-catalog', '2008', 'fr'] as const

export async function loadCpvCatalog() {
  return (await import('../data/cpv-2008-fr.json'))
    .default as unknown as CpvCatalog
}

export function toCpvPrefix(code: string) {
  const digits = code.replace(/\D/g, '').slice(0, 8)
  const prefix = digits.replace(/0+$/, '')

  return prefix.length >= 2 ? prefix : digits.slice(0, 2)
}

export function addCpvCode(prefixes: readonly string[], code: string) {
  const prefix = toCpvPrefix(code)

  if (prefixes.some((existing) => prefix.startsWith(existing))) {
    return [...prefixes]
  }

  return [
    ...prefixes.filter((existing) => !existing.startsWith(prefix)),
    prefix,
  ]
}

export function findCpvLabel(
  entries: readonly CpvCatalogEntry[],
  prefix: string
) {
  const paddedCode = prefix.padEnd(8, '0')

  return (
    entries.find(([code]) => code === paddedCode)?.[1] ??
    entries.find(([code]) => code.startsWith(prefix))?.[1]
  )
}

export function findCpvMatches(
  entries: readonly CpvCatalogEntry[],
  query: string,
  limit = 20
) {
  const terms = normalize(query).split(/\s+/).filter(Boolean)

  if (terms.join('').length < 2) {
    return []
  }

  return entries
    .filter(([code, label]) => {
      const searchable = `${code} ${normalize(label)}`
      return terms.every((term) => searchable.includes(term))
    })
    .slice(0, limit)
}

function normalize(value: string) {
  return value
    .normalize('NFD')
    .replace(/\p{Diacritic}/gu, '')
    .toLocaleLowerCase('fr')
    .trim()
}
