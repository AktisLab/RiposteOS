import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useNavigate } from '@tanstack/react-router'
import {
  BookOpenCheck,
  CalendarClock,
  Loader2,
  RotateCcw,
  X,
} from 'lucide-react'
import { toast } from 'sonner'
import { cpvCatalogQueryKey, loadCpvCatalog } from '@/lib/cpv-catalog'
import { cn } from '@/lib/utils'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import {
  Tooltip,
  TooltipContent,
  TooltipTrigger,
} from '@/components/ui/tooltip'
import { consultationsQueryRoot } from '@/features/consultations/api'
import {
  type Opportunity,
  type OpportunityStatus,
  opportunitiesQueryRoot,
  promoteOpportunity,
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
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const statusMutation = useMutation({
    mutationFn: (status: OpportunityStatus) =>
      updateOpportunityStatus(opportunity.id, status),
    onSuccess: (_, status) => {
      void queryClient.invalidateQueries({ queryKey: opportunitiesQueryRoot })
      toast.success(
        status === 'Dismissed'
          ? 'Opportunité écartée'
          : 'Opportunité à réexaminer'
      )
    },
    onError: (error) => toast.error(error.message),
  })
  const promotionMutation = useMutation({
    mutationFn: () => promoteOpportunity(opportunity.id),
    onSuccess: (consultation) => {
      void queryClient.invalidateQueries({ queryKey: opportunitiesQueryRoot })
      void queryClient.invalidateQueries({ queryKey: consultationsQueryRoot })
      toast.success('Étude ouverte')
      void navigate({
        to: '/consultations/$consultationId',
        params: { consultationId: consultation.id },
      })
    },
    onError: (error) => toast.error(error.message),
  })
  const pending = statusMutation.isPending || promotionMutation.isPending

  if (opportunity.status === 'Retained') {
    return (
      <Badge
        variant='outline'
        className='gap-1.5 text-emerald-700 dark:text-emerald-400'
      >
        <BookOpenCheck />
        Étude ouverte
      </Badge>
    )
  }

  if (opportunity.status === 'Dismissed') {
    return (
      <Button
        variant='outline'
        size='sm'
        onClick={() => statusMutation.mutate('ToQualify')}
        disabled={pending}
      >
        {statusMutation.isPending ? (
          <Loader2 className='animate-spin' />
        ) : (
          <RotateCcw />
        )}
        Réexaminer
      </Button>
    )
  }

  return (
    <div className='flex items-center gap-1.5'>
      <Button
        size='sm'
        onClick={() => promotionMutation.mutate()}
        disabled={pending}
      >
        {promotionMutation.isPending ? (
          <Loader2 className='animate-spin' />
        ) : (
          <BookOpenCheck />
        )}
        {promotionMutation.isPending ? 'Ouverture…' : 'Étudier'}
      </Button>
      <Button
        variant='ghost'
        size='icon'
        className='size-8'
        onClick={() => statusMutation.mutate('Dismissed')}
        disabled={pending}
        aria-label={`Écarter l’opportunité ${opportunity.title}`}
        title='Écarter'
      >
        {statusMutation.isPending ? (
          <Loader2 className='animate-spin' />
        ) : (
          <X />
        )}
      </Button>
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
