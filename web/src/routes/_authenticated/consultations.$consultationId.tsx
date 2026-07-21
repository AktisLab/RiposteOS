import { z } from 'zod'
import { createFileRoute } from '@tanstack/react-router'
import { ConsultationDetail } from '@/features/consultations/detail'

export const Route = createFileRoute(
  '/_authenticated/consultations/$consultationId'
)({
  validateSearch: z.object({
    conversation: z.string().uuid().optional().catch(undefined),
  }),
  component: ConsultationDetail,
})
