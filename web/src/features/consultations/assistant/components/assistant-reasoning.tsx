import { useState, type ReactNode } from 'react'
import { MessagePartPrimitive } from '@assistant-ui/react'
import { Brain, ChevronDown } from 'lucide-react'
import { cn } from '@/lib/utils'
import {
  Collapsible,
  CollapsibleContent,
  CollapsibleTrigger,
} from '@/components/ui/collapsible'

export function AssistantReasoning({
  children,
  streaming,
}: {
  children: ReactNode
  streaming: boolean
}) {
  const [manualOpen, setManualOpen] = useState<boolean | null>(null)
  const open = manualOpen ?? streaming

  return (
    <Collapsible
      open={open}
      onOpenChange={setManualOpen}
      className='mb-3 border-l-2 border-muted pl-3 text-muted-foreground'
    >
      <CollapsibleTrigger className='group/reasoning flex min-h-7 items-center gap-2 text-xs font-medium hover:text-foreground focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none'>
        <Brain
          aria-hidden='true'
          className={cn('size-3.5', streaming && 'animate-pulse')}
        />
        {streaming ? 'Analyse en cours' : 'Voir l’analyse'}
        <ChevronDown
          aria-hidden='true'
          className='size-3.5 transition-transform group-data-[state=open]/reasoning:rotate-180'
        />
      </CollapsibleTrigger>
      <CollapsibleContent className='CollapsibleContent'>
        <div
          className='max-h-40 overflow-y-auto py-2 pr-3 text-xs leading-5 whitespace-pre-wrap'
          aria-live={streaming ? 'polite' : undefined}
        >
          {children}
        </div>
      </CollapsibleContent>
    </Collapsible>
  )
}

export function AssistantReasoningText() {
  return <MessagePartPrimitive.Text smooth />
}
