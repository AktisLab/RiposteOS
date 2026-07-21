import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useExternalStoreRuntime } from '@assistant-ui/react'
import { toast } from 'sonner'
import { retryAssistantMessage, streamAssistantMessage } from './api'
import {
  createRuntimeMessages,
  idleAssistantStream,
  startAssistantRetry,
  startAssistantStream,
  updateAssistantStream,
} from './runtime'
import { type AssistantMessage } from './types'

const suggestions = [
  'Quelle est la date limite de remise des offres ?',
  'Quels sont les critères de sélection ?',
  'Quels livrables sont attendus ?',
]

type UseAssistantRuntimeOptions = {
  consultationId: string
  conversationId: string
  messages: AssistantMessage[]
  canAsk: boolean
  onChanged: () => void
}

export function useAssistantRuntime({
  consultationId,
  conversationId,
  messages,
  canAsk,
  onChanged,
}: UseAssistantRuntimeOptions) {
  const [stream, setStream] = useState(idleAssistantStream)
  const controller = useRef<AbortController | null>(null)

  useEffect(() => () => controller.current?.abort(), [])

  const send = useCallback(
    async (value: string) => {
      const submitted = value.trim()
      if (!submitted || controller.current || !canAsk) return

      const abortController = new AbortController()
      controller.current = abortController
      setStream(startAssistantStream(submitted))
      try {
        await streamAssistantMessage(
          consultationId,
          conversationId,
          submitted,
          abortController.signal,
          (event) => {
            setStream((current) => updateAssistantStream(current, event))
            if (event.type === 'message_completed') onChanged()
            if (event.type === 'message_failed' && event.error) {
              toast.error(event.error)
            }
            if (event.type === 'message_cancelled') {
              toast.message('Génération arrêtée.')
            }
          }
        )
      } catch (error) {
        if (!(error instanceof DOMException && error.name === 'AbortError')) {
          toast.error(
            error instanceof Error ? error.message : 'Erreur temporaire.'
          )
        }
      } finally {
        controller.current = null
        setStream((current) => ({
          ...current,
          isRunning: false,
          pendingUser: null,
          assistantId: null,
          assistantReasoning: '',
          assistantContent: '',
          activity: null,
        }))
        onChanged()
      }
    },
    [canAsk, consultationId, conversationId, onChanged]
  )

  const retry = useCallback(
    async (userMessageId: string) => {
      if (controller.current || !canAsk) return

      const abortController = new AbortController()
      controller.current = abortController
      setStream(startAssistantRetry())
      try {
        await retryAssistantMessage(
          consultationId,
          conversationId,
          userMessageId,
          abortController.signal,
          (event) => {
            setStream((current) => updateAssistantStream(current, event))
            if (event.type === 'message_completed') onChanged()
            if (event.type === 'message_failed' && event.error) {
              toast.error(event.error)
            }
          }
        )
      } catch (error) {
        if (!(error instanceof DOMException && error.name === 'AbortError')) {
          toast.error(
            error instanceof Error ? error.message : 'Erreur temporaire.'
          )
        }
      } finally {
        controller.current = null
        setStream(idleAssistantStream)
        onChanged()
      }
    },
    [canAsk, consultationId, conversationId, onChanged]
  )

  const runtimeMessages = useMemo(
    () => createRuntimeMessages(messages, stream),
    [messages, stream]
  )
  const runtime = useExternalStoreRuntime({
    isDisabled: !canAsk,
    isRunning: stream.isRunning,
    messages: runtimeMessages,
    suggestions: canAsk ? suggestions.map((prompt) => ({ prompt })) : [],
    convertMessage: (message) => message,
    onNew: async (message) => {
      const question = message.content
        .filter((part) => part.type === 'text')
        .map((part) => part.text)
        .join('')
      await send(question)
    },
    onReload: async (parentId) => {
      if (parentId) await retry(parentId)
    },
    onCancel: async () => controller.current?.abort(),
    unstable_capabilities: { copy: true },
  })

  return { runtime, activity: stream.activity }
}
