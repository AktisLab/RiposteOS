import { createFileRoute } from '@tanstack/react-router'
import { ModulePlaceholder } from '@/features/module-placeholder'

export const Route = createFileRoute('/_authenticated/knowledge')({
  component: () => <ModulePlaceholder title='Base de connaissances' />,
})
