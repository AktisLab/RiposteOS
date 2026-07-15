import { createFileRoute } from '@tanstack/react-router'
import { ModulePlaceholder } from '@/features/module-placeholder'

export const Route = createFileRoute('/_authenticated/consultations')({
  component: () => <ModulePlaceholder title='Consultations' />,
})
