import assert from 'node:assert/strict'
import test from 'node:test'
import {
  createRuntimeMessages,
  startAssistantRetry,
  startAssistantStream,
  updateAssistantStream,
} from './runtime.ts'
import { createAssistantEventParser } from './stream.ts'
import { type AssistantStreamEvent } from './types.ts'

const event = (
  values: Partial<AssistantStreamEvent> & Pick<AssistantStreamEvent, 'type'>
): AssistantStreamEvent => ({
  messageId: null,
  delta: null,
  error: null,
  message: null,
  activity: null,
  ...values,
})

test('le message assistant reste en cours pendant les deltas SSE', () => {
  let stream = startAssistantStream('Quels sont les livrables ?')
  stream = updateAssistantStream(
    stream,
    event({ type: 'message_started', messageId: 'assistant-1' })
  )
  stream = updateAssistantStream(
    stream,
    event({ type: 'reasoning_delta', delta: 'Je recherche les preuves.' })
  )
  stream = updateAssistantStream(
    stream,
    event({ type: 'answer_delta', delta: '**Trois** livrables' })
  )

  const messages = createRuntimeMessages([], stream)
  assert.equal(messages[0]?.role, 'user')
  assert.equal(messages[1]?.role, 'assistant')
  assert.deepEqual(messages[1]?.status, { type: 'running' })
  const reasoning = messages[1]?.content[0]
  assert.equal(
    typeof reasoning === 'object' && reasoning?.type === 'reasoning'
      ? reasoning.text
      : null,
    'Je recherche les preuves.'
  )
  const content = messages[1]?.content[1]
  assert.equal(
    typeof content === 'object' && content?.type === 'text'
      ? content.text
      : null,
    '**Trois** livrables'
  )
})

test('le parseur SSE conserve un événement découpé entre deux chunks', () => {
  const received: AssistantStreamEvent[] = []
  const parser = createAssistantEventParser((item) => received.push(item))

  parser.push('event: answer_delta\ndata: {"type":"answer_')
  parser.push(
    'delta","messageId":"a1","delta":"Bonjour","error":null,"message":null,"activity":null}\n\n'
  )
  parser.finish()

  assert.equal(received.length, 1)
  assert.equal(received[0]?.type, 'answer_delta')
  assert.equal(received[0]?.delta, 'Bonjour')
})

test('un message assistant en échec reste visible et réessayable', () => {
  const messages = createRuntimeMessages(
    [
      {
        id: 'user-1',
        role: 0,
        content: 'Quel produit faut-il réaliser ?',
        status: 1,
        errorMessage: null,
        createdAt: '2026-07-21T08:34:33Z',
        completedAt: '2026-07-21T08:34:33Z',
        failedAt: null,
        providerName: null,
        model: null,
        evidence: [],
        details: null,
      },
      {
        id: 'assistant-1',
        role: 1,
        content: null,
        status: 2,
        errorMessage: 'La réponse n’a pas pu être vérifiée.',
        createdAt: '2026-07-21T08:34:34Z',
        completedAt: null,
        failedAt: '2026-07-21T08:34:42Z',
        providerName: null,
        model: null,
        evidence: [],
        details: null,
      },
    ],
    startAssistantRetry()
  )

  assert.equal(messages[1]?.id, 'assistant-1')
  assert.deepEqual(messages[1]?.status, {
    type: 'incomplete',
    reason: 'error',
    error: 'La réponse n’a pas pu être vérifiée.',
  })
  assert.equal(messages[2]?.role, 'assistant')
  assert.deepEqual(messages[2]?.status, { type: 'running' })
})
