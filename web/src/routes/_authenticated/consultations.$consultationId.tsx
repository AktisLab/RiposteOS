import { createFileRoute } from '@tanstack/react-router'
import { ConsultationDetail } from '@/features/consultations/detail'

export const Route = createFileRoute(
  '/_authenticated/consultations/$consultationId'
)({
  component: ConsultationDetail,
})
