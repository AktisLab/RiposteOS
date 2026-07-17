import { Link } from '@tanstack/react-router'
import { X } from 'lucide-react'
import { Logo, LogoLockup } from '@/assets/logo'
import {
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  useSidebar,
} from '@/components/ui/sidebar'
import { Button } from '../ui/button'

export function AppTitle() {
  const { setOpenMobile, state, isMobile } = useSidebar()
  const isCollapsed = state === 'collapsed' && !isMobile
  return (
    <SidebarMenu>
      <SidebarMenuItem>
        <SidebarMenuButton
          size='lg'
          className='h-12 gap-2 p-0 hover:bg-transparent active:bg-transparent'
          asChild
        >
          <div>
            <Link
              to='/'
              onClick={() => setOpenMobile(false)}
              aria-label='Accueil RiposteOS'
              className='flex min-w-0 flex-1 items-center'
            >
              {isCollapsed ? (
                <Logo className='size-8 shrink-0' aria-hidden='true' />
              ) : (
                <LogoLockup
                  className='h-8 max-w-[10.5rem] shrink-0'
                  aria-hidden='true'
                />
              )}
            </Link>
            {isMobile && <CloseSidebar />}
          </div>
        </SidebarMenuButton>
      </SidebarMenuItem>
    </SidebarMenu>
  )
}

function CloseSidebar() {
  const { toggleSidebar } = useSidebar()

  return (
    <Button
      data-sidebar='trigger'
      data-slot='sidebar-trigger'
      variant='ghost'
      size='icon'
      className='aspect-square size-8 max-md:scale-125'
      onClick={toggleSidebar}
    >
      <X />
      <span className='sr-only'>Fermer la navigation</span>
    </Button>
  )
}
