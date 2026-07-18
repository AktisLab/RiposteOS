import assert from 'node:assert/strict'
import test from 'node:test'
import { buildConsultationListQuery } from './gridify.ts'

const referenceTime = Date.parse('2026-07-17T10:00:00.000Z')

test('builds paginated consultation filters and deterministic requested sort', () => {
  assert.deepEqual(
    buildConsultationListQuery(
      {
        page: 2,
        pageSize: 20,
        filter: ' mairie (Paris) ',
        source: ['boamp', 'manual'],
        deadline: ['open', 'unknown'],
        sort: 'responseDeadline',
        direction: 'asc',
      },
      referenceTime
    ),
    {
      page: 2,
      pageSize: 20,
      filter:
        '(title=*mairie \\(Paris\\)/i|buyer=*mairie \\(Paris\\)/i),(source=boamp/i|source=null),(responseDeadline>=2026-07-17T10:00:00.000Z|responseDeadline=null)',
      orderBy: 'responseDeadline',
    }
  )
})

test('omits empty consultation filters', () => {
  assert.deepEqual(
    buildConsultationListQuery(
      {
        page: 1,
        pageSize: 10,
        filter: ' ',
        source: [],
        deadline: [],
        sort: 'createdAt',
        direction: 'desc',
      },
      referenceTime
    ),
    { page: 1, pageSize: 10, filter: undefined, orderBy: 'createdAt desc' }
  )
})
