import { createFileRoute } from '@tanstack/react-router'
import { SettingsOverview } from '@/features/settings'

export const Route = createFileRoute('/_authenticated/settings/')({
  component: SettingsOverview,
})
