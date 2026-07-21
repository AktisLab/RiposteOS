import {
  AssistantRuntimeProvider,
  AuiIf,
  SuggestionPrimitive,
  ThreadPrimitive,
} from '@assistant-ui/react'
import { ArrowDown, CircleAlert, FileSearch, Sparkles } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { type AssistantEvidence, type AssistantMessage } from '../types'
import { useAssistantRuntime } from '../use-assistant-runtime'
import { AssistantComposer } from './assistant-composer'
import { AssistantMessageView } from './assistant-message'

type AssistantThreadProps = {
  consultationId: string
  conversationId: string
  messages: AssistantMessage[]
  loading: boolean
  errorMessage: string | null
  canAsk: boolean
  unavailableReason: string
  onEvidenceSelected: (evidence: AssistantEvidence) => void
  onChanged: () => void
}

export function AssistantThread({
  consultationId,
  conversationId,
  messages,
  loading,
  errorMessage,
  canAsk,
  unavailableReason,
  onEvidenceSelected,
  onChanged,
}: AssistantThreadProps) {
  const { runtime, activity } = useAssistantRuntime({
    consultationId,
    conversationId,
    messages,
    canAsk,
    onChanged,
  })

  return (
    <AssistantRuntimeProvider runtime={runtime}>
      <ThreadPrimitive.Root className='relative flex h-full min-h-0 flex-col bg-background'>
        <ThreadPrimitive.Viewport
          turnAnchor='top'
          topAnchorMessageClamp={{ tallerThan: '9rem', visibleHeight: '6rem' }}
          scrollToBottomOnInitialize
          scrollToBottomOnRunStart
          className='min-h-0 flex-1 overflow-y-auto px-4 py-5'
        >
          {loading ? (
            <ThreadLoading />
          ) : errorMessage ? (
            <div
              className='flex h-full flex-col items-center justify-center gap-2 text-center text-sm text-muted-foreground'
              role='alert'
            >
              <CircleAlert aria-hidden='true' className='size-5' />
              {errorMessage}
            </div>
          ) : (
            <>
              <AuiIf condition={(state) => state.thread.isEmpty}>
                <ThreadEmpty
                  canAsk={canAsk}
                  unavailableReason={unavailableReason}
                />
              </AuiIf>
              <div className='space-y-6'>
                <ThreadPrimitive.Messages
                  components={{
                    Message: () => (
                      <AssistantMessageView
                        onEvidenceSelected={onEvidenceSelected}
                        activity={activity}
                      />
                    ),
                  }}
                />
              </div>
            </>
          )}
          <ThreadPrimitive.ScrollToBottom asChild>
            <Button
              size='icon'
              variant='outline'
              className='sticky bottom-2 left-1/2 z-10 size-8 -translate-x-1/2 rounded-full bg-background shadow-sm disabled:hidden'
              aria-label='Revenir au dernier message'
              title='Revenir au dernier message'
            >
              <ArrowDown />
            </Button>
          </ThreadPrimitive.ScrollToBottom>
        </ThreadPrimitive.Viewport>
        <AssistantComposer />
      </ThreadPrimitive.Root>
    </AssistantRuntimeProvider>
  )
}

function ThreadLoading() {
  return (
    <div
      className='space-y-6'
      role='status'
      aria-label='Chargement des messages'
    >
      <div className='ml-auto w-2/3 space-y-2'>
        <Skeleton className='ml-auto h-3 w-16' />
        <Skeleton className='h-16 w-full rounded-lg' />
      </div>
      <div className='flex gap-3'>
        <Skeleton className='size-7 shrink-0 rounded-md' />
        <div className='w-3/4 space-y-2'>
          <Skeleton className='h-3 w-28' />
          <Skeleton className='h-3 w-full' />
          <Skeleton className='h-3 w-4/5' />
        </div>
      </div>
    </div>
  )
}

function ThreadEmpty({
  canAsk,
  unavailableReason,
}: {
  canAsk: boolean
  unavailableReason: string
}) {
  return (
    <div className='mx-auto flex h-full max-w-xl flex-col justify-center py-8 text-center'>
      <div className='mx-auto flex size-9 items-center justify-center rounded-md border bg-muted/40'>
        {canAsk ? (
          <Sparkles aria-hidden='true' className='size-4' />
        ) : (
          <FileSearch aria-hidden='true' className='size-4' />
        )}
      </div>
      <h3 className='mt-3 text-sm font-medium'>
        {canAsk ? 'Interrogez le dossier' : 'Assistant indisponible'}
      </h3>
      <p className='mt-1 text-xs leading-5 text-muted-foreground'>
        {canAsk
          ? 'Les réponses sont construites à partir des passages indexés et restent reliées à leurs sources.'
          : unavailableReason}
      </p>
      {canAsk && (
        <div className='mt-4 flex flex-wrap justify-center gap-2'>
          <ThreadPrimitive.Suggestions
            components={{ Suggestion: AssistantSuggestion }}
          />
        </div>
      )}
    </div>
  )
}

function AssistantSuggestion() {
  return (
    <SuggestionPrimitive.Trigger asChild send>
      <Button variant='outline' size='sm' className='h-auto py-1.5 text-xs'>
        <SuggestionPrimitive.Title />
      </Button>
    </SuggestionPrimitive.Trigger>
  )
}
