import { useEffect, useMemo, useState } from 'react'
import { getRouteApi } from '@tanstack/react-router'
import {
  type OnChangeFn,
  type SortingState,
  type VisibilityState,
  flexRender,
  getCoreRowModel,
  useReactTable,
} from '@tanstack/react-table'
import {
  CircleCheck,
  CircleHelp,
  CircleX,
  Inbox,
  MapPin,
  Sparkles,
} from 'lucide-react'
import { cn } from '@/lib/utils'
import { useTableUrlState } from '@/hooks/use-table-url-state'
import { Button } from '@/components/ui/button'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { DataTablePagination, DataTableToolbar } from '@/components/data-table'
import { type Opportunity } from '../api'
import { type OpportunitySortField } from '../gridify'
import { createOpportunityColumns } from './opportunities-columns'
import { OpportunityAdvancedFilters } from './opportunity-advanced-filters'

const route = getRouteApi('/_authenticated/opportunities')

const deadlineOptions = [
  { label: 'Ouverte', value: 'open', icon: CircleCheck },
  { label: 'Clôturée', value: 'closed', icon: CircleX },
  { label: 'Non renseignée', value: 'unknown', icon: CircleHelp },
]
const statusOptions = [
  { label: 'À qualifier', value: 'ToQualify', icon: Inbox },
  { label: 'Retenue', value: 'Retained', icon: CircleCheck },
  { label: 'Écartée', value: 'Dismissed', icon: CircleX },
]

type OpportunitiesTableProps = {
  opportunities: Opportunity[]
  totalCount: number
  referenceTime: number
  highRelevanceThreshold: number
  preferredDepartmentCodes: string[]
  urgentDeadlineDays: number
}

export function OpportunitiesTable({
  opportunities,
  totalCount,
  referenceTime,
  highRelevanceThreshold,
  preferredDepartmentCodes,
  urgentDeadlineDays,
}: OpportunitiesTableProps) {
  const search = route.useSearch()
  const navigate = route.useNavigate()
  const columns = useMemo(
    () =>
      createOpportunityColumns(
        referenceTime,
        urgentDeadlineDays,
        highRelevanceThreshold
      ),
    [referenceTime, urgentDeadlineDays, highRelevanceThreshold]
  )
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
      { columnId: 'responseDeadline', searchKey: 'deadline', type: 'array' },
      { columnId: 'status', searchKey: 'status', type: 'array' },
    ],
  })
  const sorting: SortingState = [
    {
      id: search.sort ?? 'publicationDate',
      desc: (search.direction ?? 'desc') === 'desc',
    },
  ]
  const onSortingChange: OnChangeFn<SortingState> = (updater) => {
    const next = typeof updater === 'function' ? updater(sorting) : updater
    const sort = next[0]
    const sortId =
      (sort?.id as OpportunitySortField | undefined) ?? 'publicationDate'
    navigate({
      search: (previous) => ({
        ...previous,
        page: undefined,
        sort: sortId,
        direction: sort?.desc ? 'desc' : 'asc',
      }),
    })
  }
  const pageCount = Math.max(1, Math.ceil(totalCount / pagination.pageSize))

  // eslint-disable-next-line react-hooks/incompatible-library
  const table = useReactTable({
    data: opportunities,
    columns,
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

  const externalFilters = Boolean(
    search.highRelevance ||
    search.preferredTerritory ||
    search.buyer ||
    search.department ||
    search.cpv
  )
  const isFiltered =
    Boolean(globalFilter) || columnFilters.length > 0 || externalFilters
  const onlyToQualify =
    search.status?.length === 1 && search.status[0] === 'ToQualify'

  function updateSearch(values: Record<string, unknown>) {
    navigate({
      search: (previous) => ({ ...previous, page: undefined, ...values }),
    })
  }

  return (
    <div className='flex flex-col'>
      <div className='space-y-3 border-b px-4 py-3'>
        <DataTableToolbar
          table={table}
          searchPlaceholder='Rechercher un avis, un acheteur…'
          filters={[
            {
              columnId: 'responseDeadline',
              title: 'Échéance',
              options: deadlineOptions,
            },
            { columnId: 'status', title: 'Statut', options: statusOptions },
          ]}
          isExternallyFiltered={externalFilters}
          onReset={() =>
            updateSearch({
              filter: undefined,
              source: undefined,
              deadline: undefined,
              status: undefined,
              highRelevance: undefined,
              preferredTerritory: undefined,
              buyer: undefined,
              department: undefined,
              cpv: undefined,
            })
          }
        >
          <OpportunityAdvancedFilters
            key={`${search.buyer}-${search.department}-${search.cpv}`}
            filters={{
              buyer: search.buyer ?? '',
              department: search.department ?? '',
              cpv: search.cpv ?? '',
            }}
            onApply={(filters) => updateSearch(filters)}
          />
        </DataTableToolbar>
        <div className='flex flex-wrap items-center gap-2'>
          <span className='text-xs font-medium text-muted-foreground'>
            Vues rapides
          </span>
          <QuickFilter
            active={search.highRelevance ?? false}
            onClick={() =>
              updateSearch({
                highRelevance: !search.highRelevance || undefined,
              })
            }
          >
            <Sparkles />
            Très pertinentes ≥ {highRelevanceThreshold}
          </QuickFilter>
          {preferredDepartmentCodes.length > 0 && (
            <QuickFilter
              active={search.preferredTerritory ?? false}
              onClick={() =>
                updateSearch({
                  preferredTerritory: !search.preferredTerritory || undefined,
                })
              }
            >
              <MapPin />
              Territoires prioritaires
            </QuickFilter>
          )}
          <QuickFilter
            active={onlyToQualify}
            onClick={() =>
              updateSearch({
                status: onlyToQualify ? undefined : ['ToQualify'],
              })
            }
          >
            <Inbox />À qualifier
          </QuickFilter>
        </div>
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
              <TableRow key={row.id}>
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
                className='h-32 text-center text-muted-foreground'
              >
                {isFiltered
                  ? 'Aucune opportunité ne correspond à ces filtres.'
                  : 'Aucun avis importé. Lancez le premier import BOAMP.'}
              </TableCell>
            </TableRow>
          )}
        </TableBody>
      </Table>
      <DataTablePagination table={table} className='border-t px-4 py-3' />
    </div>
  )
}

function QuickFilter({
  active,
  onClick,
  children,
}: {
  active: boolean
  onClick: () => void
  children: React.ReactNode
}) {
  return (
    <Button
      variant={active ? 'secondary' : 'ghost'}
      size='sm'
      className='h-7 cursor-pointer px-2 text-xs'
      onClick={onClick}
      aria-pressed={active}
    >
      {children}
    </Button>
  )
}
