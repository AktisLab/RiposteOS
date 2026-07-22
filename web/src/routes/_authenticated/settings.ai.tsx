import { z } from 'zod'
import { createFileRoute } from '@tanstack/react-router'
import { AiSettings } from '@/features/settings/ai'

export const Route = createFileRoute('/_authenticated/settings/ai')({
  validateSearch: z.object({
    executionPage: z.number().int().min(1).optional().catch(1),
    operation: z
      .enum([
        'all',
        'DocumentAnalysis',
        'DocumentClassification',
        'DocumentEmbedding',
        'ConsultationChat',
      ])
      .optional()
      .catch('all'),
    status: z
      .enum(['all', 'Running', 'Completed', 'Failed', 'NotConfigured'])
      .optional()
      .catch('all'),
  }),
  component: AiSettings,
})
