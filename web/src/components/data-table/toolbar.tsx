import { Cross2Icon } from '@radix-ui/react-icons'
import { type Table } from '@tanstack/react-table'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { DataTableFacetedFilter } from './faceted-filter'
import { DataTableViewOptions } from './view-options'

type DataTableToolbarProps<TData> = {
  table: Table<TData>
  searchPlaceholder?: string
  searchKey?: string
  filters?: {
    columnId: string
    title: string
    options: {
      label: string
      value: string
      icon?: React.ComponentType<{ className?: string }>
    }[]
  }[]
  children?: React.ReactNode
  isExternallyFiltered?: boolean
  onReset?: () => void
}

export function DataTableToolbar<TData>({
  table,
  searchPlaceholder = 'Filtrer…',
  searchKey,
  filters = [],
  children,
  isExternallyFiltered,
  onReset,
}: DataTableToolbarProps<TData>) {
  const isFiltered =
    table.getState().columnFilters.length > 0 ||
    table.getState().globalFilter ||
    isExternallyFiltered

  return (
    <div className='flex flex-wrap items-center justify-between gap-2'>
      <div className='flex w-full flex-1 flex-col-reverse items-start gap-2 sm:flex-row sm:flex-wrap sm:items-center'>
        {searchKey ? (
          <Input
            placeholder={searchPlaceholder}
            value={
              (table.getColumn(searchKey)?.getFilterValue() as string) ?? ''
            }
            onChange={(event) =>
              table.getColumn(searchKey)?.setFilterValue(event.target.value)
            }
            className='h-8 w-full sm:w-64'
          />
        ) : (
          <Input
            placeholder={searchPlaceholder}
            value={table.getState().globalFilter ?? ''}
            onChange={(event) => table.setGlobalFilter(event.target.value)}
            className='h-8 w-full sm:w-64'
          />
        )}
        <div className='flex flex-wrap gap-2'>
          {filters.map((filter) => {
            const column = table.getColumn(filter.columnId)
            if (!column) return null
            return (
              <DataTableFacetedFilter
                key={filter.columnId}
                column={column}
                title={filter.title}
                options={filter.options}
              />
            )
          })}
          {children}
        </div>
        {isFiltered && (
          <Button
            variant='ghost'
            onClick={() => {
              if (onReset) {
                onReset()
              } else {
                table.resetColumnFilters()
                table.setGlobalFilter('')
              }
            }}
            className='h-8 px-2 lg:px-3'
          >
            Réinitialiser
            <Cross2Icon className='ms-2 h-4 w-4' />
          </Button>
        )}
      </div>
      <DataTableViewOptions table={table} />
    </div>
  )
}
