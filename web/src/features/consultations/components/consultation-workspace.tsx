import { useSyncExternalStore } from 'react'
import { cn } from '@/lib/utils'
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
} from '@/components/ui/sheet'

const DESKTOP_WORKSPACE_QUERY = '(min-width: 1280px)'

type ConsultationWorkspaceProps = {
  assistantOpen: boolean
  onAssistantOpenChange: (open: boolean) => void
  assistant: React.ReactNode
  children: React.ReactNode
}

export function ConsultationWorkspace({
  assistantOpen,
  onAssistantOpenChange,
  assistant,
  children,
}: ConsultationWorkspaceProps) {
  const desktop = useDesktopWorkspace()

  return (
    <div
      className={cn(
        'grid min-h-0 flex-1',
        desktop && assistantOpen && 'grid-cols-[minmax(0,1fr)_28rem]'
      )}
    >
      <div className='min-h-0 overflow-y-auto'>{children}</div>

      {desktop ? (
        assistantOpen && (
          <aside
            id='consultation-assistant'
            aria-label='Assistant IA de la consultation'
            className='min-h-0 overflow-hidden border-l bg-background'
          >
            {assistant}
          </aside>
        )
      ) : (
        <Sheet open={assistantOpen} onOpenChange={onAssistantOpenChange}>
          <SheetContent
            id='consultation-assistant'
            side='right'
            className='w-full gap-0 p-0 sm:max-w-lg'
          >
            <SheetHeader className='sr-only'>
              <SheetTitle>Assistant IA de la consultation</SheetTitle>
              <SheetDescription>
                Interrogez les documents analysés de la consultation.
              </SheetDescription>
            </SheetHeader>
            {assistant}
          </SheetContent>
        </Sheet>
      )}
    </div>
  )
}

function useDesktopWorkspace() {
  return useSyncExternalStore(
    (callback) => {
      const mediaQuery = window.matchMedia(DESKTOP_WORKSPACE_QUERY)
      mediaQuery.addEventListener('change', callback)
      return () => mediaQuery.removeEventListener('change', callback)
    },
    () => window.matchMedia(DESKTOP_WORKSPACE_QUERY).matches,
    () => false
  )
}
