import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  ChevronLeft,
  ChevronRight,
  History,
  Loader2,
  MessageSquarePlus,
} from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import {
  archiveAssistantConversation,
  assistantConversationQueryKey,
  assistantConversationsQueryKey,
  createAssistantConversation,
  getAssistantConversation,
  getAssistantConversations,
  renameAssistantConversation,
} from '../api'
import { type AssistantConversation, type AssistantEvidence } from '../types'
import { AssistantThread } from './assistant-thread'
import { ConversationList } from './conversation-list'

type ConsultationAssistantProps = {
  consultationId: string
  activeConversationId: string | null
  onActiveConversationChange: (conversationId: string | null) => void
  canAsk: boolean
  unavailableReason: string
  onEvidenceSelected: (evidence: AssistantEvidence) => void
}

export function ConsultationAssistant({
  consultationId,
  activeConversationId,
  onActiveConversationChange,
  canAsk,
  unavailableReason,
  onEvidenceSelected,
}: ConsultationAssistantProps) {
  const queryClient = useQueryClient()
  const [conversationListOpen, setConversationListOpen] = useState(false)
  const conversationsQuery = useQuery({
    queryKey: assistantConversationsQueryKey(consultationId),
    queryFn: () => getAssistantConversations(consultationId),
  })
  const conversations =
    conversationsQuery.data?.filter((item) => !item.archivedAt) ?? []
  const conversationId =
    conversationsQuery.data === undefined
      ? activeConversationId
      : conversations.some((item) => item.id === activeConversationId)
        ? activeConversationId
        : (conversations[0]?.id ?? null)

  useEffect(() => {
    if (conversationId !== activeConversationId) {
      onActiveConversationChange(conversationId)
    }
  }, [activeConversationId, conversationId, onActiveConversationChange])

  const conversationQuery = useQuery({
    queryKey: assistantConversationQueryKey(
      consultationId,
      conversationId ?? ''
    ),
    queryFn: () => getAssistantConversation(consultationId, conversationId!),
    enabled: conversationId !== null,
  })
  const createMutation = useMutation({
    mutationFn: () => createAssistantConversation(consultationId),
    onSuccess: (conversation) => {
      queryClient.setQueryData<AssistantConversation[]>(
        assistantConversationsQueryKey(consultationId),
        (current) => [
          conversation,
          ...(current ?? []).filter((item) => item.id !== conversation.id),
        ]
      )
      onActiveConversationChange(conversation.id)
      setConversationListOpen(false)
      void queryClient.invalidateQueries({
        queryKey: assistantConversationsQueryKey(consultationId),
      })
    },
    onError: (error) => toast.error(error.message),
  })
  const archiveMutation = useMutation({
    mutationFn: (id: string) =>
      archiveAssistantConversation(consultationId, id),
    onSuccess: () => {
      onActiveConversationChange(null)
      void queryClient.invalidateQueries({
        queryKey: assistantConversationsQueryKey(consultationId),
      })
    },
    onError: (error) => toast.error(error.message),
  })
  const renameMutation = useMutation({
    mutationFn: ({ id, title }: { id: string; title: string }) =>
      renameAssistantConversation(consultationId, id, title),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: assistantConversationsQueryKey(consultationId),
      })
      if (conversationId) {
        void queryClient.invalidateQueries({
          queryKey: assistantConversationQueryKey(
            consultationId,
            conversationId
          ),
        })
      }
    },
    onError: (error) => toast.error(error.message),
  })

  const conversationList = (
    <ConversationList
      conversations={conversations}
      activeConversationId={conversationId}
      onSelect={(id) => {
        onActiveConversationChange(id)
        setConversationListOpen(false)
      }}
      onArchive={(id) => archiveMutation.mutate(id)}
      onRename={(id, title) => renameMutation.mutate({ id, title })}
    />
  )

  const activeConversation = conversations.find(
    (conversation) => conversation.id === conversationId
  )
  const showConversationList = conversationListOpen || conversationId === null

  return (
    <section
      aria-labelledby='assistant-heading'
      className='flex h-full min-h-0 flex-col'
    >
      <header className='shrink-0 border-b px-3 py-3 pr-12 xl:pr-3'>
        <div className='flex items-center justify-between gap-3'>
          <div className='min-w-0'>
            <h2 id='assistant-heading' className='text-sm font-semibold'>
              Assistant du dossier
            </h2>
            <p className='mt-0.5 text-xs text-muted-foreground'>
              Réponses reliées aux sources
            </p>
          </div>
          <Button
            size='icon'
            variant='outline'
            className='size-8 shrink-0'
            aria-label='Nouvelle conversation'
            title='Nouvelle conversation'
            onClick={() => createMutation.mutate()}
            disabled={createMutation.isPending}
          >
            {createMutation.isPending ? (
              <Loader2 className='animate-spin' />
            ) : (
              <MessageSquarePlus />
            )}
          </Button>
        </div>

        <div className='mt-3 flex items-center gap-2'>
          <Button
            size='sm'
            variant='ghost'
            className='h-8 min-w-0 flex-1 justify-start px-2 text-xs'
            aria-expanded={showConversationList}
            onClick={() => setConversationListOpen(!showConversationList)}
          >
            <History aria-hidden='true' />
            <span className='truncate'>
              {activeConversation?.title ?? 'Conversations'}
            </span>
            {showConversationList ? (
              <ChevronLeft aria-hidden='true' className='ml-auto' />
            ) : (
              <ChevronRight aria-hidden='true' className='ml-auto' />
            )}
          </Button>
          <span className='shrink-0 text-[11px] text-muted-foreground tabular-nums'>
            {conversations.length}
          </span>
        </div>
      </header>

      <div className='min-h-0 flex-1'>
        {showConversationList ? (
          <div className='h-full overflow-y-auto bg-muted/20 px-2 py-2'>
            {conversationList}
          </div>
        ) : conversationId ? (
          <AssistantThread
            key={conversationId}
            consultationId={consultationId}
            conversationId={conversationId}
            messages={conversationQuery.data?.messages ?? []}
            loading={conversationQuery.isPending}
            errorMessage={conversationQuery.error?.message ?? null}
            canAsk={canAsk}
            unavailableReason={unavailableReason}
            onEvidenceSelected={onEvidenceSelected}
            onChanged={() => {
              void queryClient.invalidateQueries({
                queryKey: assistantConversationQueryKey(
                  consultationId,
                  conversationId
                ),
              })
              void queryClient.invalidateQueries({
                queryKey: assistantConversationsQueryKey(consultationId),
              })
            }}
          />
        ) : (
          <div className='flex items-center justify-center p-8 text-center text-sm text-muted-foreground'>
            Créez une conversation pour interroger les documents analysés.
          </div>
        )}
      </div>
    </section>
  )
}
