import { apiRequest, getApiErrorMessage } from '@/lib/api-client'
import { createAssistantEventParser } from './stream'
import {
  type AssistantConversation,
  type AssistantConversationDetails,
  type AssistantStreamEvent,
} from './types'

const assistantQueryRoot = ['consultations', 'assistant'] as const

export const assistantConversationsQueryKey = (consultationId: string) =>
  [...assistantQueryRoot, consultationId, 'conversations'] as const

export const assistantConversationQueryKey = (
  consultationId: string,
  conversationId: string
) =>
  [...assistantConversationsQueryKey(consultationId), conversationId] as const

export const getAssistantConversations = (consultationId: string) =>
  apiRequest<AssistantConversation[]>(
    `/api/consultations/${consultationId}/assistant/conversations`,
    { errorMessage: 'Impossible de charger les conversations.' }
  )

export const getAssistantConversation = (
  consultationId: string,
  conversationId: string
) =>
  apiRequest<AssistantConversationDetails>(
    `/api/consultations/${consultationId}/assistant/conversations/${conversationId}`,
    { errorMessage: 'Impossible de charger la conversation.' }
  )

export const createAssistantConversation = (
  consultationId: string,
  title?: string
) =>
  apiRequest<AssistantConversation>(
    `/api/consultations/${consultationId}/assistant/conversations`,
    {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ title }),
      errorMessage: 'Impossible de créer la conversation.',
    }
  )

export const archiveAssistantConversation = (
  consultationId: string,
  conversationId: string
) =>
  apiRequest<void>(
    `/api/consultations/${consultationId}/assistant/conversations/${conversationId}/archive`,
    { method: 'POST', errorMessage: 'Impossible d’archiver la conversation.' }
  )

export const renameAssistantConversation = (
  consultationId: string,
  conversationId: string,
  title: string
) =>
  apiRequest<void>(
    `/api/consultations/${consultationId}/assistant/conversations/${conversationId}`,
    {
      method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ title }),
      errorMessage: 'Impossible de renommer la conversation.',
    }
  )

export async function streamAssistantMessage(
  consultationId: string,
  conversationId: string,
  content: string,
  signal: AbortSignal,
  onEvent: (event: AssistantStreamEvent) => void
) {
  await readAssistantStream(
    `/api/consultations/${consultationId}/assistant/conversations/${conversationId}/messages`,
    {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ content }),
      signal,
    },
    onEvent
  )
}

export async function retryAssistantMessage(
  consultationId: string,
  conversationId: string,
  userMessageId: string,
  signal: AbortSignal,
  onEvent: (event: AssistantStreamEvent) => void
) {
  await readAssistantStream(
    `/api/consultations/${consultationId}/assistant/conversations/${conversationId}/messages/${userMessageId}/retry`,
    { method: 'POST', signal },
    onEvent
  )
}

async function readAssistantStream(
  url: string,
  init: RequestInit,
  onEvent: (event: AssistantStreamEvent) => void
) {
  const response = await fetch(url, init)
  if (!response.ok || !response.body) {
    throw new Error(
      await getApiErrorMessage(response, 'Impossible d’interroger l’assistant.')
    )
  }

  const reader = response.body.getReader()
  const decoder = new TextDecoder()
  const parser = createAssistantEventParser(onEvent)
  while (true) {
    const { done, value } = await reader.read()
    if (value) parser.push(decoder.decode(value, { stream: !done }))
    if (done) break
  }
  parser.finish()
}
