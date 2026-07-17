import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { ChevronLeft, ChevronRight, History, Loader2 } from 'lucide-react'
import { Button } from '@/components/ui/button'
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
} from '@/components/ui/sheet'
import { StateMessage } from '@/components/state-message'
import { getImportRuns, importRunsQueryKey } from '../api'
import { ImportRunStatus } from './import-run-status'

const pageSize = 10

export function ImportHistoryDrawer({
  open,
  onOpenChange,
}: {
  open: boolean
  onOpenChange: (open: boolean) => void
}) {
  const [page, setPage] = useState(1)
  const query = { page, pageSize }
  const importsQuery = useQuery({
    queryKey: importRunsQueryKey(query),
    queryFn: () => getImportRuns(query),
    enabled: open,
    refetchInterval: (current) =>
      current.state.data?.items.some(
        (run) => run.status === 'Queued' || run.status === 'Running'
      )
        ? 2_000
        : false,
  })
  const pageData = importsQuery.data
  const pageCount = Math.max(
    1,
    Math.ceil((pageData?.totalCount ?? 0) / pageSize)
  )

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent className='w-full gap-0 sm:max-w-lg'>
        <SheetHeader className='border-b pe-12 text-start'>
          <SheetTitle>Historique des synchronisations</SheetTitle>
          <SheetDescription>
            Imports par source, progression et résultats de chaque exécution.
          </SheetDescription>
        </SheetHeader>

        <div className='min-h-0 flex-1 overflow-y-auto p-4'>
          {importsQuery.isPending ? (
            <StateMessage icon={<Loader2 className='animate-spin' />}>
              Chargement de l’historique…
            </StateMessage>
          ) : importsQuery.isError ? (
            <StateMessage icon={<History />}>
              {importsQuery.error.message}
            </StateMessage>
          ) : pageData?.items.length === 0 ? (
            <StateMessage icon={<History />}>
              Aucune synchronisation enregistrée.
            </StateMessage>
          ) : (
            <div className='space-y-3'>
              {pageData?.items.map((run) => (
                <ImportRunStatus key={run.id} run={run} />
              ))}
            </div>
          )}
        </div>

        <div className='flex items-center justify-between border-t p-4'>
          <p className='text-sm text-muted-foreground'>
            Page {page} sur {pageCount}
          </p>
          <div className='flex gap-2'>
            <Button
              variant='outline'
              size='icon'
              onClick={() => setPage((current) => current - 1)}
              disabled={page <= 1}
              aria-label='Page précédente'
            >
              <ChevronLeft />
            </Button>
            <Button
              variant='outline'
              size='icon'
              onClick={() => setPage((current) => current + 1)}
              disabled={page >= pageCount}
              aria-label='Page suivante'
            >
              <ChevronRight />
            </Button>
          </div>
        </div>
      </SheetContent>
    </Sheet>
  )
}
