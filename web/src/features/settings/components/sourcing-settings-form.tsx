import { useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Loader2, Save } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { Textarea } from '@/components/ui/textarea'
import { LearnMore } from '@/components/learn-more'
import {
  type SourcingSettings,
  sourcingSettingsQueryKey,
  updateSourcingSettings,
} from '../api'
import { CpvPicker } from './cpv-picker'

const listFields = [
  'keywords',
  'excludedKeywords',
  'positiveSignals',
  'negativeSignals',
  'preferredDepartmentCodes',
  'cpvWhitelistPrefixes',
  'cpvWatchPrefixes',
  'cpvExcludedPrefixes',
] as const

type ListField = (typeof listFields)[number]
type FormState = Omit<SourcingSettings, ListField | 'updatedAt'> &
  Record<ListField, string>

export function SourcingSettingsForm({
  settings,
}: {
  settings: SourcingSettings
}) {
  const queryClient = useQueryClient()
  const [form, setForm] = useState<FormState>(() => createFormState(settings))
  const mutation = useMutation({
    mutationFn: updateSourcingSettings,
    onSuccess: (updatedSettings) => {
      queryClient.setQueryData(sourcingSettingsQueryKey, updatedSettings)
      toast.success('Profil de sourcing enregistré', {
        description: 'Il sera appliqué dès la prochaine synchronisation.',
      })
    },
    onError: (error) => toast.error(error.message),
  })
  const parsedLists = Object.fromEntries(
    listFields.map((field) => [field, parseLines(form[field])])
  ) as Record<ListField, string[]>

  function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    mutation.mutate({
      ...form,
      ...parsedLists,
    })
  }

  function setList(field: ListField, value: string) {
    setForm((current) => ({ ...current, [field]: value }))
  }

  function setNumber(
    field: Exclude<keyof FormState, ListField>,
    value: number
  ) {
    setForm((current) => ({ ...current, [field]: value }))
  }

  const invalidLists = listFields.some(
    (field) => parsedLists[field].length > 100
  )

  return (
    <form onSubmit={handleSubmit} className='space-y-5'>
      <Tabs
        defaultValue='collection'
        className='gap-0 overflow-hidden rounded-xl border bg-card'
      >
        <TabsList className='h-auto w-full justify-start gap-1 overflow-x-auto rounded-none border-b bg-muted/20 p-1.5'>
          <TabsTrigger value='collection' className='px-3 py-2'>
            Collecte
          </TabsTrigger>
          <TabsTrigger value='relevance' className='px-3 py-2'>
            Pertinence
          </TabsTrigger>
          <TabsTrigger value='targeting' className='px-3 py-2'>
            Territoires et CPV
          </TabsTrigger>
          <TabsTrigger value='scoring' className='px-3 py-2'>
            Scoring
          </TabsTrigger>
        </TabsList>

        <TabsContent value='collection' className='m-0 p-6 md:p-8'>
          <SettingsSection
            title='Collecte BOAMP'
            description='Ces termes déterminent les avis récupérés par la source.'
            hint='Le BOAMP est le Bulletin officiel des annonces des marchés publics français.'
          >
            <LinesField
              id='sourcing-keywords'
              label='Mots-clés recherchés'
              value={form.keywords}
              onChange={(value) => setList('keywords', value)}
              count={parsedLists.keywords.length}
              placeholder={'développement logiciel\nlogiciel métier'}
              required
            />
            <LinesField
              id='sourcing-exclusions'
              label='Termes exclus de la collecte'
              value={form.excludedKeywords}
              onChange={(value) => setList('excludedKeywords', value)}
              count={parsedLists.excludedKeywords.length}
              placeholder={'porte automatique\nserrurerie'}
            />
          </SettingsSection>
        </TabsContent>

        <TabsContent value='relevance' className='m-0 p-6 md:p-8'>
          <SettingsSection
            title='Signaux de pertinence'
            description='Chaque correspondance ajoute ou retire le poids configuré.'
          >
            <LinesField
              id='positive-signals'
              label='Signaux positifs'
              value={form.positiveSignals}
              onChange={(value) => setList('positiveSignals', value)}
              count={parsedLists.positiveSignals.length}
              placeholder={'développement spécifique\napi\nrgaa'}
            />
            <LinesField
              id='negative-signals'
              label='Signaux négatifs'
              value={form.negativeSignals}
              onChange={(value) => setList('negativeSignals', value)}
              count={parsedLists.negativeSignals.length}
              placeholder={'acquisition de licences\nhébergement seul'}
            />
          </SettingsSection>
        </TabsContent>

        <TabsContent value='targeting' className='m-0 p-6 md:p-8'>
          <div className='space-y-8'>
            <SettingsSection
              title='Territoires prioritaires'
              description='Les départements sélectionnés reçoivent le bonus territorial.'
              columns='md:grid-cols-2'
            >
              <LinesField
                id='preferred-departments'
                label='Codes département'
                value={form.preferredDepartmentCodes}
                onChange={(value) => setList('preferredDepartmentCodes', value)}
                count={parsedLists.preferredDepartmentCodes.length}
                placeholder={'69\n38\n01'}
                compact
              />
            </SettingsSection>

            <SettingsSection
              title='Classification CPV'
              description='Recherchez le référentiel officiel par code ou libellé. Une catégorie sélectionnée couvre aussi ses sous-codes.'
              hint='Le CPV est la classification européenne qui décrit l’objet d’un marché public. 72* couvre par exemple tous les services informatiques.'
              columns='grid-cols-1'
            >
              <CpvPicker
                id='cpv-whitelist'
                label='CPV ciblés'
                description={`Très pertinents · +${form.cpvWhitelistBoost} points`}
                value={form.cpvWhitelistPrefixes}
                onChange={(value) => setList('cpvWhitelistPrefixes', value)}
              />
              <CpvPicker
                id='cpv-watch'
                label='CPV surveillés'
                description={`À surveiller · +${form.cpvWatchBoost} points`}
                value={form.cpvWatchPrefixes}
                onChange={(value) => setList('cpvWatchPrefixes', value)}
              />
              <CpvPicker
                id='cpv-excluded'
                label='CPV pénalisés'
                description={`Hors cible · −${form.cpvExclusionPenalty} points`}
                value={form.cpvExcludedPrefixes}
                onChange={(value) => setList('cpvExcludedPrefixes', value)}
              />
            </SettingsSection>
          </div>
        </TabsContent>

        <TabsContent value='scoring' className='m-0 space-y-8 p-6 md:p-8'>
          <SettingsSection
            title='Pondération des signaux'
            description='Les scores sont toujours bornés entre 0 et 100.'
            columns='sm:grid-cols-2 lg:grid-cols-3'
          >
            <NumberField
              label='Signal positif'
              value={form.positiveSignalWeight}
              onChange={(value) => setNumber('positiveSignalWeight', value)}
            />
            <NumberField
              label='Signal négatif'
              value={form.negativeSignalPenalty}
              onChange={(value) => setNumber('negativeSignalPenalty', value)}
            />
            <NumberField
              label='Territoire prioritaire'
              value={form.preferredDepartmentBoost}
              onChange={(value) => setNumber('preferredDepartmentBoost', value)}
            />
            <NumberField
              label='CPV ciblé'
              value={form.cpvWhitelistBoost}
              onChange={(value) => setNumber('cpvWhitelistBoost', value)}
              hint='Points ajoutés pour chaque correspondance avec un préfixe CPV ciblé.'
            />
            <NumberField
              label='CPV surveillé'
              value={form.cpvWatchBoost}
              onChange={(value) => setNumber('cpvWatchBoost', value)}
              hint='Points ajoutés pour chaque correspondance avec un préfixe CPV surveillé.'
            />
            <NumberField
              label='CPV pénalisé'
              value={form.cpvExclusionPenalty}
              onChange={(value) => setNumber('cpvExclusionPenalty', value)}
              hint='Points retirés pour chaque correspondance avec un préfixe CPV pénalisé.'
            />
            <NumberField
              label='Pénalité urgence'
              value={form.urgentDeadlinePenalty}
              onChange={(value) => setNumber('urgentDeadlinePenalty', value)}
            />
          </SettingsSection>

          <SettingsSection
            title='Règles opérationnelles'
            description='Ces seuils pilotent la collecte et les vues rapides.'
            columns='sm:grid-cols-2 lg:grid-cols-3'
          >
            <NumberField
              label='Urgent sous (jours)'
              value={form.urgentDeadlineDays}
              onChange={(value) => setNumber('urgentDeadlineDays', value)}
              max={365}
            />
            <NumberField
              label='Seuil très pertinent'
              value={form.highRelevanceThreshold}
              onChange={(value) => setNumber('highRelevanceThreshold', value)}
            />
            <NumberField
              label='Avis par page BOAMP'
              value={form.pageSize}
              onChange={(value) => setNumber('pageSize', value)}
              min={1}
            />
          </SettingsSection>
        </TabsContent>
      </Tabs>

      <div className='sticky bottom-4 z-10 flex flex-col gap-3 rounded-xl border bg-background/95 px-4 py-3 shadow-lg backdrop-blur sm:flex-row sm:items-center sm:justify-between'>
        <div>
          <p className='text-sm font-medium'>Profil de sourcing</p>
          <p className='text-sm text-muted-foreground'>
            Appliqué dès la prochaine synchronisation.
          </p>
        </div>
        <Button
          type='submit'
          disabled={
            mutation.isPending ||
            invalidLists ||
            parsedLists.keywords.length === 0
          }
        >
          {mutation.isPending ? <Loader2 className='animate-spin' /> : <Save />}
          Enregistrer le profil
        </Button>
      </div>
    </form>
  )
}

function SettingsSection({
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
    <section className='space-y-5'>
      <div className='border-b pb-4'>
        <div className='flex items-center gap-1.5'>
          <h3 className='font-medium'>{title}</h3>
          {hint && <HelpHint label={`Aide sur ${title}`}>{hint}</HelpHint>}
        </div>
        <p className='text-sm text-muted-foreground'>{description}</p>
      </div>
      <div className={`grid gap-5 ${columns}`}>{children}</div>
    </section>
  )
}

function LinesField({
  id,
  label,
  value,
  onChange,
  count,
  placeholder,
  hint,
  required,
  compact,
}: {
  id: string
  label: string
  value: string
  onChange: (value: string) => void
  count: number
  placeholder: string
  hint?: string
  required?: boolean
  compact?: boolean
}) {
  const isInvalid = count > 100

  return (
    <div className='space-y-2'>
      <div className='flex items-center justify-between gap-3'>
        <div className='flex items-center gap-1.5'>
          <Label htmlFor={id}>{label}</Label>
          {hint && <HelpHint label={`Aide sur ${label}`}>{hint}</HelpHint>}
        </div>
        <span
          className={`text-xs tabular-nums ${isInvalid ? 'text-destructive' : 'text-muted-foreground'}`}
        >
          {count}/100
        </span>
      </div>
      <Textarea
        id={id}
        value={value}
        onChange={(event) => onChange(event.target.value)}
        rows={compact ? 6 : 9}
        maxLength={10_000}
        placeholder={placeholder}
        className='resize-y bg-background font-mono text-sm leading-6'
        required={required}
        aria-invalid={isInvalid}
      />
      {isInvalid && (
        <p role='alert' className='text-xs text-destructive'>
          Maximum 100 valeurs.
        </p>
      )}
    </div>
  )
}

function NumberField({
  label,
  value,
  onChange,
  hint,
  min = 0,
  max = 100,
}: {
  label: string
  value: number
  onChange: (value: number) => void
  hint?: string
  min?: number
  max?: number
}) {
  const id = `sourcing-${label.toLowerCase().replace(/\s+/g, '-')}`
  return (
    <div className='space-y-2'>
      <div className='flex items-center gap-1.5'>
        <Label htmlFor={id}>{label}</Label>
        {hint && <HelpHint label={`Aide sur ${label}`}>{hint}</HelpHint>}
      </div>
      <Input
        id={id}
        type='number'
        min={min}
        max={max}
        value={value}
        onChange={(event) => onChange(event.target.valueAsNumber)}
        className='font-mono tabular-nums'
        required
      />
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

function parseLines(value: string) {
  return [
    ...new Set(
      value
        .split('\n')
        .map((line) => line.trim())
        .filter(Boolean)
    ),
  ]
}

function createFormState(settings: SourcingSettings): FormState {
  return {
    keywords: settings.keywords.join('\n'),
    excludedKeywords: settings.excludedKeywords.join('\n'),
    positiveSignals: settings.positiveSignals.join('\n'),
    negativeSignals: settings.negativeSignals.join('\n'),
    preferredDepartmentCodes: settings.preferredDepartmentCodes.join('\n'),
    cpvWhitelistPrefixes: settings.cpvWhitelistPrefixes.join('\n'),
    cpvWatchPrefixes: settings.cpvWatchPrefixes.join('\n'),
    cpvExcludedPrefixes: settings.cpvExcludedPrefixes.join('\n'),
    pageSize: settings.pageSize,
    positiveSignalWeight: settings.positiveSignalWeight,
    negativeSignalPenalty: settings.negativeSignalPenalty,
    preferredDepartmentBoost: settings.preferredDepartmentBoost,
    cpvWhitelistBoost: settings.cpvWhitelistBoost,
    cpvWatchBoost: settings.cpvWatchBoost,
    cpvExclusionPenalty: settings.cpvExclusionPenalty,
    urgentDeadlineDays: settings.urgentDeadlineDays,
    urgentDeadlinePenalty: settings.urgentDeadlinePenalty,
    highRelevanceThreshold: settings.highRelevanceThreshold,
  }
}
