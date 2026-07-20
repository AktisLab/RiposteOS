import { useQuery } from '@tanstack/react-query'
import { useLayout } from '@/context/layout-provider'
import {
  Sidebar,
  SidebarContent,
  SidebarHeader,
  SidebarRail,
} from '@/components/ui/sidebar'
import { isAiProviderUnavailable } from '@/features/settings/ai-health'
import { aiProvidersQueryKey, getAiProviders } from '@/features/settings/api'
import { AppTitle } from './app-title'
import { getSidebarData } from './data/sidebar-data'
import { NavGroup } from './nav-group'

export function AppSidebar() {
  const { collapsible, variant } = useLayout()
  const providersQuery = useQuery({
    queryKey: aiProvidersQueryKey,
    queryFn: getAiProviders,
    staleTime: 30_000,
    refetchInterval: 60_000,
  })
  const sidebarData = getSidebarData(
    providersQuery.data?.some(isAiProviderUnavailable) ?? false
  )
  return (
    <Sidebar collapsible={collapsible} variant={variant}>
      <SidebarHeader>
        <AppTitle />
      </SidebarHeader>
      <SidebarContent>
        {sidebarData.navGroups.map((props) => (
          <NavGroup key={props.title} {...props} />
        ))}
      </SidebarContent>
      <SidebarRail />
    </Sidebar>
  )
}
