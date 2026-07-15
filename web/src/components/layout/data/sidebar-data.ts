import {
  BookOpen,
  FileText,
  LayoutDashboard,
  PenLine,
  Radar,
  Settings,
} from 'lucide-react'
import { type SidebarData } from '../types'

export const sidebarData: SidebarData = {
  navGroups: [
    {
      title: 'RiposteOS',
      items: [
        { title: 'Accueil', url: '/', icon: LayoutDashboard },
        { title: 'Sourcing', url: '/opportunities', icon: Radar },
        { title: 'Consultations', url: '/consultations', icon: FileText },
        { title: 'Connaissances', url: '/knowledge', icon: BookOpen },
        { title: 'Réponses', url: '/responses', icon: PenLine },
      ],
    },
    {
      title: 'Configuration',
      items: [{ title: 'Paramètres', url: '/settings', icon: Settings }],
    },
  ],
}
