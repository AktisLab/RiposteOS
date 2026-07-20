import { Link } from '@tanstack/react-router'
import { Bot, ChevronRight, Radar } from 'lucide-react'

export function SettingsOverview() {
  return (
    <>
      <header className='space-y-2 border-b pb-6'>
        <h1 className='text-3xl font-bold tracking-tight'>Paramètres</h1>
        <p className='max-w-2xl text-pretty text-muted-foreground'>
          Configurez chaque domaine de RiposteOS depuis un espace dédié.
        </p>
      </header>

      <section aria-labelledby='settings-categories' className='max-w-4xl'>
        <div className='mb-4'>
          <h2 id='settings-categories' className='text-lg font-semibold'>
            Catégories
          </h2>
          <p className='text-sm text-muted-foreground'>
            Les réglages sont regroupés selon leur impact dans le produit.
          </p>
        </div>

        <div className='overflow-hidden rounded-xl border bg-card'>
          <Link
            to='/settings/sourcing'
            className='group grid grid-cols-[auto_1fr_auto] items-center gap-4 p-5 transition-colors hover:bg-muted/40 focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none focus-visible:ring-inset active:bg-muted/60'
          >
            <span className='flex size-11 items-center justify-center rounded-lg border bg-background text-emerald-700 dark:text-emerald-400'>
              <Radar className='size-5' />
            </span>
            <span className='min-w-0'>
              <span className='block font-semibold'>Sourcing</span>
              <span className='mt-1 block text-sm leading-relaxed text-muted-foreground'>
                Sources, mots-clés, signaux de pertinence, territoires, CPV et
                pondération du score.
              </span>
            </span>
            <ChevronRight className='size-5 text-muted-foreground transition-transform group-hover:translate-x-0.5 group-hover:text-foreground' />
          </Link>
          <Link
            to='/settings/ai'
            className='group grid grid-cols-[auto_1fr_auto] items-center gap-4 border-t p-5 transition-colors hover:bg-muted/40 focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none focus-visible:ring-inset active:bg-muted/60'
          >
            <span className='flex size-11 items-center justify-center rounded-lg border bg-background text-violet-700 dark:text-violet-400'>
              <Bot className='size-5' />
            </span>
            <span className='min-w-0'>
              <span className='block font-semibold'>IA</span>
              <span className='mt-1 block text-sm leading-relaxed text-muted-foreground'>
                Fournisseurs IA et affectation des tâches automatiques.
              </span>
            </span>
            <ChevronRight className='size-5 text-muted-foreground transition-transform group-hover:translate-x-0.5 group-hover:text-foreground' />
          </Link>
        </div>
      </section>
    </>
  )
}
