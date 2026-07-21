import { useEffect, useRef } from 'react'
import { useQuery } from '@tanstack/react-query'
import { FileSearch, Loader2 } from 'lucide-react'
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
} from '@/components/ui/sheet'
import { StateMessage } from '@/components/state-message'
import {
  documentAnalysisPassagesQueryKey,
  getDocumentAnalysisPassages,
} from '../api'

type DocumentAnalysisDrawerProps = {
  consultationId: string
  documentId: string
  documentName: string
  targetOrdinal?: number
  open: boolean
  onOpenChange: (open: boolean) => void
}

export function DocumentAnalysisDrawer({
  consultationId,
  documentId,
  documentName,
  targetOrdinal,
  open,
  onOpenChange,
}: DocumentAnalysisDrawerProps) {
  const passagesQuery = useQuery({
    queryKey: documentAnalysisPassagesQueryKey(consultationId, documentId),
    queryFn: () => getDocumentAnalysisPassages(consultationId, documentId),
    enabled: open,
  })
  const target = useRef<HTMLLIElement | null>(null)
  useEffect(() => {
    if (open && passagesQuery.data && target.current) {
      target.current.scrollIntoView({ block: 'center' })
    }
  }, [open, passagesQuery.data, targetOrdinal])

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent className='w-full gap-0 sm:max-w-2xl'>
        <SheetHeader className='border-b pe-12 text-start'>
          <SheetTitle>Passages extraits du document</SheetTitle>
          <SheetDescription>{documentName}</SheetDescription>
        </SheetHeader>
        <div className='min-h-0 flex-1 overflow-y-auto p-4'>
          {passagesQuery.isPending ? (
            <StateMessage icon={<Loader2 className='animate-spin' />}>
              Chargement des passages extraits…
            </StateMessage>
          ) : passagesQuery.isError ? (
            <StateMessage icon={<FileSearch />} role='alert'>
              {passagesQuery.error.message}
            </StateMessage>
          ) : passagesQuery.data?.length === 0 ? (
            <StateMessage icon={<FileSearch />}>
              Aucun passage extrait pour ce document.
            </StateMessage>
          ) : (
            <ol className='space-y-4'>
              {passagesQuery.data?.map((passage) => (
                <li
                  key={passage.ordinal}
                  ref={passage.ordinal === targetOrdinal ? target : undefined}
                  className={`border-b pb-4 last:border-0 ${passage.ordinal === targetOrdinal ? 'bg-accent/60 outline outline-1 outline-ring/40' : ''}`}
                >
                  <p className='text-xs text-muted-foreground'>
                    #{passage.ordinal}
                    {passage.pageNumber && ` · page ${passage.pageNumber}`}
                    {passage.sectionTitle && ` · ${passage.sectionTitle}`}
                    {passage.sourceLocation && ` · ${passage.sourceLocation}`}
                  </p>
                  <p className='mt-1 text-sm whitespace-pre-wrap'>
                    {passage.text}
                  </p>
                </li>
              ))}
            </ol>
          )}
        </div>
      </SheetContent>
    </Sheet>
  )
}
