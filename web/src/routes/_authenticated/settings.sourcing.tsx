import { createFileRoute } from '@tanstack/react-router'
import { SourcingSettings } from '@/features/settings/sourcing'

export const Route = createFileRoute('/_authenticated/settings/sourcing')({
  component: SourcingSettings,
})
