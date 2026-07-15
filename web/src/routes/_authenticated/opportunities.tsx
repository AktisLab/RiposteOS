import { createFileRoute } from '@tanstack/react-router'
import { ModulePlaceholder } from '@/features/module-placeholder'

export const Route = createFileRoute('/_authenticated/opportunities')({
  component: () => <ModulePlaceholder title='Sourcing' />,
})
