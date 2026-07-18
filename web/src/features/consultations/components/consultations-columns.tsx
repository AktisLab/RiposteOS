import { Link } from '@tanstack/react-router'
import { type ColumnDef } from '@tanstack/react-table'
import { ArrowRight, FileText } from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { DataTableColumnHeader } from '@/components/data-table'
import { SourcingSourceLogo } from '@/components/sourcing-source-logo'
import { type Consultation } from '../api'
import {
  formatConsultationDeadline,
  nextConsultationAction,
} from '../presentation'

export const consultationColumns: ColumnDef<Consultation>[] = [
  {
    accessorKey: 'title',
    header: ({ column }) => (
      <DataTableColumnHeader column={column} title='Consultation' />
    ),
    meta: {
      label: 'Consultation',
      className: 'w-[34%] min-w-80',
      tdClassName: 'whitespace-normal py-4',
    },
    cell: ({ row }) => (
      <div className='space-y-1'>
        <Link
          to='/consultations/$consultationId'
          params={{ consultationId: row.original.id }}
          className='line-clamp-2 font-semibold hover:text-primary hover:underline focus-visible:rounded-sm focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none'
        >
          {row.original.title}
        </Link>
        {row.original.sourceId && (
          <p className='text-xs text-muted-foreground'>
            Référence {row.original.sourceId}
          </p>
        )}
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
    cell: ({ row }) => (
      <span className='line-clamp-2 leading-snug'>{row.original.buyer}</span>
    ),
  },
  {
    accessorKey: 'source',
    header: ({ column }) => (
      <DataTableColumnHeader column={column} title='Source' />
    ),
    meta: { label: 'Source', className: 'min-w-32' },
    cell: ({ row }) =>
      row.original.source ? (
        <Badge variant='outline' className='gap-1.5'>
          <SourcingSourceLogo
            source={row.original.source}
            className='size-3.5'
          />
          {row.original.source.toUpperCase()}
        </Badge>
      ) : (
        <span className='text-sm text-muted-foreground'>Manuelle</span>
      ),
  },
  {
    accessorKey: 'responseDeadline',
    header: ({ column }) => (
      <DataTableColumnHeader column={column} title='Échéance' />
    ),
    meta: { label: 'Échéance', className: 'min-w-44' },
    cell: ({ row }) => (
      <span className='text-sm'>
        {formatConsultationDeadline(row.original.responseDeadline)}
      </span>
    ),
  },
  {
    accessorKey: 'documentCount',
    enableSorting: false,
    header: 'Documents',
    meta: { label: 'Documents', className: 'min-w-28' },
    cell: ({ row }) => (
      <span className='inline-flex items-center gap-1.5 tabular-nums'>
        <FileText className='size-4 text-muted-foreground' />
        {row.original.documentCount}
      </span>
    ),
  },
  {
    id: 'nextAction',
    enableSorting: false,
    header: 'Prochaine action',
    meta: { label: 'Prochaine action', className: 'min-w-48' },
    cell: ({ row }) => (
      <span className='text-sm font-medium'>
        {nextConsultationAction(row.original.documentCount)}
      </span>
    ),
  },
  {
    id: 'actions',
    enableHiding: false,
    enableSorting: false,
    header: 'Actions',
    meta: { className: 'w-20 text-right' },
    cell: ({ row }) => (
      <Button variant='ghost' size='icon' asChild>
        <Link
          to='/consultations/$consultationId'
          params={{ consultationId: row.original.id }}
          aria-label={`Ouvrir la consultation ${row.original.title}`}
          title='Ouvrir la consultation'
        >
          <ArrowRight />
        </Link>
      </Button>
    ),
  },
]
