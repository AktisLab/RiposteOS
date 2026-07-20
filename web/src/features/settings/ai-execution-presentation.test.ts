import assert from 'node:assert/strict'
import test from 'node:test'
import {
  buildAiExecutionFilter,
  formatAiExecutionDuration,
  formatAiExecutionPayload,
  parseDocumentAnalysisInput,
} from './ai-execution-presentation.ts'

test('buildAiExecutionFilter combines only active filters', () => {
  assert.equal(buildAiExecutionFilter('all', 'all'), undefined)
  assert.equal(
    buildAiExecutionFilter('DocumentClassification', 'Failed'),
    'operation=DocumentClassification,status=Failed'
  )
})

test('formatAiExecutionPayload indents JSON and keeps legacy text readable', () => {
  assert.equal(
    formatAiExecutionPayload('{"text":"réponse"}'),
    '{\n  "text": "réponse"\n}'
  )
  assert.equal(formatAiExecutionPayload('ancienne réponse'), 'ancienne réponse')
})

test('parseDocumentAnalysisInput presents the document descriptor and rejects other payloads', () => {
  assert.deepEqual(
    parseDocumentAnalysisInput(
      '{"Id":"document-id","Size":65061,"Sha256":"abc","ContentType":"application/vnd.openxmlformats-officedocument.wordprocessingml.document","OriginalFileName":"DC1.docx"}'
    ),
    {
      id: 'document-id',
      fileName: 'DC1.docx',
      contentType:
        'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
      size: '65 061 o',
      sha256: 'abc',
    }
  )
  assert.equal(parseDocumentAnalysisInput('{"text":"réponse"}'), null)
})

test('formatAiExecutionDuration formats a completed execution', () => {
  assert.equal(
    formatAiExecutionDuration(
      '2026-07-20T10:00:00.000Z',
      '2026-07-20T10:01:08.000Z',
      null
    ),
    '1 min 8 s'
  )
  assert.equal(
    formatAiExecutionDuration('2026-07-20T10:00:00.000Z', null, null),
    null
  )
})
