import { useQuery } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import { ChevronLeft, Radar } from 'lucide-react'
import { Skeleton } from '@/components/ui/skeleton'
import { StateMessage } from '@/components/state-message'
import { getSourcingSettings, sourcingSettingsQueryKey } from './api'
import { SourcingSettingsForm } from './components/sourcing-settings-form'

export function SourcingSettings() {
  const settingsQuery = useQuery({
    queryKey: sourcingSettingsQueryKey,
    queryFn: getSourcingSettings,
  })

  return (
    <>
      <header className='flex flex-col gap-4 border-b pb-6'>
        <Link
          to='/settings'
          className='inline-flex w-fit items-center gap-1 text-sm text-muted-foreground transition-colors hover:text-foreground focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none'
        >
          <ChevronLeft aria-hidden='true' className='size-4' />
          Tous les paramètres
        </Link>
        <div className='flex items-start gap-4'>
          <span className='mt-0.5 flex size-11 shrink-0 items-center justify-center rounded-lg bg-emerald-500/10 text-emerald-700 dark:text-emerald-400'>
            <Radar aria-hidden='true' className='size-5' />
          </span>
          <div className='space-y-1.5'>
            <h1 className='text-2xl font-bold tracking-tight md:text-3xl'>
              Paramètres de sourcing
            </h1>
            <p className='max-w-2xl text-pretty text-muted-foreground'>
              Adaptez le sourcing à votre activité.
            </p>
          </div>
        </div>
      </header>

      {settingsQuery.isPending ? (
        <div
          className='space-y-4'
          aria-label='Chargement du profil de sourcing'
        >
          <Skeleton className='h-11 w-full rounded-xl' />
          <Skeleton className='h-96 w-full rounded-xl' />
          <Skeleton className='h-16 w-full rounded-xl' />
        </div>
      ) : settingsQuery.isError ? (
        <StateMessage icon={<Radar />} className='rounded-xl border'>
          {settingsQuery.error.message}
        </StateMessage>
      ) : settingsQuery.data === null ? (
        <StateMessage icon={<Radar />} className='rounded-xl border'>
          Aucun profil de sourcing n’est encore configuré. Il sera créé lors de
          l’onboarding.
        </StateMessage>
      ) : (
        <SourcingSettingsForm
          key={settingsQuery.data.updatedAt}
          settings={settingsQuery.data}
        />
      )}
    </>
  )
}
