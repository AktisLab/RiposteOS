import { useMemo, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Check, Search, X } from 'lucide-react'
import {
  addCpvCode,
  cpvCatalogQueryKey,
  findCpvLabel,
  findCpvMatches,
  loadCpvCatalog,
  toCpvPrefix,
} from '@/lib/cpv-catalog'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import {
  Command,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
} from '@/components/ui/command'
import { Label } from '@/components/ui/label'
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from '@/components/ui/popover'

type CpvPickerProps = {
  id: string
  label: string
  description: string
  value: string
  onChange: (value: string) => void
}

export function CpvPicker({
  id,
  label,
  description,
  value,
  onChange,
}: CpvPickerProps) {
  const [open, setOpen] = useState(false)
  const [search, setSearch] = useState('')
  const catalogQuery = useQuery({
    queryKey: cpvCatalogQueryKey,
    queryFn: loadCpvCatalog,
    staleTime: Number.POSITIVE_INFINITY,
  })
  const prefixes = useMemo(() => parsePrefixes(value), [value])
  const matches = useMemo(
    () => findCpvMatches(catalogQuery.data?.items ?? [], search),
    [catalogQuery.data, search]
  )

  function update(nextPrefixes: readonly string[]) {
    onChange(nextPrefixes.join('\n'))
  }

  function select(code: string) {
    update(addCpvCode(prefixes, code))
    setOpen(false)
    setSearch('')
  }

  return (
    <div className='space-y-4 rounded-lg border bg-background p-4'>
      <div className='flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between'>
        <div>
          <div className='flex items-center gap-2'>
            <Label htmlFor={id}>{label}</Label>
            <Badge variant='secondary' className='tabular-nums'>
              {prefixes.length} {prefixes.length === 1 ? 'code' : 'codes'}
            </Badge>
          </div>
          <p className='mt-1 text-xs text-muted-foreground'>{description}</p>
        </div>

        <Popover
          open={open}
          onOpenChange={(nextOpen) => {
            setOpen(nextOpen)
            if (!nextOpen) setSearch('')
          }}
        >
          <PopoverTrigger asChild>
            <Button
              id={id}
              type='button'
              variant='outline'
              size='sm'
              aria-label={`Ajouter dans ${label.toLocaleLowerCase('fr')}`}
              className='cursor-pointer'
              disabled={prefixes.length >= 100}
            >
              <Search />
              Ajouter un CPV
            </Button>
          </PopoverTrigger>
          <PopoverContent
            align='end'
            className='w-96 max-w-[calc(100vw-2rem)] p-0'
          >
            <Command shouldFilter={false}>
              <CommandInput
                value={search}
                onValueChange={setSearch}
                placeholder='Ex. logiciel, maintenance, 722…'
              />
              <CommandList>
                {catalogQuery.isPending ? (
                  <p className='py-6 text-center text-sm text-muted-foreground'>
                    Chargement du référentiel…
                  </p>
                ) : catalogQuery.isError ? (
                  <p className='py-6 text-center text-sm text-destructive'>
                    Le référentiel CPV est indisponible.
                  </p>
                ) : search.trim().length < 2 ? (
                  <p className='py-6 text-center text-sm text-muted-foreground'>
                    Saisissez au moins 2 caractères.
                  </p>
                ) : matches.length === 0 ? (
                  <p className='py-6 text-center text-sm text-muted-foreground'>
                    Aucun code CPV trouvé.
                  </p>
                ) : (
                  <CommandGroup heading='Codes CPV'>
                    {matches.map(([code, resultLabel]) => {
                      const prefix = toCpvPrefix(code)
                      const alreadyCovered = prefixes.some((existing) =>
                        prefix.startsWith(existing)
                      )

                      return (
                        <CommandItem
                          key={code}
                          value={`${code} ${resultLabel}`}
                          onSelect={() => select(code)}
                          disabled={alreadyCovered}
                          className='cursor-pointer items-start py-2.5'
                        >
                          {alreadyCovered ? (
                            <Check className='mt-0.5' />
                          ) : (
                            <Search className='mt-0.5' />
                          )}
                          <span className='min-w-0 flex-1'>
                            <span className='flex items-center gap-2'>
                              <span className='font-mono text-xs'>{code}</span>
                              <Badge variant='outline'>{prefix}*</Badge>
                            </span>
                            <span className='mt-1 block text-xs leading-relaxed text-muted-foreground'>
                              {resultLabel}
                            </span>
                          </span>
                        </CommandItem>
                      )
                    })}
                  </CommandGroup>
                )}
              </CommandList>
            </Command>
          </PopoverContent>
        </Popover>
      </div>

      {prefixes.length === 0 ? (
        <p className='rounded-md border border-dashed px-3 py-4 text-center text-xs text-muted-foreground'>
          Aucun code sélectionné.
        </p>
      ) : (
        <ul className='grid gap-2 md:grid-cols-2 xl:grid-cols-3'>
          {prefixes.map((prefix) => (
            <li
              key={prefix}
              className='relative rounded-md border bg-muted/25 p-3 pr-10'
            >
              <Badge variant='outline' className='font-mono'>
                {prefix}*
              </Badge>
              <p className='mt-2 text-xs leading-relaxed'>
                {catalogQuery.data
                  ? (findCpvLabel(catalogQuery.data.items, prefix) ??
                    'Préfixe CPV personnalisé')
                  : 'Chargement du libellé…'}
              </p>
              <Button
                type='button'
                variant='ghost'
                size='icon'
                className='absolute top-2 right-2 size-7 cursor-pointer text-muted-foreground hover:text-destructive'
                aria-label={`Retirer le préfixe CPV ${prefix}`}
                onClick={() =>
                  update(prefixes.filter((existing) => existing !== prefix))
                }
              >
                <X />
              </Button>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}

function parsePrefixes(value: string) {
  return [
    ...new Set(
      value
        .split(/\r?\n/)
        .map((item) => item.trim())
        .filter(Boolean)
    ),
  ]
}
