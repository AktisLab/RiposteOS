import { useEffect, useState } from 'react'
import { getRouteApi } from '@tanstack/react-router'
import {
  type OnChangeFn,
  type SortingState,
  type VisibilityState,
  flexRender,
  getCoreRowModel,
  useReactTable,
} from '@tanstack/react-table'
import { CircleCheck, CircleHelp, CircleX, FileInput } from 'lucide-react'
import { cn } from '@/lib/utils'
import { useTableUrlState } from '@/hooks/use-table-url-state'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { DataTablePagination, DataTableToolbar } from '@/components/data-table'
import {
  BoampLogo,
  PlaceLogo,
  TedLogo,
} from '@/components/sourcing-source-logo'
import { type Consultation } from '../api'
import { type ConsultationSortField } from '../gridify'
import { consultationColumns } from './consultations-columns'

const route = getRouteApi('/_authenticated/consultations/')

const sourceOptions = [
  { label: 'BOAMP', value: 'boamp', icon: BoampLogo },
  { label: 'TED', value: 'ted', icon: TedLogo },
  { label: 'PLACE', value: 'place', icon: PlaceLogo },
  { label: 'Manuelle', value: 'manual', icon: FileInput },
]

const deadlineOptions = [
  { label: 'À venir', value: 'open', icon: CircleCheck },
  { label: 'Dépassée', value: 'closed', icon: CircleX },
  { label: 'Non renseignée', value: 'unknown', icon: CircleHelp },
]

type ConsultationsTableProps = {
  consultations: Consultation[]
  totalCount: number
}

export function ConsultationsTable({
  consultations,
  totalCount,
}: ConsultationsTableProps) {
  const search = route.useSearch()
  const navigate = route.useNavigate()
  const [columnVisibility, setColumnVisibility] = useState<VisibilityState>({})
  const {
    globalFilter,
    onGlobalFilterChange,
    columnFilters,
    onColumnFiltersChange,
    pagination,
    onPaginationChange,
    ensurePageInRange,
  } = useTableUrlState({
    search,
    navigate,
    pagination: { defaultPage: 1, defaultPageSize: 20 },
    globalFilter: { key: 'filter', trim: false },
    columnFilters: [
      { columnId: 'source', searchKey: 'source', type: 'array' },
      { columnId: 'responseDeadline', searchKey: 'deadline', type: 'array' },
    ],
  })
  const sorting: SortingState = [
    {
      id: search.sort ?? 'responseDeadline',
      desc: (search.direction ?? 'asc') === 'desc',
    },
  ]
  const onSortingChange: OnChangeFn<SortingState> = (updater) => {
    const next = typeof updater === 'function' ? updater(sorting) : updater
    const sort = next[0]
    navigate({
      search: (previous) => ({
        ...previous,
        page: undefined,
        sort:
          (sort?.id as ConsultationSortField | undefined) ?? 'responseDeadline',
        direction: sort?.desc ? 'desc' : 'asc',
      }),
    })
  }
  const pageCount = Math.max(1, Math.ceil(totalCount / pagination.pageSize))

  // eslint-disable-next-line react-hooks/incompatible-library
  const table = useReactTable({
    data: consultations,
    columns: consultationColumns,
    pageCount,
    state: {
      sorting,
      columnVisibility,
      columnFilters,
      globalFilter,
      pagination,
    },
    manualFiltering: true,
    manualPagination: true,
    manualSorting: true,
    enableMultiSort: false,
    getRowId: (row) => row.id,
    getCoreRowModel: getCoreRowModel(),
    onSortingChange,
    onColumnVisibilityChange: setColumnVisibility,
    onPaginationChange,
    onGlobalFilterChange,
    onColumnFiltersChange,
  })

  useEffect(() => ensurePageInRange(pageCount), [ensurePageInRange, pageCount])

  const isFiltered = Boolean(globalFilter) || columnFilters.length > 0

  return (
    <div className='flex flex-col'>
      <div className='border-b px-4 py-3'>
        <DataTableToolbar
          table={table}
          searchPlaceholder='Rechercher une consultation, un acheteur…'
          filters={[
            { columnId: 'source', title: 'Source', options: sourceOptions },
            {
              columnId: 'responseDeadline',
              title: 'Échéance',
              options: deadlineOptions,
            },
          ]}
        />
      </div>
      <Table
        className='min-w-6xl'
        containerClassName='max-h-[calc(100svh-18rem)] min-h-80'
      >
        <TableHeader className='sticky top-0 z-10 bg-background shadow-xs'>
          {table.getHeaderGroups().map((headerGroup) => (
            <TableRow key={headerGroup.id} className='hover:bg-transparent'>
              {headerGroup.headers.map((header) => (
                <TableHead
                  key={header.id}
                  colSpan={header.colSpan}
                  className={cn(
                    header.column.columnDef.meta?.className,
                    header.column.columnDef.meta?.thClassName
                  )}
                >
                  {header.isPlaceholder
                    ? null
                    : flexRender(
                        header.column.columnDef.header,
                        header.getContext()
                      )}
                </TableHead>
              ))}
            </TableRow>
          ))}
        </TableHeader>
        <TableBody>
          {table.getRowModel().rows.length > 0 ? (
            table.getRowModel().rows.map((row) => (
              <TableRow
                key={row.id}
                tabIndex={0}
                aria-label={`Ouvrir la consultation ${row.original.title}`}
                className='group cursor-pointer border-b even:bg-muted/15 hover:bg-muted/40 focus-visible:bg-muted/40 focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none focus-visible:ring-inset'
                onClick={(event) => {
                  if (isInteractiveTarget(event.target)) return
                  void navigate({
                    to: '/consultations/$consultationId',
                    params: { consultationId: row.original.id },
                  })
                }}
                onKeyDown={(event) => {
                  if (
                    isInteractiveTarget(event.target) ||
                    (event.key !== 'Enter' && event.key !== ' ')
                  ) {
                    return
                  }
                  event.preventDefault()
                  void navigate({
                    to: '/consultations/$consultationId',
                    params: { consultationId: row.original.id },
                  })
                }}
              >
                {row.getVisibleCells().map((cell) => (
                  <TableCell
                    key={cell.id}
                    className={cn(
                      cell.column.columnDef.meta?.className,
                      cell.column.columnDef.meta?.tdClassName
                    )}
                  >
                    {flexRender(cell.column.columnDef.cell, cell.getContext())}
                  </TableCell>
                ))}
              </TableRow>
            ))
          ) : (
            <TableRow>
              <TableCell
                colSpan={table.getVisibleLeafColumns().length}
                className='h-40 text-center text-muted-foreground'
              >
                {isFiltered
                  ? 'Aucune consultation ne correspond à ces filtres.'
                  : 'Aucune consultation à qualifier. Étudiez une opportunité depuis le sourcing ou ajoutez une consultation manuellement.'}
              </TableCell>
            </TableRow>
          )}
        </TableBody>
      </Table>
      <DataTablePagination table={table} className='border-t px-4 py-3' />
    </div>
  )
}

function isInteractiveTarget(target: EventTarget | null) {
  return (
    target instanceof Element &&
    Boolean(target.closest('a, button, input, select, [role="combobox"]'))
  )
}
