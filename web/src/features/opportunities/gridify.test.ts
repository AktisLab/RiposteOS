import assert from 'node:assert/strict'
import test from 'node:test'
import { buildOpportunityListQuery } from './gridify.ts'

test('builds an escaped, filtered and sorted Gridify query', () => {
  const query = buildOpportunityListQuery(
    {
      page: 2,
      pageSize: 20,
      filter: 'portail, API/i',
      source: ['boamp'],
      deadline: ['open', 'unknown'],
      status: ['ToQualify'],
      highRelevance: true,
      preferredTerritory: true,
      buyer: 'Lyon',
      department: '',
      cpv: '722',
      sort: 'responseDeadline',
      direction: 'asc',
    },
    Date.UTC(2026, 6, 15, 10),
    {
      highRelevanceThreshold: 35,
      preferredDepartmentCodes: ['69', '38'],
    }
  )

  assert.deepEqual(query, {
    page: 2,
    pageSize: 20,
    filter:
      '(title=*portail\\, API\\/i/i|buyer=*portail\\, API\\/i/i|sourceId=*portail\\, API\\/i/i),source=boamp/i,(responseDeadline>=2026-07-15T10:00:00.000Z|responseDeadline=null),status=ToQualify/i,matchScore>=35,buyer=*Lyon/i',
    orderBy: 'responseDeadline',
    departments: ['69', '38'],
    cpv: '722',
  })
})

test('omits empty filters and keeps descending order explicit', () => {
  const query = buildOpportunityListQuery(
    {
      page: 1,
      pageSize: 50,
      filter: '   ',
      source: [],
      deadline: [],
      status: [],
      highRelevance: false,
      preferredTerritory: false,
      buyer: '',
      department: '',
      cpv: '',
      sort: 'matchScore',
      direction: 'desc',
    },
    0,
    { highRelevanceThreshold: 35, preferredDepartmentCodes: [] }
  )

  assert.equal(query.filter, undefined)
  assert.equal(query.orderBy, 'matchScore desc')
})
