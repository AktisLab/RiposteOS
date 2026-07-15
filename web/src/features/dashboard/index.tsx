import { CheckCircle2, Container, Database, Server } from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import {
  Card,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card'
import { ConfigDrawer } from '@/components/config-drawer'
import { Header } from '@/components/layout/header'
import { Main } from '@/components/layout/main'
import { Search } from '@/components/search'
import { ThemeSwitch } from '@/components/theme-switch'

const foundations = [
  { title: 'API', description: 'ASP.NET Core 10', icon: Server },
  { title: 'Données', description: 'PostgreSQL + pgvector', icon: Database },
  { title: 'Workers', description: 'Hangfire + PostgreSQL', icon: Container },
]

export function Dashboard() {
  return (
    <>
      <Header>
        <Search />
        <ThemeSwitch />
        <ConfigDrawer />
      </Header>
      <Main>
        <div className='mb-8 space-y-2'>
          <div className='flex items-center gap-3'>
            <h1 className='text-2xl font-bold tracking-tight'>RiposteOS</h1>
            <Badge variant='secondary'>Base technique</Badge>
          </div>
          <p className='max-w-2xl text-muted-foreground'>
            Le socle est prêt. Les fonctionnalités métier seront ajoutées par
            tranches verticales.
          </p>
        </div>
        <div className='grid gap-4 md:grid-cols-3'>
          {foundations.map(({ title, description, icon: Icon }) => (
            <Card key={title}>
              <CardHeader>
                <div className='mb-2 flex items-center justify-between'>
                  <Icon className='size-5 text-muted-foreground' />
                  <CheckCircle2 className='size-4 text-emerald-600' />
                </div>
                <CardTitle className='text-base'>{title}</CardTitle>
                <CardDescription>{description}</CardDescription>
              </CardHeader>
            </Card>
          ))}
        </div>
      </Main>
    </>
  )
}
