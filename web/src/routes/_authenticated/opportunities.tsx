import { z } from 'zod'
import { createFileRoute } from '@tanstack/react-router'
import { Opportunities } from '@/features/opportunities'
import {
  deadlineFilterValues,
  opportunitySortFields,
} from '@/features/opportunities/gridify'

const opportunitySearchSchema = z.object({
  page: z.number().int().min(1).optional().catch(1),
  pageSize: z.number().int().min(1).max(100).optional().catch(20),
  filter: z.string().max(200).optional().catch(''),
  source: z.array(z.string().max(30)).max(10).optional().catch([]),
  deadline: z.array(z.enum(deadlineFilterValues)).optional().catch([]),
  status: z
    .array(z.enum(['ToQualify', 'Retained', 'Dismissed']))
    .optional()
    .catch([]),
  highRelevance: z.boolean().optional().catch(false),
  preferredTerritory: z.boolean().optional().catch(false),
  buyer: z.string().max(100).optional().catch(''),
  department: z.string().max(3).optional().catch(''),
  cpv: z.string().max(8).optional().catch(''),
  sort: z.enum(opportunitySortFields).optional().catch('matchScore'),
  direction: z.enum(['asc', 'desc']).optional().catch('desc'),
})

export const Route = createFileRoute('/_authenticated/opportunities')({
  validateSearch: opportunitySearchSchema,
  component: Opportunities,
})
