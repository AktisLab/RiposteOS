export const consultationDeadlineFilterValues = [
  'open',
  'closed',
  'unknown',
] as const
export const consultationSortFields = [
  'title',
  'buyer',
  'source',
  'responseDeadline',
  'createdAt',
] as const

export type ConsultationDeadlineFilter =
  (typeof consultationDeadlineFilterValues)[number]
export type ConsultationSortField = (typeof consultationSortFields)[number]

export type ConsultationListQuery = {
  page: number
  pageSize: number
  filter?: string
  orderBy?: string
}

export type ConsultationTableSearch = {
  page: number
  pageSize: number
  filter: string
  source: string[]
  deadline: ConsultationDeadlineFilter[]
  sort: ConsultationSortField
  direction: 'asc' | 'desc'
}

export function buildConsultationListQuery(
  search: ConsultationTableSearch,
  referenceTime: number
): ConsultationListQuery {
  const filters: string[] = []
  const term = search.filter.trim()

  if (term) {
    const value = escapeGridifyValue(term)
    filters.push(`(title=*${value}/i|buyer=*${value}/i)`)
  }

  if (search.source.length > 0) {
    filters.push(
      groupWithOr(
        search.source.map((source) =>
          source === 'manual'
            ? 'source=null'
            : `source=${escapeGridifyValue(source)}/i`
        )
      )
    )
  }

  if (search.deadline.length > 0) {
    const now = new Date(referenceTime).toISOString()
    const deadlineFilters: Record<ConsultationDeadlineFilter, string> = {
      open: `responseDeadline>=${now}`,
      closed: `responseDeadline<${now}`,
      unknown: 'responseDeadline=null',
    }
    filters.push(
      groupWithOr(search.deadline.map((value) => deadlineFilters[value]))
    )
  }

  return {
    page: search.page,
    pageSize: search.pageSize,
    filter: filters.length > 0 ? filters.join(',') : undefined,
    orderBy: `${search.sort}${search.direction === 'desc' ? ' desc' : ''}`,
  }
}

function groupWithOr(filters: string[]) {
  return filters.length === 1 ? filters[0] : `(${filters.join('|')})`
}

function escapeGridifyValue(value: string) {
  return value.replace(/([(),|\\]|\/i)/g, '\\$1')
}
