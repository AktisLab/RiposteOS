import {
  type AssistantAnswerDetails,
  type AssistantEvidence,
  type AssistantMessage,
  type AssistantStreamEvent,
} from './types.ts'

type AssistantRuntimeMessage = {
  id: string
  role: 'user' | 'assistant'
  content: (
    | { type: 'text'; text: string }
    | { type: 'reasoning'; text: string }
  )[]
  createdAt?: Date
  status?:
    | { type: 'running' }
    | { type: 'complete'; reason: 'stop' }
    | {
        type: 'incomplete'
        reason: 'cancelled' | 'error'
        error?: string
      }
  metadata?: {
    isOptimistic?: boolean
    custom?: AssistantMessageMetadata
  }
}

export type AssistantMessageMetadata = {
  evidence: AssistantEvidence[]
  details: AssistantAnswerDetails | null
  providerName: string | null
  model: string | null
}

export type AssistantStreamState = {
  isRunning: boolean
  pendingUser: string | null
  assistantId: string | null
  assistantReasoning: string
  assistantContent: string
  activity: string | null
  completedMessage: AssistantMessage | null
}

export const idleAssistantStream: AssistantStreamState = {
  isRunning: false,
  pendingUser: null,
  assistantId: null,
  assistantReasoning: '',
  assistantContent: '',
  activity: null,
  completedMessage: null,
}

export const startAssistantStream = (
  content: string
): AssistantStreamState => ({
  ...idleAssistantStream,
  isRunning: true,
  pendingUser: content,
  assistantId: 'pending-assistant',
  activity: 'Recherche dans les passages indexés du DCE…',
})

export const startAssistantRetry = (): AssistantStreamState => ({
  ...idleAssistantStream,
  isRunning: true,
  assistantId: 'pending-assistant',
  activity: 'Nouvelle recherche dans le DCE…',
})

export function updateAssistantStream(
  state: AssistantStreamState,
  event: AssistantStreamEvent
): AssistantStreamState {
  switch (event.type) {
    case 'message_started':
      return event.messageId
        ? { ...state, assistantId: event.messageId }
        : state
    case 'answer_delta':
      return event.delta
        ? { ...state, assistantContent: state.assistantContent + event.delta }
        : state
    case 'reasoning_delta':
      return event.delta
        ? {
            ...state,
            assistantReasoning: state.assistantReasoning + event.delta,
          }
        : state
    case 'activity':
      return event.activity ? { ...state, activity: event.activity } : state
    case 'message_completed':
      return event.message
        ? {
            ...idleAssistantStream,
            completedMessage: event.message,
          }
        : state
    case 'message_failed':
    case 'message_cancelled':
      return idleAssistantStream
    default:
      return state
  }
}

function convertPersistedMessage(
  message: AssistantMessage
): AssistantRuntimeMessage {
  const role = message.role === 0 ? 'user' : 'assistant'
  const status =
    role === 'assistant'
      ? message.status === 1
        ? ({ type: 'complete', reason: 'stop' } as const)
        : ({
            type: 'incomplete',
            reason: message.status === 3 ? 'cancelled' : 'error',
            error: message.errorMessage ?? undefined,
          } as const)
      : undefined
  return {
    id: message.id,
    role,
    content: [
      ...(role === 'assistant' && message.details?.reasoningSummary
        ? [
            {
              type: 'reasoning' as const,
              text: message.details.reasoningSummary,
            },
          ]
        : []),
      { type: 'text', text: message.content ?? '' },
    ],
    createdAt: new Date(message.createdAt),
    status,
    metadata: {
      custom: {
        evidence: message.evidence,
        details: message.details,
        providerName: message.providerName,
        model: message.model,
      } satisfies AssistantMessageMetadata,
    },
  }
}

export function createRuntimeMessages(
  messages: AssistantMessage[],
  stream: AssistantStreamState
): AssistantRuntimeMessage[] {
  const persisted = [
    ...messages,
    ...(stream.completedMessage &&
    !messages.some((message) => message.id === stream.completedMessage?.id)
      ? [stream.completedMessage]
      : []),
  ]
    .filter(
      (message) =>
        message.role === 0 || message.status !== 0 || Boolean(message.content)
    )
    .map(convertPersistedMessage)

  if (stream.pendingUser) {
    persisted.push({
      id: 'pending-user',
      role: 'user',
      content: [{ type: 'text', text: stream.pendingUser }],
      metadata: { isOptimistic: true },
    })
  }
  if (stream.assistantId) {
    persisted.push({
      id: stream.assistantId,
      role: 'assistant',
      content: [
        ...(stream.assistantReasoning
          ? [
              {
                type: 'reasoning' as const,
                text: stream.assistantReasoning,
              },
            ]
          : []),
        ...(stream.assistantContent
          ? [{ type: 'text' as const, text: stream.assistantContent }]
          : []),
      ],
      status: { type: 'running' },
      metadata: { isOptimistic: true },
    })
  }

  return persisted
}
