import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { CalendarClock } from 'lucide-react'
import * as m from 'motion/react-m'
import { toast } from 'sonner'
import { cpvCatalogQueryKey, loadCpvCatalog } from '@/lib/cpv-catalog'
import { cn } from '@/lib/utils'
import { Badge } from '@/components/ui/badge'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import {
  Tooltip,
  TooltipContent,
  TooltipTrigger,
} from '@/components/ui/tooltip'
import {
  type Opportunity,
  type OpportunityStatus,
  opportunitiesQueryRoot,
  updateOpportunityStatus,
} from '../api'
import { presentMatchReason } from '../match-reason'

const dateTimeFormatter = new Intl.DateTimeFormat('fr-FR', {
  day: '2-digit',
  month: 'short',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
})

const statusLabels: Record<OpportunityStatus, string> = {
  ToQualify: 'À qualifier',
  Retained: 'Retenue',
  Dismissed: 'Écartée',
}

export function MatchScoreCell({
  opportunity,
  highRelevanceThreshold,
}: {
  opportunity: Opportunity
  highRelevanceThreshold: number
}) {
  const reasonCount = opportunity.matchReasons.length
  const cpvCatalogQuery = useQuery({
    queryKey: cpvCatalogQueryKey,
    queryFn: loadCpvCatalog,
    staleTime: Number.POSITIVE_INFINITY,
  })
  const reasonLabel = `${reasonCount} ${reasonCount === 1 ? 'raison' : 'raisons'}`
  const mediumRelevanceThreshold = Math.ceil(highRelevanceThreshold / 2)
  const tone =
    opportunity.matchScore >= highRelevanceThreshold
      ? 'border-emerald-600/30 bg-emerald-500/10 text-emerald-700 dark:text-emerald-400'
      : opportunity.matchScore >= mediumRelevanceThreshold
        ? 'border-amber-600/30 bg-amber-500/10 text-amber-700 dark:text-amber-400'
        : 'text-muted-foreground'
  const relevanceLabel =
    opportunity.matchScore >= highRelevanceThreshold
      ? 'Très pertinente'
      : opportunity.matchScore >= mediumRelevanceThreshold
        ? 'À examiner'
        : 'Faible'

  return (
    <Tooltip>
      <TooltipTrigger asChild>
        <button
          type='button'
          className='cursor-help rounded-md focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none'
          aria-label={`Score ${opportunity.matchScore} sur 100, ${reasonLabel}`}
        >
          <span className='inline-flex'>
            <Badge
              variant='outline'
              className={cn('gap-1.5 tabular-nums', tone)}
            >
              <strong>{opportunity.matchScore}</strong>
              <span className='font-normal'>{relevanceLabel}</span>
            </Badge>
          </span>
        </button>
      </TooltipTrigger>
      <TooltipContent
        side='right'
        sideOffset={8}
        className='w-80 p-0 text-start'
      >
        {reasonCount > 0 ? (
          <>
            <p className='border-b border-primary-foreground/15 px-3 py-2 font-medium'>
              Détail du score
            </p>
            <ul className='space-y-0.5 p-2'>
              {opportunity.matchReasons.map((reason) => {
                const presentation = presentMatchReason(
                  reason,
                  cpvCatalogQuery.data?.items
                )
                return (
                  <li
                    key={reason}
                    className='grid grid-cols-[2.25rem_1fr] gap-2 rounded-sm px-2 py-1.5'
                  >
                    <span className='font-mono font-semibold tabular-nums'>
                      {presentation.weight}
                    </span>
                    <span className='leading-snug'>
                      <span>{presentation.label}</span>
                      {presentation.detail && (
                        <span className='mt-0.5 block text-primary-foreground/70'>
                          {presentation.detail}
                        </span>
                      )}
                    </span>
                  </li>
                )
              })}
            </ul>
          </>
        ) : (
          <p className='px-3 py-2 leading-snug'>
            Aucun signal n’a contribué à ce score.
          </p>
        )}
      </TooltipContent>
    </Tooltip>
  )
}

export function OpportunityStatusCell({
  opportunity,
}: {
  opportunity: Opportunity
}) {
  const queryClient = useQueryClient()
  const mutation = useMutation({
    mutationFn: (status: OpportunityStatus) =>
      updateOpportunityStatus(opportunity.id, status),
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: opportunitiesQueryRoot }),
    onError: (error) => toast.error(error.message),
  })

  const statusTone = {
    ToQualify: 'bg-amber-500',
    Retained: 'bg-emerald-500',
    Dismissed: 'bg-muted-foreground',
  }[opportunity.status]

  return (
    <div className='w-fit'>
      <Select
        value={opportunity.status}
        onValueChange={(status) => mutation.mutate(status as OpportunityStatus)}
        disabled={mutation.isPending}
      >
        <SelectTrigger size='sm' className='w-32 bg-background/80'>
          <m.span
            key={opportunity.status}
            aria-hidden='true'
            className={cn('size-1.5 rounded-full', statusTone)}
            initial={{ opacity: 0.4, scale: 0.6 }}
            animate={{ opacity: 1, scale: 1 }}
            transition={{ duration: 0.14, ease: 'easeOut' }}
          />
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          {Object.entries(statusLabels).map(([status, label]) => (
            <SelectItem key={status} value={status}>
              {label}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    </div>
  )
}

export function DeadlineCell({
  value,
  referenceTime,
  urgentDeadlineDays,
}: {
  value: string | null
  referenceTime: number
  urgentDeadlineDays: number
}) {
  if (!value) {
    return (
      <span className='rounded-md border border-dashed px-2 py-1 text-xs text-muted-foreground'>
        Non renseignée
      </span>
    )
  }

  const deadline = new Date(value)
  const remainingDays = Math.ceil(
    (deadline.getTime() - referenceTime) / (24 * 60 * 60 * 1000)
  )
  const expired = remainingDays < 0
  const urgent = !expired && remainingDays <= urgentDeadlineDays
  const label = expired
    ? 'Clôturé'
    : remainingDays === 0
      ? "Aujourd'hui"
      : remainingDays === 1
        ? 'Dans 1 jour'
        : `Dans ${remainingDays} jours`

  return (
    <div>
      <span
        className={cn(
          'inline-flex items-center gap-1.5 rounded-md font-medium',
          expired && 'text-muted-foreground',
          urgent && 'bg-destructive/10 px-2 py-1 text-destructive'
        )}
      >
        <CalendarClock className='size-3.5' />
        {label}
      </span>
      <p className='mt-1 text-xs text-muted-foreground'>
        {dateTimeFormatter.format(deadline)}
      </p>
    </div>
  )
}
