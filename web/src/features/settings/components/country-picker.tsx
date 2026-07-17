import { useMemo, useState } from 'react'
import { Check, ChevronsUpDown, X } from 'lucide-react'
import { cn } from '@/lib/utils'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import {
  Command,
  CommandEmpty,
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
import { parseCountryCodes } from '../country-codes'
import { tedCountries } from '../data/ted-countries'

type CountryPickerProps = {
  value: string
  onChange: (value: string) => void
}

export function CountryPicker({ value, onChange }: CountryPickerProps) {
  const [open, setOpen] = useState(false)
  const selectedCodes = useMemo(() => parseCountryCodes(value), [value])
  const selected = new Set(selectedCodes)

  function update(codes: readonly string[]) {
    onChange(codes.join('\n'))
  }

  function toggle(code: string) {
    update(
      selected.has(code)
        ? selectedCodes.filter((current) => current !== code)
        : [...selectedCodes, code]
    )
  }

  return (
    <div className='space-y-4'>
      <div className='flex items-start justify-between gap-3'>
        <div>
          <div className='flex items-center gap-2'>
            <Label htmlFor='allowed-countries'>Pays collectés</Label>
            <Badge variant='secondary' className='tabular-nums'>
              {selectedCodes.length || 'Tous'}
            </Badge>
          </div>
          <p className='mt-1 text-xs leading-relaxed text-muted-foreground'>
            La France est sélectionnée par défaut. Retirez-la pour collecter
            explicitement tous les pays TED.
          </p>
        </div>

        <Popover open={open} onOpenChange={setOpen}>
          <PopoverTrigger asChild>
            <Button
              id='allowed-countries'
              type='button'
              variant='outline'
              size='sm'
              className='shrink-0 cursor-pointer'
              aria-label='Choisir les pays collectés'
            >
              Choisir
              <ChevronsUpDown aria-hidden='true' />
            </Button>
          </PopoverTrigger>
          <PopoverContent
            align='end'
            className='w-80 max-w-[calc(100vw-2rem)] p-0'
          >
            <Command>
              <CommandInput placeholder='Rechercher un pays…' />
              <CommandList>
                <CommandEmpty>Aucun pays trouvé.</CommandEmpty>
                <CommandGroup>
                  {tedCountries.map((country) => {
                    const isSelected = selected.has(country.code)

                    return (
                      <CommandItem
                        key={country.code}
                        value={`${country.label} ${country.code}`}
                        onSelect={() => toggle(country.code)}
                        className='cursor-pointer'
                      >
                        <span
                          className={cn(
                            'flex size-4 items-center justify-center rounded-sm border',
                            isSelected
                              ? 'border-primary bg-primary text-primary-foreground'
                              : 'border-muted-foreground/40'
                          )}
                        >
                          {isSelected && (
                            <Check aria-hidden='true' className='size-3' />
                          )}
                        </span>
                        <span className='flex-1'>{country.label}</span>
                        <span className='font-mono text-xs text-muted-foreground'>
                          {country.code}
                        </span>
                      </CommandItem>
                    )
                  })}
                </CommandGroup>
              </CommandList>
            </Command>
          </PopoverContent>
        </Popover>
      </div>

      {selectedCodes.length > 0 && (
        <div className='flex flex-wrap gap-2'>
          {selectedCodes.map((code) => {
            const country = tedCountries.find((item) => item.code === code)

            return (
              <Badge key={code} variant='outline' className='gap-1.5 py-1'>
                {country?.label ?? code}
                <button
                  type='button'
                  onClick={() => toggle(code)}
                  className='cursor-pointer rounded-sm text-muted-foreground hover:text-destructive focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none'
                  aria-label={`Retirer ${country?.label ?? code}`}
                >
                  <X aria-hidden='true' className='size-3' />
                </button>
              </Badge>
            )
          })}
        </div>
      )}
    </div>
  )
}
