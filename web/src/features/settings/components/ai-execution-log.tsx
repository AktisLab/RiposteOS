import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { getRouteApi } from '@tanstack/react-router'
import { CircleAlert, CircleCheck, CircleDashed, Loader2 } from 'lucide-react'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { StateMessage } from '@/components/state-message'
import {
  aiExecutionOperationPresentation,
  aiExecutionStatusPresentation,
  buildAiExecutionFilter,
  formatAiExecutionDuration,
  formatAiExecutionPayload,
  parseDocumentAnalysisInput,
} from '../ai-execution-presentation'
import {
  type AiExecutionLog,
  aiExecutionLogDetailsQueryKey,
  aiExecutionLogsQueryKey,
  getAiExecutionLogDetails,
  getAiExecutionLogs,
} from '../api'

const route = getRouteApi('/_authenticated/settings/ai')

export function AiExecutionLog() {
  const search = route.useSearch()
  const navigate = route.useNavigate()
  const [selectedExecution, setSelectedExecution] =
    useState<AiExecutionLog | null>(null)
  const operation = search.operation ?? 'all'
  const status = search.status ?? 'all'
  const page = search.executionPage ?? 1
  const query = {
    page,
    pageSize: 20,
    filter: buildAiExecutionFilter(operation, status),
  }
  const executions = useQuery({
    queryKey: aiExecutionLogsQueryKey(query),
    queryFn: () => getAiExecutionLogs(query),
    placeholderData: (previous) => previous,
    refetchInterval: (state) =>
      state.state.data?.items.some((item) => item.status === 'Running')
        ? 3_000
        : false,
  })
  const totalCount = executions.data?.totalCount ?? 0
  const pageCount = Math.max(1, Math.ceil(totalCount / query.pageSize))

  const updateSearch = (next: {
    operation?: typeof operation
    status?: typeof status
    executionPage?: number
  }) => {
    void navigate({
      search: (previous) => ({
        ...previous,
        ...next,
      }),
    })
  }

  return (
    <section aria-labelledby='ai-execution-log'>
      <div className='flex flex-col justify-between gap-3 sm:flex-row sm:items-center'>
        <h2
          id='ai-execution-log'
          className='text-base font-semibold tracking-tight'
        >
          Journal d’exécution
        </h2>
        <div className='flex flex-wrap gap-2'>
          <Select
            value={operation}
            onValueChange={(value) =>
              updateSearch({
                operation: value as typeof operation,
                executionPage: 1,
              })
            }
          >
            <SelectTrigger aria-label='Filtrer par opération' className='w-48'>
              <SelectValue placeholder='Toutes les opérations' />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value='all'>Toutes les opérations</SelectItem>
              <SelectItem value='DocumentAnalysis'>
                Analyse documentaire
              </SelectItem>
              <SelectItem value='DocumentClassification'>Classement</SelectItem>
            </SelectContent>
          </Select>
          <Select
            value={status}
            onValueChange={(value) =>
              updateSearch({
                status: value as typeof status,
                executionPage: 1,
              })
            }
          >
            <SelectTrigger aria-label='Filtrer par résultat' className='w-40'>
              <SelectValue placeholder='Tous les résultats' />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value='all'>Tous les résultats</SelectItem>
              <SelectItem value='Running'>En cours</SelectItem>
              <SelectItem value='Completed'>Terminés</SelectItem>
              <SelectItem value='Failed'>Échecs</SelectItem>
              <SelectItem value='NotConfigured'>Non configuré</SelectItem>
            </SelectContent>
          </Select>
        </div>
      </div>

      {executions.isPending ? (
        <StateMessage icon={<Loader2 className='animate-spin' />} role='status'>
          Chargement du journal IA…
        </StateMessage>
      ) : executions.isError ? (
        <StateMessage icon={<CircleAlert />} role='alert'>
          {executions.error.message}
        </StateMessage>
      ) : (
        <>
          <div className='mt-4 overflow-x-auto border-y'>
            <table className='w-full min-w-[47.5rem] text-sm'>
              <thead className='border-b text-left text-xs text-muted-foreground'>
                <tr>
                  <th className='px-3 py-3 font-medium'>Exécution</th>
                  <th className='px-3 py-3 font-medium'>Cible</th>
                  <th className='px-3 py-3 font-medium'>Service</th>
                  <th className='px-3 py-3 font-medium'>Résultat</th>
                  <th className='px-3 py-3 font-medium'>Démarrée</th>
                  <th className='px-3 py-3 font-medium' aria-label='Détail' />
                </tr>
              </thead>
              <tbody>
                {executions.data.items.length === 0 ? (
                  <tr>
                    <td
                      colSpan={6}
                      className='h-28 px-3 text-center text-muted-foreground'
                    >
                      Aucune exécution ne correspond à ces filtres.
                    </td>
                  </tr>
                ) : (
                  executions.data.items.map((execution) => (
                    <ExecutionRow
                      key={execution.id}
                      execution={execution}
                      onOpen={() => setSelectedExecution(execution)}
                    />
                  ))
                )}
              </tbody>
            </table>
          </div>
          {totalCount > query.pageSize && (
            <div className='flex items-center justify-end gap-3 pt-3 text-sm text-muted-foreground'>
              <span>
                Page {page} sur {pageCount}
              </span>
              <Button
                size='sm'
                variant='outline'
                disabled={page === 1}
                onClick={() => updateSearch({ executionPage: page - 1 })}
              >
                Précédent
              </Button>
              <Button
                size='sm'
                variant='outline'
                disabled={page >= pageCount}
                onClick={() => updateSearch({ executionPage: page + 1 })}
              >
                Suivant
              </Button>
            </div>
          )}
        </>
      )}

      <AiExecutionDetails
        execution={selectedExecution}
        onOpenChange={(open) => !open && setSelectedExecution(null)}
      />
    </section>
  )
}

function ExecutionRow({
  execution,
  onOpen,
}: {
  execution: AiExecutionLog
  onOpen: () => void
}) {
  const status = aiExecutionStatusPresentation[execution.status]
  const Icon =
    execution.status === 'Completed'
      ? CircleCheck
      : execution.status === 'Running'
        ? CircleDashed
        : CircleAlert
  const duration = formatAiExecutionDuration(
    execution.startedAt,
    execution.completedAt,
    execution.failedAt
  )

  return (
    <tr className='border-b last:border-b-0 hover:bg-muted/30'>
      <td className='px-3 py-3 font-medium'>
        {aiExecutionOperationPresentation[execution.operation]}
      </td>
      <td
        className='max-w-72 truncate px-3 py-3'
        title={execution.subjectLabel}
      >
        {execution.subjectLabel}
      </td>
      <td className='px-3 py-3 text-muted-foreground'>
        {execution.providerName ? (
          <span>
            {execution.providerName}
            {execution.model ? ` · ${execution.model}` : ''}
          </span>
        ) : (
          '—'
        )}
      </td>
      <td className={`px-3 py-3 ${status.className}`}>
        <span className='inline-flex items-center gap-1.5'>
          <Icon aria-hidden='true' className='size-3.5' />
          {status.label}
          {duration ? ` · ${duration}` : ''}
        </span>
      </td>
      <td className='px-3 py-3 whitespace-nowrap text-muted-foreground'>
        {new Intl.DateTimeFormat('fr-FR', {
          dateStyle: 'short',
          timeStyle: 'medium',
        }).format(new Date(execution.startedAt))}
      </td>
      <td className='px-3 py-3 text-right'>
        <Button size='sm' variant='ghost' onClick={onOpen}>
          Détail
        </Button>
      </td>
    </tr>
  )
}

function AiExecutionDetails({
  execution,
  onOpenChange,
}: {
  execution: AiExecutionLog | null
  onOpenChange: (open: boolean) => void
}) {
  const detailQuery = useQuery({
    queryKey: aiExecutionLogDetailsQueryKey(execution?.id ?? ''),
    queryFn: () => getAiExecutionLogDetails(execution?.id ?? ''),
    enabled: execution !== null,
  })

  if (!execution) return null

  const details = [
    ['Cible', execution.subjectLabel],
    ['Service', execution.providerName ?? '—'],
    ['Modèle', execution.model ?? '—'],
    ['Identifiant cible', execution.subjectId],
    ['Identifiant de corrélation', execution.correlationId ?? '—'],
    ['Identifiant exécution', execution.id],
  ]

  return (
    <Dialog open onOpenChange={onOpenChange}>
      <DialogContent className='max-w-3xl'>
        <DialogHeader>
          <DialogTitle>
            {aiExecutionOperationPresentation[execution.operation]}
          </DialogTitle>
        </DialogHeader>
        <dl className='divide-y border-y text-sm'>
          {details.map(([label, value]) => (
            <div
              key={label}
              className='grid gap-1 px-0 py-3 sm:grid-cols-[10rem_1fr]'
            >
              <dt className='text-muted-foreground'>{label}</dt>
              <dd className='font-mono text-xs break-all'>{value}</dd>
            </div>
          ))}
          {execution.errorMessage && (
            <div className='grid gap-1 px-0 py-3 sm:grid-cols-[10rem_1fr]'>
              <dt className='text-muted-foreground'>Erreur</dt>
              <dd>{execution.errorMessage}</dd>
            </div>
          )}
        </dl>
        {detailQuery.isPending ? (
          <StateMessage
            icon={<Loader2 className='animate-spin' />}
            role='status'
          >
            Chargement des échanges IA…
          </StateMessage>
        ) : detailQuery.isError ? (
          <StateMessage icon={<CircleAlert />} role='alert'>
            {detailQuery.error.message}
          </StateMessage>
        ) : (
          <div className='space-y-5'>
            <ExecutionPayload
              label='Donnée envoyée'
              payload={detailQuery.data.input}
              operation={execution.operation}
            />
            <ExecutionPayload
              label='Réponse reçue'
              payload={detailQuery.data.output}
            />
          </div>
        )}
      </DialogContent>
    </Dialog>
  )
}

function ExecutionPayload({
  label,
  payload,
  operation,
}: {
  label: string
  payload: string | null
  operation?: AiExecutionLog['operation']
}) {
  if (payload === null) return null

  const documentInput =
    operation === 'DocumentAnalysis' && label === 'Donnée envoyée'
      ? parseDocumentAnalysisInput(payload)
      : null
  const documentDetails = documentInput
    ? [
        ['Fichier', documentInput.fileName],
        ['Type', documentInput.contentType],
        ['Taille', documentInput.size],
        ['Empreinte SHA-256', documentInput.sha256],
        ['Identifiant document', documentInput.id],
      ]
    : null

  return (
    <section aria-label={label}>
      <h3 className='mb-2 text-sm font-medium'>{label}</h3>
      {documentDetails ? (
        <dl className='divide-y border-y text-sm'>
          {documentDetails.map(([detailLabel, value]) => (
            <div
              key={detailLabel}
              className='grid gap-1 py-2 sm:grid-cols-[10rem_1fr]'
            >
              <dt className='text-muted-foreground'>{detailLabel}</dt>
              <dd className='font-mono text-xs break-all'>{value}</dd>
            </div>
          ))}
        </dl>
      ) : (
        <pre className='max-h-80 overflow-auto border bg-muted/30 p-3 font-mono text-xs break-words whitespace-pre-wrap'>
          {formatAiExecutionPayload(payload)}
        </pre>
      )}
    </section>
  )
}
