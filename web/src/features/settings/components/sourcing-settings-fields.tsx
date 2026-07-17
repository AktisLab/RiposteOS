import { useState, type KeyboardEvent } from 'react'
import { Clock3, Code2, Plus, X } from 'lucide-react'
import { findSourcingSource } from '@/lib/sourcing-source'
import { cn } from '@/lib/utils'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { LearnMore } from '@/components/learn-more'
import { SourcingSourceLogo } from '@/components/sourcing-source-logo'
import {
  findSchedulePreset,
  sourcingSchedulePresets,
} from '../sourcing-schedule'

export function SettingsSection({
  title,
  description,
  hint,
  columns = 'md:grid-cols-2',
  children,
}: {
  title: string
  description: string
  hint?: string
  columns?: string
  children: React.ReactNode
}) {
  return (
    <section className='space-y-6'>
      <div className='border-b pb-4'>
        <div className='flex items-center gap-2'>
          <h2 className='text-base font-semibold'>{title}</h2>
          {hint && <HelpHint label={`Aide sur ${title}`}>{hint}</HelpHint>}
        </div>
        <p className='mt-1 text-sm leading-relaxed text-muted-foreground'>
          {description}
        </p>
      </div>
      <div className={`grid gap-6 ${columns}`}>{children}</div>
    </section>
  )
}

export function TermsField({
  id,
  label,
  values,
  onChange,
  placeholder,
  hint,
}: {
  id: string
  label: string
  values: string[]
  onChange: (values: string[]) => void
  placeholder: string
  hint?: string
}) {
  const [draft, setDraft] = useState('')
  const normalizedDraft = draft.trim()
  const alreadyExists = values.some(
    (value) =>
      value.toLocaleLowerCase('fr') === normalizedDraft.toLocaleLowerCase('fr')
  )
  const canAdd =
    normalizedDraft.length > 0 && values.length < 100 && !alreadyExists

  function addTerm() {
    if (!canAdd) return
    onChange([...values, normalizedDraft])
    setDraft('')
  }

  function handleKeyDown(event: KeyboardEvent<HTMLInputElement>) {
    if (event.key !== 'Enter') return
    event.preventDefault()
    addTerm()
  }

  return (
    <div className='space-y-3'>
      <div className='flex items-center justify-between gap-3'>
        <div className='flex items-center gap-2'>
          <Label htmlFor={id}>{label}</Label>
          {hint && <HelpHint label={`Aide sur ${label}`}>{hint}</HelpHint>}
        </div>
        <Badge variant='secondary' className='tabular-nums'>
          {values.length} {values.length === 1 ? 'terme' : 'termes'}
        </Badge>
      </div>

      <div className='flex gap-2'>
        <Input
          id={id}
          value={draft}
          onChange={(event) => setDraft(event.target.value)}
          onKeyDown={handleKeyDown}
          maxLength={100}
          placeholder={placeholder}
          aria-describedby={`${id}-help`}
        />
        <Button
          type='button'
          variant='outline'
          onClick={addTerm}
          disabled={!canAdd}
          className='cursor-pointer'
        >
          <Plus aria-hidden='true' />
          Ajouter
        </Button>
      </div>
      <p id={`${id}-help`} className='text-xs text-muted-foreground'>
        Saisissez un terme ou une expression, puis appuyez sur Entrée.
      </p>

      {values.length === 0 ? (
        <p className='rounded-md border border-dashed px-3 py-5 text-center text-xs text-muted-foreground'>
          Aucun terme ajouté.
        </p>
      ) : (
        <ul className='flex max-h-44 flex-wrap gap-2 overflow-y-auto rounded-md bg-muted/35 p-3'>
          {values.map((value) => (
            <li key={value}>
              <Badge variant='outline' className='gap-1.5 bg-background py-1'>
                {value}
                <button
                  type='button'
                  onClick={() =>
                    onChange(values.filter((item) => item !== value))
                  }
                  className='cursor-pointer rounded-sm text-muted-foreground hover:text-destructive focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none'
                  aria-label={`Retirer ${value}`}
                >
                  <X aria-hidden='true' className='size-3' />
                </button>
              </Badge>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}

export function ScheduleField({
  source,
  value,
  onChange,
}: {
  source: string
  value: string
  onChange: (value: string) => void
}) {
  const currentPreset = findSchedulePreset(value)
  const [advanced, setAdvanced] = useState(currentPreset === null)
  const sourceDescription = findSourcingSource(source)?.description

  function switchToSimpleSchedule() {
    if (currentPreset === null) onChange(sourcingSchedulePresets[0].cron)
    setAdvanced(false)
  }

  return (
    <div className='rounded-xl border bg-card p-5 shadow-xs'>
      <div className='flex items-start gap-3'>
        <span className='flex size-9 shrink-0 items-center justify-center rounded-lg border bg-background'>
          <SourcingSourceLogo source={source} className='size-6' />
        </span>
        <div className='min-w-0 flex-1'>
          <div className='flex items-center justify-between gap-3'>
            <h3 className='font-semibold'>{source}</h3>
            {advanced && <Badge variant='outline'>Mode avancé</Badge>}
          </div>
          {sourceDescription && (
            <p className='mt-0.5 text-xs text-muted-foreground'>
              {sourceDescription}
            </p>
          )}
        </div>
      </div>

      {advanced ? (
        <div className='mt-5 space-y-2 rounded-lg bg-muted/40 p-3'>
          <Label htmlFor={`${source.toLocaleLowerCase('fr')}-cron`}>
            Expression CRON
          </Label>
          <Input
            id={`${source.toLocaleLowerCase('fr')}-cron`}
            value={value}
            onChange={(event) => onChange(event.target.value)}
            maxLength={100}
            className='font-mono'
            required
          />
          <p className='text-xs text-muted-foreground'>
            Format standard à 5 champs, interprété en UTC.
          </p>
        </div>
      ) : (
        <div className='mt-5 space-y-2 rounded-lg bg-muted/40 p-3'>
          <Label htmlFor={`${source.toLocaleLowerCase('fr')}-schedule`}>
            Rythme de collecte
          </Label>
          <Select
            value={currentPreset?.value ?? sourcingSchedulePresets[0].value}
            onValueChange={(presetValue) => {
              const preset = sourcingSchedulePresets.find(
                (item) => item.value === presetValue
              )
              if (preset) onChange(preset.cron)
            }}
          >
            <SelectTrigger
              id={`${source.toLocaleLowerCase('fr')}-schedule`}
              className='w-full cursor-pointer bg-background'
              aria-label={`Rythme de collecte ${source}`}
            >
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {sourcingSchedulePresets.map((preset) => (
                <SelectItem
                  key={preset.value}
                  value={preset.value}
                  className='cursor-pointer'
                >
                  {preset.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          <p className='flex items-center gap-1.5 text-xs text-muted-foreground'>
            <Clock3 aria-hidden='true' className='size-3.5' />
            {currentPreset?.description ??
              sourcingSchedulePresets[0].description}
          </p>
        </div>
      )}

      <Button
        type='button'
        variant='link'
        size='sm'
        className='mt-3 h-auto cursor-pointer px-0 text-xs'
        onClick={() =>
          advanced ? switchToSimpleSchedule() : setAdvanced(true)
        }
      >
        <Code2 aria-hidden='true' />
        {advanced ? 'Utiliser un rythme simple' : 'Réglage CRON avancé'}
      </Button>
    </div>
  )
}

export function NumberField({
  label,
  value,
  onChange,
  hint,
  unit,
  min = 0,
  max = 100,
}: {
  label: string
  value: number
  onChange: (value: number) => void
  hint?: string
  unit?: string
  min?: number
  max?: number
}) {
  const id = `sourcing-${label.toLowerCase().replace(/\s+/g, '-')}`

  return (
    <div className='space-y-2'>
      <div className='flex items-center gap-2'>
        <Label htmlFor={id}>{label}</Label>
        {hint && <HelpHint label={`Aide sur ${label}`}>{hint}</HelpHint>}
      </div>
      <div className='relative'>
        <Input
          id={id}
          type='number'
          min={min}
          max={max}
          value={value}
          onChange={(event) => onChange(event.target.valueAsNumber)}
          className={cn('font-mono tabular-nums', unit && 'pr-16')}
          required
        />
        {unit && (
          <span className='pointer-events-none absolute inset-y-0 right-3 flex items-center text-xs text-muted-foreground'>
            {unit}
          </span>
        )}
      </div>
    </div>
  )
}

function HelpHint({
  label,
  children,
}: {
  label: string
  children: React.ReactNode
}) {
  return (
    <LearnMore
      triggerProps={{ 'aria-label': label, className: 'cursor-help' }}
      contentProps={{ className: 'leading-relaxed' }}
    >
      {children}
    </LearnMore>
  )
}
