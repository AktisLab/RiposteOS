export const deadlineFilterValues = ['open', 'closed', 'unknown'] as const
export const opportunitySortFields = [
  'title',
  'buyer',
  'matchScore',
  'status',
  'publicationDate',
  'responseDeadline',
] as const

export type DeadlineFilter = (typeof deadlineFilterValues)[number]
export type OpportunitySortField = (typeof opportunitySortFields)[number]

export type OpportunityListQuery = {
  page: number
  pageSize: number
  filter?: string
  orderBy?: string
  departments?: string[]
  cpv?: string
}

type OpportunityTableSearch = {
  page: number
  pageSize: number
  filter: string
  source: string[]
  deadline: DeadlineFilter[]
  status: string[]
  highRelevance: boolean
  preferredTerritory: boolean
  buyer: string
  department: string
  cpv: string
  sort: OpportunitySortField
  direction: 'asc' | 'desc'
}

export function buildOpportunityListQuery(
  search: OpportunityTableSearch,
  referenceTime: number,
  profile: {
    highRelevanceThreshold: number
    preferredDepartmentCodes: string[]
  }
): OpportunityListQuery {
  const filters: string[] = []
  const term = search.filter.trim()

  if (term) {
    const value = escapeGridifyValue(term)
    filters.push(`(title=*${value}/i|buyer=*${value}/i|sourceId=*${value}/i)`)
  }

  if (search.source.length > 0) {
    filters.push(
      groupWithOr(
        search.source.map((source) => `source=${escapeGridifyValue(source)}/i`)
      )
    )
  }

  if (search.deadline.length > 0) {
    const now = new Date(referenceTime).toISOString()
    const deadlineFilters: Record<DeadlineFilter, string> = {
      open: `responseDeadline>=${now}`,
      closed: `responseDeadline<${now}`,
      unknown: 'responseDeadline=null',
    }
    filters.push(
      groupWithOr(search.deadline.map((status) => deadlineFilters[status]))
    )
  }

  if (search.status.length > 0) {
    filters.push(
      groupWithOr(
        search.status.map((status) => `status=${escapeGridifyValue(status)}/i`)
      )
    )
  }

  if (search.highRelevance) {
    filters.push(`matchScore>=${profile.highRelevanceThreshold}`)
  }

  if (search.buyer.trim()) {
    filters.push(`buyer=*${escapeGridifyValue(search.buyer.trim())}/i`)
  }

  const department = search.department.trim().toUpperCase()
  const departments = [
    ...(search.preferredTerritory ? profile.preferredDepartmentCodes : []),
    ...(department ? [department] : []),
  ].filter((value, index, values) => values.indexOf(value) === index)
  const cpv = search.cpv.trim()

  return {
    page: search.page,
    pageSize: search.pageSize,
    filter: filters.length > 0 ? filters.join(',') : undefined,
    orderBy: `${search.sort}${search.direction === 'desc' ? ' desc' : ''}`,
    departments: departments.length > 0 ? departments : undefined,
    cpv: cpv || undefined,
  }
}

function groupWithOr(filters: string[]) {
  return filters.length === 1 ? filters[0] : `(${filters.join('|')})`
}

function escapeGridifyValue(value: string) {
  return value.replace(/([(),|\\]|\/i)/g, '\\$1')
}
