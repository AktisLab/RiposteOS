import { useDeferredValue, useEffect, useMemo, useRef, useState } from 'react'
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
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { ConfigDrawer } from '@/components/config-drawer'
import { Header } from '@/components/layout/header'
import { Main } from '@/components/layout/main'
import { Search } from '@/components/search'
import { SourcingSourceLogo } from '@/components/sourcing-source-logo'
import { StateMessage } from '@/components/state-message'
import { ThemeSwitch } from '@/components/theme-switch'
import {
  getSourcingSettings,
  sourcingSettingsQueryKey,
} from '@/features/settings/api'
import {
  type Opportunity,
  type OpportunitySource,
  getOpportunities,
  getImportRuns,
  importSource,
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
const latestImportQuery = { page: 1, pageSize: 10 }
const allSources: OpportunitySource[] = ['boamp', 'ted']

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
      sort: search.sort ?? 'matchScore',
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
    refetchInterval: (query) =>
      query.state.data?.items.some(
        (run) => run.status === 'Queued' || run.status === 'Running'
      )
        ? 2_000
        : false,
  })
  const importMutation = useMutation({
    mutationFn: (sources: OpportunitySource[]) =>
      Promise.all(sources.map(importSource)),
    onSuccess: (runs) => {
      queryClient.setQueryData(importRunsQueryKey(latestImportQuery), {
        items: [...runs, ...(importRunsQuery.data?.items ?? [])].slice(
          0,
          latestImportQuery.pageSize
        ),
        totalCount: (importRunsQuery.data?.totalCount ?? 0) + runs.length,
        ...latestImportQuery,
      })
      void queryClient.invalidateQueries({ queryKey: importRunsQueryRoot })
      toast.success(
        runs.length === 1
          ? `Import ${runs[0]?.source} planifié`
          : `${runs.length} imports planifiés`,
        {
          description: 'Le worker va le traiter en arrière-plan.',
        }
      )
    },
    onError: (error) => toast.error(error.message),
  })
  const opportunities = opportunitiesQuery.data?.items ?? emptyOpportunities
  const opportunityPage = opportunitiesQuery.data
  const importRuns = useMemo(
    () => importRunsQuery.data?.items ?? [],
    [importRunsQuery.data?.items]
  )
  const activeSources = new Set(
    importRuns
      .filter((run) => run.status === 'Queued' || run.status === 'Running')
      .map((run) => run.source.toLowerCase())
  )
  const activeRun = activeSources.size > 0
  const profileMissing = settingsQuery.isSuccess && settingsQuery.data === null
  const finalizedRunIds = useRef(new Set<string>())

  useEffect(() => {
    const newlyFinalized = importRuns.filter(
      (run) =>
        run.status !== 'Queued' &&
        run.status !== 'Running' &&
        !finalizedRunIds.current.has(run.id)
    )
    if (newlyFinalized.length === 0) {
      return
    }

    newlyFinalized.forEach((run) => finalizedRunIds.current.add(run.id))
    void queryClient.invalidateQueries({ queryKey: opportunitiesQueryRoot })
  }, [importRuns, queryClient])

  function synchronize(sources: OpportunitySource[]) {
    importMutation.mutate(sources)
  }

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
              onClick={() => synchronize(allSources)}
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
                  disabled={
                    importMutation.isPending ||
                    activeSources.has('boamp') ||
                    profileMissing
                  }
                  onSelect={() => synchronize(['boamp'])}
                >
                  <SourcingSourceLogo source='boamp' className='size-4' />
                  Synchroniser BOAMP
                </DropdownMenuItem>
                <DropdownMenuItem
                  className='cursor-pointer'
                  disabled={
                    importMutation.isPending ||
                    activeSources.has('ted') ||
                    profileMissing
                  }
                  onSelect={() => synchronize(['ted'])}
                >
                  <SourcingSourceLogo source='ted' className='size-4' />
                  Synchroniser TED
                </DropdownMenuItem>
                <DropdownMenuSeparator />
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
