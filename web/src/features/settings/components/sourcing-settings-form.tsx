import { useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useBlocker } from '@tanstack/react-router'
import {
  CheckCircle2,
  CircleAlert,
  Loader2,
  MapPin,
  Save,
  Search,
  SlidersHorizontal,
  Target,
} from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { ConfirmDialog } from '@/components/confirm-dialog'
import {
  type SourcingSettings,
  sourcingSettingsQueryKey,
  updateSourcingSettings,
} from '../api'
import { CountryPicker } from './country-picker'
import { CpvPicker } from './cpv-picker'
import {
  NumberField,
  ScheduleField,
  SettingsSection,
  TermsField,
} from './sourcing-settings-fields'

const listFields = [
  'keywords',
  'excludedKeywords',
  'positiveSignals',
  'negativeSignals',
  'allowedCountryCodes',
  'preferredDepartmentCodes',
  'cpvWhitelistPrefixes',
  'cpvWatchPrefixes',
  'cpvExcludedPrefixes',
] as const

const tabTriggerClassName =
  'h-12 flex-none rounded-none border-0 border-b-2 border-transparent px-0 text-muted-foreground shadow-none data-[state=active]:border-primary data-[state=active]:bg-transparent data-[state=active]:text-foreground data-[state=active]:shadow-none'

type ListField = (typeof listFields)[number]
type FormState = Omit<SourcingSettings, ListField | 'updatedAt'> &
  Record<ListField, string>
type NumberFieldName = Exclude<
  keyof FormState,
  ListField | 'boampCron' | 'tedCron' | 'placeCron'
>

export function SourcingSettingsForm({
  settings,
}: {
  settings: SourcingSettings
}) {
  const queryClient = useQueryClient()
  const [form, setForm] = useState<FormState>(() => createFormState(settings))
  const isDirty =
    JSON.stringify(form) !== JSON.stringify(createFormState(settings))
  const mutation = useMutation({
    mutationFn: updateSourcingSettings,
    onSuccess: (updatedSettings) => {
      setForm(createFormState(updatedSettings))
      queryClient.setQueryData(sourcingSettingsQueryKey, updatedSettings)
      toast.success('Profil de sourcing enregistré', {
        description: 'Les changements sont appliqués.',
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

  function setNumber(field: NumberFieldName, value: number) {
    setForm((current) => ({ ...current, [field]: value }))
  }

  const invalidLists = listFields.some(
    (field) => parsedLists[field].length > 100
  )
  const blocker = useBlocker({
    shouldBlockFn: () => isDirty,
    enableBeforeUnload: isDirty,
    disabled: !isDirty || mutation.isPending,
    withResolver: true,
  })

  return (
    <form onSubmit={handleSubmit} className='space-y-5'>
      <Tabs defaultValue='collection' className='gap-0'>
        <TabsList className='h-auto w-full justify-start gap-6 overflow-x-auto rounded-none border-b bg-transparent pt-2'>
          <TabsTrigger value='collection' className={tabTriggerClassName}>
            <Search aria-hidden='true' />
            Collecte
          </TabsTrigger>
          <TabsTrigger value='relevance' className={tabTriggerClassName}>
            <Target aria-hidden='true' />
            Pertinence
          </TabsTrigger>
          <TabsTrigger value='targeting' className={tabTriggerClassName}>
            <MapPin aria-hidden='true' />
            Territoires et CPV
          </TabsTrigger>
          <TabsTrigger value='scoring' className={tabTriggerClassName}>
            <SlidersHorizontal aria-hidden='true' />
            Scoring
          </TabsTrigger>
        </TabsList>

        <TabsContent value='collection' className='m-0 py-6 md:py-8'>
          <div className='space-y-10'>
            <SettingsSection
              title='Recherche'
              description='Ajoutez les termes qui décrivent votre activité, et ceux à écarter.'
            >
              <TermsField
                id='sourcing-keywords'
                label='Ce que vous recherchez'
                values={parsedLists.keywords}
                onChange={(values) => setList('keywords', values.join('\n'))}
                placeholder='Ex. développement logiciel'
              />
              <TermsField
                id='sourcing-exclusions'
                label='Ce que vous excluez'
                values={parsedLists.excludedKeywords}
                onChange={(values) =>
                  setList('excludedKeywords', values.join('\n'))
                }
                placeholder='Ex. porte automatique'
              />
            </SettingsSection>

            <SettingsSection
              title='Rythme de synchronisation'
              description='Réglez chaque source indépendamment. PLACE couvre les consultations de l’État.'
              columns='md:grid-cols-2 xl:grid-cols-3'
            >
              <ScheduleField
                source='BOAMP'
                value={form.boampCron}
                onChange={(value) =>
                  setForm((current) => ({ ...current, boampCron: value }))
                }
              />
              <ScheduleField
                source='TED'
                value={form.tedCron}
                onChange={(value) =>
                  setForm((current) => ({ ...current, tedCron: value }))
                }
              />
              <ScheduleField
                source='PLACE'
                value={form.placeCron}
                onChange={(value) =>
                  setForm((current) => ({ ...current, placeCron: value }))
                }
              />
            </SettingsSection>
          </div>
        </TabsContent>

        <TabsContent value='relevance' className='m-0 py-6 md:py-8'>
          <SettingsSection
            title='Signaux de pertinence'
            description='Ces expressions expliquent pourquoi une opportunité gagne ou perd des points.'
          >
            <TermsField
              id='positive-signals'
              label='Signaux positifs'
              values={parsedLists.positiveSignals}
              onChange={(values) =>
                setList('positiveSignals', values.join('\n'))
              }
              placeholder='Ex. développement spécifique'
            />
            <TermsField
              id='negative-signals'
              label='Signaux négatifs'
              values={parsedLists.negativeSignals}
              onChange={(values) =>
                setList('negativeSignals', values.join('\n'))
              }
              placeholder='Ex. acquisition de licences'
            />
          </SettingsSection>
        </TabsContent>

        <TabsContent value='targeting' className='m-0 py-6 md:py-8'>
          <div className='space-y-8'>
            <SettingsSection
              title='Périmètre géographique'
              description='Limitez la collecte à certains pays, puis priorisez les départements utiles à votre activité.'
              columns='md:grid-cols-2 md:items-start'
            >
              <CountryPicker
                value={form.allowedCountryCodes}
                onChange={(value) => setList('allowedCountryCodes', value)}
              />
              <TermsField
                id='preferred-departments'
                label='Codes département'
                values={parsedLists.preferredDepartmentCodes}
                onChange={(values) =>
                  setList('preferredDepartmentCodes', values.join('\n'))
                }
                placeholder='Ex. 69'
              />
            </SettingsSection>

            <SettingsSection
              title='Ciblage par activité (CPV)'
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

        <TabsContent value='scoring' className='m-0 space-y-10 py-6 md:py-8'>
          <SettingsSection
            title='Pondération du score'
            description='Définissez l’impact de chaque signal. Le score final reste borné entre 0 et 100.'
            columns='sm:grid-cols-2 lg:grid-cols-3'
          >
            <NumberField
              label='Signal positif'
              value={form.positiveSignalWeight}
              onChange={(value) => setNumber('positiveSignalWeight', value)}
              unit='points'
            />
            <NumberField
              label='Signal négatif'
              value={form.negativeSignalPenalty}
              onChange={(value) => setNumber('negativeSignalPenalty', value)}
              unit='points'
            />
            <NumberField
              label='Territoire prioritaire'
              value={form.preferredDepartmentBoost}
              onChange={(value) => setNumber('preferredDepartmentBoost', value)}
              unit='points'
            />
            <NumberField
              label='CPV ciblé'
              value={form.cpvWhitelistBoost}
              onChange={(value) => setNumber('cpvWhitelistBoost', value)}
              hint='Points ajoutés pour chaque correspondance avec un préfixe CPV ciblé.'
              unit='points'
            />
            <NumberField
              label='CPV surveillé'
              value={form.cpvWatchBoost}
              onChange={(value) => setNumber('cpvWatchBoost', value)}
              hint='Points ajoutés pour chaque correspondance avec un préfixe CPV surveillé.'
              unit='points'
            />
            <NumberField
              label='CPV pénalisé'
              value={form.cpvExclusionPenalty}
              onChange={(value) => setNumber('cpvExclusionPenalty', value)}
              hint='Points retirés pour chaque correspondance avec un préfixe CPV pénalisé.'
              unit='points'
            />
            <NumberField
              label='Pénalité urgence'
              value={form.urgentDeadlinePenalty}
              onChange={(value) => setNumber('urgentDeadlinePenalty', value)}
              unit='points'
            />
          </SettingsSection>

          <SettingsSection
            title='Seuils opérationnels'
            description='Ces valeurs pilotent l’affichage des priorités et la taille des requêtes envoyées aux sources.'
            columns='sm:grid-cols-2 lg:grid-cols-3'
          >
            <NumberField
              label='Délai urgent'
              value={form.urgentDeadlineDays}
              onChange={(value) => setNumber('urgentDeadlineDays', value)}
              max={365}
              unit='jours'
            />
            <NumberField
              label='Seuil très pertinent'
              value={form.highRelevanceThreshold}
              onChange={(value) => setNumber('highRelevanceThreshold', value)}
              unit='points'
            />
            <NumberField
              label='Avis par requête'
              value={form.pageSize}
              onChange={(value) => setNumber('pageSize', value)}
              min={1}
              unit='avis'
            />
          </SettingsSection>
        </TabsContent>
      </Tabs>

      <div className='sticky bottom-4 z-10 flex flex-col gap-3 rounded-xl border bg-background/95 px-4 py-3 shadow-sm backdrop-blur sm:flex-row sm:items-center sm:justify-between'>
        <div className='flex items-start gap-3' aria-live='polite'>
          {isDirty ? (
            <CircleAlert
              aria-hidden='true'
              className='mt-0.5 size-5 shrink-0 text-amber-600'
            />
          ) : (
            <CheckCircle2
              aria-hidden='true'
              className='mt-0.5 size-5 shrink-0 text-emerald-600'
            />
          )}
          <div>
            <p className='text-sm font-medium'>
              {isDirty ? 'Modifications non enregistrées' : 'Profil à jour'}
            </p>
            <p className='text-sm text-muted-foreground'>
              {isDirty
                ? 'Enregistrez-les avant de quitter cette page.'
                : 'Aucune modification à enregistrer.'}
            </p>
          </div>
        </div>
        <Button
          type='submit'
          disabled={
            mutation.isPending ||
            !isDirty ||
            invalidLists ||
            parsedLists.keywords.length === 0 ||
            !form.boampCron.trim() ||
            !form.tedCron.trim() ||
            !form.placeCron.trim()
          }
        >
          {mutation.isPending ? (
            <Loader2 className='animate-spin' />
          ) : isDirty ? (
            <Save />
          ) : (
            <CheckCircle2 />
          )}
          {mutation.isPending
            ? 'Enregistrement…'
            : isDirty
              ? 'Enregistrer les modifications'
              : 'Enregistré'}
        </Button>
      </div>

      <ConfirmDialog
        open={blocker.status === 'blocked'}
        onOpenChange={(open) => {
          if (!open && blocker.status === 'blocked') blocker.reset()
        }}
        title='Quitter sans enregistrer ?'
        desc='Vos modifications seront perdues.'
        cancelBtnText='Rester sur la page'
        confirmText='Quitter sans enregistrer'
        destructive
        handleConfirm={() => {
          if (blocker.status === 'blocked') blocker.proceed()
        }}
      />
    </form>
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
    allowedCountryCodes: settings.allowedCountryCodes.join('\n'),
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
    boampCron: settings.boampCron,
    tedCron: settings.tedCron,
    placeCron: settings.placeCron ?? settings.tedCron,
  }
}
