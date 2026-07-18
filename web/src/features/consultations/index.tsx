import { useDeferredValue, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { getRouteApi } from '@tanstack/react-router'
import { FileSearch, Loader2, Plus } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { ConfigDrawer } from '@/components/config-drawer'
import { Header } from '@/components/layout/header'
import { Main } from '@/components/layout/main'
import { Search } from '@/components/search'
import { StateMessage } from '@/components/state-message'
import { ThemeSwitch } from '@/components/theme-switch'
import {
  type Consultation,
  consultationsQueryKey,
  getConsultations,
} from './api'
import { ConsultationsTable } from './components/consultations-table'
import { CreateConsultationDialog } from './components/create-consultation-dialog'
import { buildConsultationListQuery } from './gridify'

const route = getRouteApi('/_authenticated/consultations/')
const emptyConsultations: Consultation[] = []

export function Consultations() {
  const search = route.useSearch()
  const deferredFilter = useDeferredValue(search.filter)
  const [dialogOpen, setDialogOpen] = useState(false)
  const [referenceTime] = useState(Date.now)
  const listQuery = buildConsultationListQuery(
    {
      page: search.page ?? 1,
      pageSize: search.pageSize ?? 20,
      filter: deferredFilter ?? '',
      source: search.source ?? [],
      deadline: search.deadline ?? [],
      sort: search.sort ?? 'responseDeadline',
      direction: search.direction ?? 'asc',
    },
    referenceTime
  )
  const query = useQuery({
    queryKey: consultationsQueryKey(listQuery),
    queryFn: () => getConsultations(listQuery),
    placeholderData: (previous) => previous,
  })

  return (
    <>
      <Header>
        <Search />
        <ThemeSwitch />
        <ConfigDrawer />
      </Header>
      <Main className='flex flex-1 flex-col gap-6'>
        <section className='flex flex-wrap items-center justify-between gap-4 border-b pb-6'>
          <div>
            <h1 className='text-3xl font-bold tracking-tight'>Consultations</h1>
            <p className='mt-1 text-sm text-muted-foreground'>À qualifier</p>
          </div>
          <Button onClick={() => setDialogOpen(true)}>
            <Plus />
            Ajouter manuellement
          </Button>
        </section>

        <Card className='overflow-hidden py-0'>
          <CardContent className='p-0'>
            {query.isPending ? (
              <StateMessage
                icon={<Loader2 className='animate-spin' />}
                role='status'
              >
                Chargement des consultations…
              </StateMessage>
            ) : query.isError ? (
              <StateMessage icon={<FileSearch />} role='alert'>
                {query.error.message}
              </StateMessage>
            ) : (
              <ConsultationsTable
                consultations={query.data?.items ?? emptyConsultations}
                totalCount={query.data?.totalCount ?? 0}
              />
            )}
          </CardContent>
        </Card>
      </Main>

      <CreateConsultationDialog
        open={dialogOpen}
        onOpenChange={setDialogOpen}
      />
    </>
  )
}
