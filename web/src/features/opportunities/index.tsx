import { useDeferredValue, useEffect, useRef, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { getRouteApi } from '@tanstack/react-router'
import { ChevronDown, History, Loader2, Radar, RefreshCw } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { ConfigDrawer } from '@/components/config-drawer'
import { Header } from '@/components/layout/header'
import { Main } from '@/components/layout/main'
import { Search } from '@/components/search'
import { StateMessage } from '@/components/state-message'
import { ThemeSwitch } from '@/components/theme-switch'
import {
  getSourcingSettings,
  sourcingSettingsQueryKey,
} from '@/features/settings/api'
import {
  type Opportunity,
  getOpportunities,
  getImportRuns,
  importBoamp,
  importRunsQueryKey,
  importRunsQueryRoot,
  opportunitiesQueryKey,
  opportunitiesQueryRoot,
} from './api'
import { ImportHistoryDrawer } from './components/import-history-drawer'
import { OpportunitiesTable } from './components/opportunities-table'
import { buildOpportunityListQuery } from './gridify'

const emptyOpportunities: Opportunity[] = []
const route = getRouteApi('/_authenticated/opportunities')
const latestImportQuery = { page: 1, pageSize: 1 }

export function Opportunities() {
  const queryClient = useQueryClient()
  const search = route.useSearch()
  const deferredFilter = useDeferredValue(search.filter)
  const [historyOpen, setHistoryOpen] = useState(false)
  const [referenceTime] = useState(Date.now)
  const settingsQuery = useQuery({
    queryKey: sourcingSettingsQueryKey,
    queryFn: getSourcingSettings,
  })
  const filterProfile = settingsQuery.data ?? {
    highRelevanceThreshold: 100,
    preferredDepartmentCodes: [],
    urgentDeadlineDays: 7,
  }
  const opportunityQuery = buildOpportunityListQuery(
    {
      page: search.page ?? 1,
      pageSize: search.pageSize ?? 20,
      filter: deferredFilter ?? '',
      source: search.source ?? [],
      deadline: search.deadline ?? [],
      status: search.status ?? [],
      highRelevance: search.highRelevance ?? false,
      preferredTerritory: search.preferredTerritory ?? false,
      buyer: search.buyer ?? '',
      department: search.department ?? '',
      cpv: search.cpv ?? '',
      sort: search.sort ?? 'publicationDate',
      direction: search.direction ?? 'desc',
    },
    referenceTime,
    filterProfile
  )
  const opportunitiesQuery = useQuery({
    queryKey: opportunitiesQueryKey(opportunityQuery),
    queryFn: () => getOpportunities(opportunityQuery),
    placeholderData: (previous) => previous,
  })
  const importRunsQuery = useQuery({
    queryKey: importRunsQueryKey(latestImportQuery),
    queryFn: () => getImportRuns(latestImportQuery),
    refetchInterval: (query) => {
      const latestRun = query.state.data?.items[0]
      return latestRun?.status === 'Queued' || latestRun?.status === 'Running'
        ? 2_000
        : false
    },
  })
  const importMutation = useMutation({
    mutationFn: importBoamp,
    onSuccess: (run) => {
      queryClient.setQueryData(importRunsQueryKey(latestImportQuery), {
        items: [run],
        totalCount: (importRunsQuery.data?.totalCount ?? 0) + 1,
        ...latestImportQuery,
      })
      void queryClient.invalidateQueries({ queryKey: importRunsQueryRoot })
      toast.success('Import BOAMP planifié', {
        description: 'Le worker va le traiter en arrière-plan.',
      })
    },
    onError: (error) => toast.error(error.message),
  })
  const opportunities = opportunitiesQuery.data?.items ?? emptyOpportunities
  const opportunityPage = opportunitiesQuery.data
  const latestRun = importRunsQuery.data?.items[0]
  const activeRun =
    latestRun?.status === 'Queued' || latestRun?.status === 'Running'
  const profileMissing =
    settingsQuery.isSuccess && settingsQuery.data === null
  const finalizedRunId = useRef<string | null>(null)

  useEffect(() => {
    if (!latestRun || activeRun || finalizedRunId.current === latestRun.id) {
      return
    }

    finalizedRunId.current = latestRun.id
    void queryClient.invalidateQueries({ queryKey: opportunitiesQueryRoot })
  }, [activeRun, latestRun, queryClient])

  return (
    <>
      <Header>
        <Search />
        <ThemeSwitch />
        <ConfigDrawer />
      </Header>
      <Main className='flex flex-1 flex-col gap-6'>
        <section className='flex items-center justify-between gap-5 border-b pb-6'>
          <h1 className='text-3xl font-bold tracking-tight'>Sourcing</h1>
          <div className='flex items-center'>
            <Button
              className='cursor-pointer rounded-r-none'
              onClick={() => importMutation.mutate()}
              disabled={importMutation.isPending || activeRun || profileMissing}
              title={
                profileMissing
                  ? 'Créez un profil de sourcing avant de synchroniser.'
                  : undefined
              }
            >
              {importMutation.isPending || activeRun ? (
                <Loader2 className='animate-spin' />
              ) : (
                <RefreshCw />
              )}
              Synchronisation
            </Button>
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button
                  size='icon'
                  className='cursor-pointer rounded-l-none border-l border-primary-foreground/20'
                  aria-label='Ouvrir les actions de synchronisation'
                >
                  <ChevronDown />
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align='end' className='w-56'>
                <DropdownMenuItem
                  className='cursor-pointer'
                  onSelect={() => setHistoryOpen(true)}
                >
                  <History />
                  Historique
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
          </div>
          <ImportHistoryDrawer
            open={historyOpen}
            onOpenChange={setHistoryOpen}
          />
        </section>

        <Card className='overflow-hidden py-0'>
          <CardContent className='p-0'>
            {opportunitiesQuery.isPending ? (
              <StateMessage icon={<Loader2 className='animate-spin' />}>
                Chargement des opportunités…
              </StateMessage>
            ) : opportunitiesQuery.isError ? (
              <StateMessage icon={<Radar />}>
                {opportunitiesQuery.error.message}
              </StateMessage>
            ) : (
              <OpportunitiesTable
                opportunities={opportunities}
                totalCount={opportunityPage?.totalCount ?? 0}
                referenceTime={referenceTime}
                highRelevanceThreshold={filterProfile.highRelevanceThreshold}
                preferredDepartmentCodes={
                  filterProfile.preferredDepartmentCodes
                }
                urgentDeadlineDays={filterProfile.urgentDeadlineDays}
              />
            )}
          </CardContent>
        </Card>
      </Main>
    </>
  )
}
