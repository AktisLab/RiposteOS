export type AssistantEvidence = {
  passageId: string
  score: number
  documentId: string
  documentName: string
  pageNumber: number | null
  sectionTitle: string | null
  ordinal: number
  text: string
}

export type AssistantConversation = {
  id: string
  title: string
  createdAt: string
  updatedAt: string
  archivedAt: string | null
}

export type AssistantAnswerDetails = {
  status: 'Answered' | 'InsufficientEvidence'
  gaps: string[]
  followUps: string[]
  reasoningSummary: string | null
}

export type AssistantMessage = {
  id: string
  role: 0 | 1
  content: string | null
  status: 0 | 1 | 2 | 3
  errorMessage: string | null
  createdAt: string
  completedAt: string | null
  failedAt: string | null
  providerName: string | null
  model: string | null
  evidence: AssistantEvidence[]
  details: AssistantAnswerDetails | null
}

export type AssistantConversationDetails = {
  conversation: AssistantConversation
  messages: AssistantMessage[]
}

export type AssistantStreamEvent = {
  type:
    | 'message_started'
    | 'reasoning_delta'
    | 'answer_delta'
    | 'message_completed'
    | 'message_failed'
    | 'message_cancelled'
    | 'activity'
  messageId: string | null
  delta: string | null
  error: string | null
  message: AssistantMessage | null
  activity: string | null
}
