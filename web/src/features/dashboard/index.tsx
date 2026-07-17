import { Construction } from 'lucide-react'
import { ConfigDrawer } from '@/components/config-drawer'
import { Header } from '@/components/layout/header'
import { Main } from '@/components/layout/main'
import { Search } from '@/components/search'
import { ThemeSwitch } from '@/components/theme-switch'

export function Dashboard() {
  return (
    <>
      <Header>
        <Search />
        <ThemeSwitch />
        <ConfigDrawer />
      </Header>
      <Main className='flex flex-1 items-center justify-center'>
        <section className='flex max-w-sm flex-col items-center text-center'>
          <div className='mb-5 grid size-12 place-items-center rounded-lg border bg-muted/40'>
            <Construction
              aria-hidden='true'
              className='size-5 text-muted-foreground'
            />
          </div>
          <h1 className='text-2xl font-semibold tracking-tight'>
            En cours de construction
          </h1>
          <p className='mt-2 text-sm text-muted-foreground'>
            Le tableau de bord de RiposteOS arrive bientôt.
          </p>
        </section>
      </Main>
    </>
  )
}
