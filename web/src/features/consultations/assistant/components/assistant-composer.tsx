import { AuiIf, ComposerPrimitive } from '@assistant-ui/react'
import { ArrowUp, Square } from 'lucide-react'
import { Button } from '@/components/ui/button'

export function AssistantComposer() {
  return (
    <ComposerPrimitive.Root className='border-t bg-background p-3'>
      <div className='rounded-lg border bg-background shadow-xs transition-shadow focus-within:ring-2 focus-within:ring-ring/20'>
        <ComposerPrimitive.Input
          aria-label='Question pour l’assistant'
          placeholder='Posez une question sur le dossier…'
          submitMode='enter'
          className='field-sizing-content max-h-36 min-h-12 w-full resize-none bg-transparent px-3 pt-3 pb-2 text-sm outline-none placeholder:text-muted-foreground disabled:cursor-not-allowed disabled:opacity-50'
        />
        <div className='flex items-center justify-end px-2 pb-2'>
          <div>
            <AuiIf condition={(state) => state.thread.isRunning}>
              <ComposerPrimitive.Cancel asChild>
                <Button
                  size='icon'
                  variant='outline'
                  className='size-8'
                  aria-label='Arrêter la réponse'
                  title='Arrêter la réponse'
                >
                  <Square className='size-3 fill-current' />
                </Button>
              </ComposerPrimitive.Cancel>
            </AuiIf>
            <AuiIf condition={(state) => !state.thread.isRunning}>
              <ComposerPrimitive.Send asChild>
                <Button
                  size='icon'
                  className='size-8'
                  aria-label='Envoyer la question'
                  title='Envoyer la question'
                >
                  <ArrowUp />
                </Button>
              </ComposerPrimitive.Send>
            </AuiIf>
          </div>
        </div>
      </div>
    </ComposerPrimitive.Root>
  )
}
