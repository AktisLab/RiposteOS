import { z } from 'zod'
import { createFileRoute } from '@tanstack/react-router'
import { Consultations } from '@/features/consultations'
import {
  consultationDeadlineFilterValues,
  consultationSortFields,
} from '@/features/consultations/gridify'

const consultationsSearchSchema = z.object({
  page: z.number().int().min(1).optional().catch(1),
  pageSize: z.number().int().min(1).max(100).optional().catch(20),
  filter: z.string().max(200).optional().catch(''),
  source: z.array(z.string().max(30)).max(10).optional().catch([]),
  deadline: z
    .array(z.enum(consultationDeadlineFilterValues))
    .optional()
    .catch([]),
  sort: z.enum(consultationSortFields).optional().catch('responseDeadline'),
  direction: z.enum(['asc', 'desc']).optional().catch('asc'),
})

export const Route = createFileRoute('/_authenticated/consultations/')({
  validateSearch: consultationsSearchSchema,
  component: Consultations,
})
