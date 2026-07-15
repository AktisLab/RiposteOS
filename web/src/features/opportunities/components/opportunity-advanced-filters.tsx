import { useState } from 'react'
import { ListFilter } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from '@/components/ui/popover'

type Filters = {
  buyer: string
  department: string
  cpv: string
}

export function OpportunityAdvancedFilters({
  filters,
  onApply,
}: {
  filters: Filters
  onApply: (filters: Filters) => void
}) {
  const [draft, setDraft] = useState(filters)

  return (
    <Popover>
      <PopoverTrigger asChild>
        <Button variant='outline' size='sm' className='h-8 border-dashed'>
          <ListFilter />
          Filtres métier
        </Button>
      </PopoverTrigger>
      <PopoverContent align='start' className='w-80'>
        <form
          className='space-y-4'
          onSubmit={(event) => {
            event.preventDefault()
            onApply(draft)
          }}
        >
          <div className='space-y-1'>
            <h4 className='font-medium'>Filtres métier</h4>
            <p className='text-sm text-muted-foreground'>
              Recherche exacte sur les données normalisées.
            </p>
          </div>
          <FilterInput
            id='buyer-filter'
            label='Acheteur contient'
            value={draft.buyer}
            onChange={(buyer) => setDraft((current) => ({ ...current, buyer }))}
          />
          <FilterInput
            id='department-filter'
            label='Département'
            value={draft.department}
            maxLength={3}
            placeholder='69'
            onChange={(department) =>
              setDraft((current) => ({ ...current, department }))
            }
          />
          <FilterInput
            id='cpv-filter'
            label='Préfixe CPV'
            value={draft.cpv}
            maxLength={8}
            placeholder='722'
            onChange={(cpv) => setDraft((current) => ({ ...current, cpv }))}
          />
          <div className='flex justify-end gap-2'>
            <Button
              type='button'
              variant='ghost'
              size='sm'
              onClick={() => {
                const empty = { buyer: '', department: '', cpv: '' }
                setDraft(empty)
                onApply(empty)
              }}
            >
              Effacer
            </Button>
            <Button type='submit' size='sm'>
              Appliquer
            </Button>
          </div>
        </form>
      </PopoverContent>
    </Popover>
  )
}

function FilterInput({
  id,
  label,
  value,
  onChange,
  placeholder,
  maxLength,
}: {
  id: string
  label: string
  value: string
  onChange: (value: string) => void
  placeholder?: string
  maxLength?: number
}) {
  return (
    <div className='space-y-2'>
      <Label htmlFor={id}>{label}</Label>
      <Input
        id={id}
        value={value}
        onChange={(event) => onChange(event.target.value)}
        placeholder={placeholder}
        maxLength={maxLength}
      />
    </div>
  )
}
