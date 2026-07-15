import { type ColumnDef } from '@tanstack/react-table'
import { ArrowUpRight, MapPin } from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { DataTableColumnHeader } from '@/components/data-table'
import { type Opportunity } from '../api'
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
      accessorKey: 'matchScore',
      header: ({ column }) => (
        <DataTableColumnHeader column={column} title='Pertinence' />
      ),
      meta: { label: 'Pertinence', className: 'w-32' },
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
      cell: ({ row }) => (
        <div className='space-y-2'>
          <p className='line-clamp-2 leading-snug font-medium'>
            {row.original.title}
          </p>
          <div className='flex flex-wrap items-center gap-x-2 gap-y-1 text-xs text-muted-foreground'>
            <Badge variant='outline'>{row.original.source.toUpperCase()}</Badge>
            <span>{row.original.sourceId}</span>
            <span>
              Publié le{' '}
              {dateFormatter.format(new Date(row.original.publicationDate))}
            </span>
            {row.original.departmentCodes.length > 0 && (
              <span className='inline-flex items-center gap-1'>
                <MapPin className='size-3' />
                {row.original.departmentCodes.join(', ')}
              </span>
            )}
            {row.original.cpvCodes.slice(0, 2).map((code) => (
              <span key={code}>CPV {code}</span>
            ))}
          </div>
        </div>
      ),
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
      meta: { className: 'w-14 text-right' },
      cell: ({ row }) => (
        <Button variant='ghost' size='icon' asChild>
          <a
            href={row.original.noticeUrl}
            target='_blank'
            rel='noreferrer'
            aria-label={`Ouvrir l’avis ${row.original.sourceId}`}
          >
            <ArrowUpRight />
          </a>
        </Button>
      ),
    },
  ]
}
