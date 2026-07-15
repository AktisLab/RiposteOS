import { AlertTriangle, CheckCircle2, Clock3, Loader2 } from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { type ImportRun } from '../api'

export function ImportRunStatus({ run }: { run: ImportRun }) {
  const presentation = statusPresentation[run.status]
  const Icon = presentation.icon

  return (
    <div className='space-y-3 rounded-lg border bg-muted/20 p-4'>
      <div className='flex min-w-0 items-center gap-3'>
        <Icon
          className={`size-4 shrink-0 ${presentation.className} ${run.status === 'Running' ? 'animate-spin' : ''}`}
        />
        <div className='min-w-0'>
          <div className='flex flex-wrap items-center gap-2'>
            <p className='text-sm font-medium'>Import BOAMP</p>
            <Badge variant='outline'>{presentation.label}</Badge>
          </div>
          <p className='mt-0.5 truncate text-xs text-muted-foreground'>
            {getDetail(run)}
          </p>
        </div>
      </div>
      <div className='grid grid-cols-2 gap-2 text-xs tabular-nums sm:grid-cols-4'>
        <Metric label='Récupérés' value={run.fetched} />
        <Metric label='Créés' value={run.created} />
        <Metric label='Actualisés' value={run.updated} />
        {run.skipped > 0 && <Metric label='Ignorés' value={run.skipped} />}
      </div>
    </div>
  )
}

function Metric({ label, value }: { label: string; value: number }) {
  return (
    <span className='rounded-md bg-background px-2.5 py-2 text-muted-foreground'>
      <span className='block'>{label}</span>
      <strong className='mt-0.5 block text-sm font-semibold text-foreground'>
        {value}
      </strong>
    </span>
  )
}

function getDetail(run: ImportRun) {
  if (run.errorMessage) return run.errorMessage
  if (run.status === 'Queued')
    return 'En attente de prise en charge par le worker.'
  if (run.status === 'Running' && run.currentPublicationDate) {
    return `Traitement des avis publiés le ${new Intl.DateTimeFormat('fr-FR').format(new Date(`${run.currentPublicationDate}T00:00:00`))}`
  }
  if (run.finishedAt) {
    return `Terminé le ${new Intl.DateTimeFormat('fr-FR', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(run.finishedAt))}`
  }
  return 'Initialisation de la synchronisation.'
}

const statusPresentation = {
  Queued: {
    label: 'Planifié',
    icon: Clock3,
    className: 'text-muted-foreground',
  },
  Running: {
    label: 'En cours',
    icon: Loader2,
    className: 'text-blue-600',
  },
  Succeeded: {
    label: 'Terminé',
    icon: CheckCircle2,
    className: 'text-emerald-600',
  },
  PartiallyFailed: {
    label: 'Terminé avec alertes',
    icon: AlertTriangle,
    className: 'text-amber-600',
  },
  Failed: {
    label: 'Échec',
    icon: AlertTriangle,
    className: 'text-destructive',
  },
} satisfies Record<
  ImportRun['status'],
  object & {
    label: string
    icon: typeof Clock3
    className: string
  }
>
