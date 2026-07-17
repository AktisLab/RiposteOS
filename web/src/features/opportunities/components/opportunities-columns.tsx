import { type ColumnDef } from '@tanstack/react-table'
import { ArrowUpRight, Banknote, Clock3, FileText, MapPin } from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { DataTableColumnHeader } from '@/components/data-table'
import { SourcingSourceLogo } from '@/components/sourcing-source-logo'
import { type Opportunity } from '../api'
import {
  formatEstimatedValue,
  presentContractNature,
  presentExecutionDuration,
  presentProcedureType,
} from '../opportunity-presentation'
import {
  DeadlineCell,
  MatchScoreCell,
  OpportunityStatusCell,
} from './opportunity-cells'

const dateFormatter = new Intl.DateTimeFormat('fr-FR', {
  day: '2-digit',
  month: 'short',
  year: 'numeric',
})

export function createOpportunityColumns(
  referenceTime: number,
  urgentDeadlineDays: number,
  highRelevanceThreshold: number
): ColumnDef<Opportunity>[] {
  return [
    {
      accessorKey: 'source',
      enableHiding: false,
      enableSorting: false,
    },
    {
      accessorKey: 'matchScore',
      header: ({ column }) => (
        <DataTableColumnHeader column={column} title='Pertinence' />
      ),
      meta: { label: 'Pertinence', className: 'min-w-36' },
      cell: ({ row }) => (
        <MatchScoreCell
          opportunity={row.original}
          highRelevanceThreshold={highRelevanceThreshold}
        />
      ),
    },
    {
      accessorKey: 'title',
      header: ({ column }) => (
        <DataTableColumnHeader column={column} title='Opportunité' />
      ),
      meta: {
        label: 'Opportunité',
        className: 'w-[44%] min-w-96',
        tdClassName: 'whitespace-normal py-4',
      },
      cell: ({ row }) => {
        const opportunity = row.original
        const estimatedValue = formatEstimatedValue(
          opportunity.estimatedValue,
          opportunity.currency
        )
        const procedureType = presentProcedureType(opportunity.procedureType)
        const contractNature = presentContractNature(opportunity.contractNature)
        const executionDuration = presentExecutionDuration(
          opportunity.executionDuration
        )

        return (
          <div className='space-y-1.5'>
            <p className='line-clamp-2 leading-snug font-semibold transition-colors group-hover:text-primary'>
              {opportunity.title}
            </p>
            {opportunity.description && (
              <p className='line-clamp-2 text-xs leading-relaxed text-muted-foreground'>
                {opportunity.description}
              </p>
            )}
            {(procedureType ||
              contractNature ||
              estimatedValue ||
              executionDuration) && (
              <div className='flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-foreground/80'>
                {(procedureType || contractNature) && (
                  <span>
                    {[procedureType, contractNature]
                      .filter(Boolean)
                      .join(' · ')}
                  </span>
                )}
                {estimatedValue && (
                  <span className='inline-flex items-center gap-1'>
                    <Banknote className='size-3.5 text-muted-foreground' />
                    {estimatedValue}
                  </span>
                )}
                {executionDuration && (
                  <span className='inline-flex items-center gap-1'>
                    <Clock3 className='size-3.5 text-muted-foreground' />
                    {executionDuration}
                  </span>
                )}
              </div>
            )}
            <div className='flex flex-wrap items-center gap-x-2 gap-y-1 text-xs text-muted-foreground'>
              <Badge variant='outline' className='gap-1.5'>
                <SourcingSourceLogo
                  source={opportunity.source}
                  className='size-3.5'
                />
                {opportunity.source.toUpperCase()}
              </Badge>
              <span>{opportunity.sourceId}</span>
              <span>
                Publié le{' '}
                {dateFormatter.format(new Date(opportunity.publicationDate))}
              </span>
              {opportunity.departmentCodes.length > 0 && (
                <span className='inline-flex items-center gap-1'>
                  <MapPin className='size-3' />
                  {opportunity.departmentCodes.join(', ')}
                </span>
              )}
              {opportunity.cpvCodes[0] && (
                <span>CPV {opportunity.cpvCodes[0]}</span>
              )}
              {opportunity.cpvCodes.length > 1 && (
                <span>+{opportunity.cpvCodes.length - 1} CPV</span>
              )}
            </div>
          </div>
        )
      },
    },
    {
      accessorKey: 'buyer',
      header: ({ column }) => (
        <DataTableColumnHeader column={column} title='Acheteur' />
      ),
      meta: {
        label: 'Acheteur',
        className: 'min-w-56',
        tdClassName: 'whitespace-normal text-muted-foreground',
      },
      cell: ({ row }) => (
        <span className='line-clamp-2 leading-snug'>{row.original.buyer}</span>
      ),
    },
    {
      accessorKey: 'responseDeadline',
      header: ({ column }) => (
        <DataTableColumnHeader column={column} title='Échéance' />
      ),
      meta: { label: 'Échéance', className: 'min-w-44' },
      cell: ({ row }) => (
        <DeadlineCell
          value={row.original.responseDeadline}
          referenceTime={referenceTime}
          urgentDeadlineDays={urgentDeadlineDays}
        />
      ),
    },
    {
      accessorKey: 'status',
      header: ({ column }) => (
        <DataTableColumnHeader column={column} title='Statut' />
      ),
      meta: { label: 'Statut', className: 'min-w-40' },
      cell: ({ row }) => <OpportunityStatusCell opportunity={row.original} />,
    },
    {
      id: 'actions',
      enableHiding: false,
      enableSorting: false,
      meta: { className: 'w-24 text-right' },
      cell: ({ row }) => (
        <div className='flex justify-end gap-0.5'>
          {row.original.documentUrl &&
            row.original.documentUrl !== row.original.noticeUrl && (
              <Button
                variant='ghost'
                size='icon'
                asChild
                className='group/document'
              >
                <a
                  href={row.original.documentUrl}
                  target='_blank'
                  rel='noreferrer'
                  aria-label={`Ouvrir les documents de l’avis ${row.original.sourceId}`}
                  title='Ouvrir les documents'
                >
                  <FileText className='opacity-60 transition-opacity group-hover/document:opacity-100' />
                </a>
              </Button>
            )}
          <Button variant='ghost' size='icon' asChild className='group/action'>
            <a
              href={row.original.noticeUrl}
              target='_blank'
              rel='noreferrer'
              aria-label={`Ouvrir l’avis ${row.original.sourceId}`}
              title="Ouvrir l'avis"
            >
              <ArrowUpRight className='opacity-60 transition-opacity group-hover/action:opacity-100' />
            </a>
          </Button>
        </div>
      ),
    },
  ]
}
