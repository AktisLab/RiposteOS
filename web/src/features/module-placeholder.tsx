import { Construction } from 'lucide-react'
import { ConfigDrawer } from '@/components/config-drawer'
import { Header } from '@/components/layout/header'
import { Main } from '@/components/layout/main'
import { Search } from '@/components/search'
import { ThemeSwitch } from '@/components/theme-switch'

type ModulePlaceholderProps = {
  title: string
}

export function ModulePlaceholder({ title }: ModulePlaceholderProps) {
  return (
    <>
      <Header>
        <Search />
        <ThemeSwitch />
        <ConfigDrawer />
      </Header>
      <Main>
        <div className='flex min-h-[55vh] flex-col items-center justify-center gap-3 text-center'>
          <div className='rounded-full bg-muted p-4'>
            <Construction className='size-6 text-muted-foreground' />
          </div>
          <h1 className='text-2xl font-bold tracking-tight'>{title}</h1>
          <p className='text-muted-foreground'>Aucune fonctionnalité pour le moment.</p>
        </div>
      </Main>
    </>
  )
}
