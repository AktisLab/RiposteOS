import {
  ActionBarPrimitive,
  ErrorPrimitive,
  groupPartByType,
  MessagePrimitive,
  useAuiState,
  useMessagePartText,
} from '@assistant-ui/react'
import {
  Bot,
  Check,
  CircleAlert,
  Copy,
  FileText,
  Loader2,
  RefreshCw,
  User,
} from 'lucide-react'
import { Streamdown } from 'streamdown'
import { cn } from '@/lib/utils'
import { Button } from '@/components/ui/button'
import { type AssistantMessageMetadata } from '../runtime'
import { type AssistantAnswerDetails, type AssistantEvidence } from '../types'
import {
  AssistantReasoning,
  AssistantReasoningText,
} from './assistant-reasoning'

const assistantPartGrouping = groupPartByType({
  reasoning: ['group-reasoning'],
})

type AssistantMessageViewProps = {
  onEvidenceSelected: (evidence: AssistantEvidence) => void
  activity: string | null
}

export function AssistantMessageView({
  onEvidenceSelected,
  activity,
}: AssistantMessageViewProps) {
  const role = useAuiState((state) => state.message.role)
  const status = useAuiState((state) => state.message.status)
  const custom = useAuiState(
    (state) => state.message.metadata.custom
  ) as Partial<AssistantMessageMetadata>
  const isRunning = status?.type === 'running'

  return (
    <MessagePrimitive.Root
      className={cn(
        'group flex w-full gap-3',
        role === 'user' ? 'justify-end' : 'justify-start'
      )}
    >
      <MessagePrimitive.If assistant>
        <div className='mt-0.5 flex size-7 shrink-0 items-center justify-center rounded-md border bg-muted/50 text-muted-foreground'>
          <Bot aria-hidden='true' className='size-4' />
        </div>
      </MessagePrimitive.If>

      <div
        className={cn(
          'min-w-0',
          role === 'user'
            ? 'max-w-[82%] rounded-lg bg-primary px-3.5 py-2.5 text-primary-foreground'
            : 'max-w-[min(100%,48rem)] flex-1 pt-0.5'
        )}
      >
        <MessagePrimitive.If user>
          <div className='mb-1 flex items-center justify-end gap-1.5 text-[11px] font-medium text-primary-foreground/70'>
            <User aria-hidden='true' className='size-3' />
            Vous
          </div>
          <MessagePrimitive.Parts components={{ Text: UserText }} />
        </MessagePrimitive.If>

        <MessagePrimitive.If assistant>
          <div className='mb-2 flex min-h-5 items-center gap-2'>
            <p className='text-xs font-medium'>Assistant du dossier</p>
            {isRunning && (
              <span className='inline-flex items-center gap-1.5 text-[11px] text-muted-foreground'>
                <Loader2 aria-hidden='true' className='size-3 animate-spin' />
                <span className='sr-only'>Réponse en cours. </span>
                {activity ?? 'Rédaction de la réponse…'}
              </span>
            )}
          </div>
          <MessagePrimitive.GroupedParts
            groupBy={assistantPartGrouping}
            indicator='no-text'
          >
            {({ part, children }) => {
              switch (part.type) {
                case 'group-reasoning':
                  return (
                    <AssistantReasoning
                      streaming={part.status.type === 'running'}
                    >
                      {children}
                    </AssistantReasoning>
                  )
                case 'reasoning':
                  return <AssistantReasoningText />
                case 'text':
                  return <AssistantText />
                case 'indicator':
                  return <ThinkingIndicator />
                default:
                  return null
              }
            }}
          </MessagePrimitive.GroupedParts>
          <MessagePrimitive.Error>
            <ErrorPrimitive.Root className='mt-2 flex items-start gap-2 border-l-2 border-destructive/60 py-2 pl-3 text-sm'>
              <CircleAlert
                aria-hidden='true'
                className='mt-0.5 size-4 shrink-0 text-destructive'
              />
              <div className='min-w-0 flex-1'>
                <p className='font-medium'>Réponse non vérifiée</p>
                <ErrorPrimitive.Message className='mt-0.5 block text-xs leading-5 text-muted-foreground' />
              </div>
              <ActionBarPrimitive.Reload asChild>
                <Button
                  variant='outline'
                  size='sm'
                  className='h-7 shrink-0 px-2 text-xs active:scale-[0.98]'
                >
                  <RefreshCw aria-hidden='true' className='size-3.5' />
                  Réessayer
                </Button>
              </ActionBarPrimitive.Reload>
            </ErrorPrimitive.Root>
          </MessagePrimitive.Error>
          {custom.evidence && custom.evidence.length > 0 && (
            <AssistantEvidenceLinks
              evidence={custom.evidence}
              onEvidenceSelected={onEvidenceSelected}
            />
          )}
          {custom.details && (
            <AssistantAnswerDetails details={custom.details} />
          )}
          <AssistantActionBar />
        </MessagePrimitive.If>
      </div>
    </MessagePrimitive.Root>
  )
}

function ThinkingIndicator() {
  return (
    <div className='flex items-center gap-1.5 py-2' role='status'>
      <span className='size-1.5 animate-pulse rounded-full bg-foreground/60' />
      <span className='size-1.5 animate-pulse rounded-full bg-foreground/40 [animation-delay:150ms]' />
      <span className='size-1.5 animate-pulse rounded-full bg-foreground/20 [animation-delay:300ms]' />
      <span className='ml-1 text-xs text-muted-foreground'>Analyse du DCE</span>
    </div>
  )
}

function AssistantActionBar() {
  return (
    <ActionBarPrimitive.Root
      hideWhenRunning
      className='mt-2 flex h-7 items-center opacity-0 transition-opacity group-focus-within:opacity-100 group-hover:opacity-100'
    >
      <ActionBarPrimitive.Copy asChild>
        <Button
          variant='ghost'
          size='sm'
          className='h-7 px-2 text-xs text-muted-foreground'
          title='Copier la réponse'
        >
          <MessagePrimitive.If copied>
            <Check />
          </MessagePrimitive.If>
          <MessagePrimitive.If copied={false}>
            <Copy />
          </MessagePrimitive.If>
          Copier
        </Button>
      </ActionBarPrimitive.Copy>
    </ActionBarPrimitive.Root>
  )
}

function UserText() {
  const part = useMessagePartText()
  return <p className='text-sm leading-6 whitespace-pre-wrap'>{part.text}</p>
}

function AssistantText() {
  const part = useMessagePartText()
  const streaming = part.status.type === 'running'
  return (
    <Streamdown
      mode={streaming ? 'streaming' : 'static'}
      isAnimating={streaming}
      caret={streaming ? 'circle' : undefined}
      skipHtml
      allowedElements={[
        'p',
        'strong',
        'em',
        'ul',
        'ol',
        'li',
        'blockquote',
        'pre',
        'code',
        'table',
        'thead',
        'tbody',
        'tr',
        'th',
        'td',
        'br',
      ]}
      className='prose prose-sm dark:prose-invert max-w-none text-sm leading-6 [&_table]:block [&_table]:max-w-full [&_table]:overflow-x-auto'
    >
      {part.text}
    </Streamdown>
  )
}

function AssistantEvidenceLinks({
  evidence,
  onEvidenceSelected,
}: {
  evidence: AssistantEvidence[]
  onEvidenceSelected: (evidence: AssistantEvidence) => void
}) {
  return (
    <div className='mt-4 border-t pt-3'>
      <p className='mb-2 text-[11px] font-medium tracking-wide text-muted-foreground uppercase'>
        Sources utilisées · {evidence.length}
      </p>
      <div className='flex flex-wrap gap-2'>
        {evidence.map((item) => (
          <button
            type='button'
            key={item.passageId}
            className='inline-flex max-w-full items-center gap-1.5 rounded-md border bg-background px-2 py-1 text-left text-xs text-muted-foreground transition-colors hover:bg-accent hover:text-foreground focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none'
            onClick={() => onEvidenceSelected(item)}
          >
            <FileText aria-hidden='true' className='size-3.5 shrink-0' />
            <span className='truncate'>{item.documentName}</span>
            {item.pageNumber && (
              <span className='shrink-0 text-muted-foreground/70'>
                p. {item.pageNumber}
              </span>
            )}
          </button>
        ))}
      </div>
    </div>
  )
}

function AssistantAnswerDetails({
  details,
}: {
  details: AssistantAnswerDetails
}) {
  if (details.gaps.length === 0 && details.followUps.length === 0) return null
  return (
    <div className='mt-3 grid gap-3 border-t pt-3 text-sm sm:grid-cols-2'>
      {details.gaps.length > 0 && (
        <div>
          <p className='text-xs font-medium text-muted-foreground'>
            Informations manquantes
          </p>
          <ul className='mt-1 list-disc space-y-1 pl-4 text-muted-foreground'>
            {details.gaps.map((gap) => (
              <li key={gap}>{gap}</li>
            ))}
          </ul>
        </div>
      )}
      {details.followUps.length > 0 && (
        <div>
          <p className='text-xs font-medium text-muted-foreground'>
            À vérifier ou demander
          </p>
          <ul className='mt-1 list-disc space-y-1 pl-4 text-muted-foreground'>
            {details.followUps.map((followUp) => (
              <li key={followUp}>{followUp}</li>
            ))}
          </ul>
        </div>
      )}
    </div>
  )
}
