import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Download, FileText, Loader2, RotateCcw, Trash2 } from 'lucide-react'
import { toast } from 'sonner'
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from '@/components/ui/alert-dialog'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { TableCell, TableRow } from '@/components/ui/table'
import {
  type ConsultationDocument,
  type ConsultationDocumentKind,
  consultationDocumentKinds,
  consultationDocumentsQueryKey,
  consultationQueryKey,
  consultationsQueryRoot,
  detachConsultationDocument,
  requestDocumentAnalysis,
  retryDocumentClassification,
  retryDocumentEmbedding,
  updateConsultationDocumentKind,
} from '../api'
import {
  consultationDocumentKindLabels,
  formatDateTime,
  formatFileSize,
  getDocumentProcessingPresentation,
} from '../presentation'

type ConsultationDocumentRowProps = {
  consultationId: string
  document: ConsultationDocument
}

export function ConsultationDocumentRow({
  consultationId,
  document,
}: ConsultationDocumentRowProps) {
  const queryClient = useQueryClient()
  const invalidate = (includeCount: boolean) => {
    void queryClient.invalidateQueries({
      queryKey: consultationDocumentsQueryKey(consultationId),
    })
    if (includeCount) {
      void queryClient.invalidateQueries({
        queryKey: consultationQueryKey(consultationId),
      })
      void queryClient.invalidateQueries({ queryKey: consultationsQueryRoot })
    }
  }
  const categoryMutation = useMutation({
    mutationFn: (kind: ConsultationDocumentKind) =>
      updateConsultationDocumentKind(consultationId, document.id, kind),
    onSuccess: () => {
      invalidate(false)
      toast.success('Catégorie du document modifiée')
    },
    onError: (error) => toast.error(error.message),
  })
  const detachMutation = useMutation({
    mutationFn: () => detachConsultationDocument(consultationId, document.id),
    onSuccess: () => {
      invalidate(true)
      toast.success('Document détaché de la consultation')
    },
    onError: (error) => toast.error(error.message),
  })
  const analysisMutation = useMutation({
    mutationFn: () => requestDocumentAnalysis(consultationId, document.id),
    onSuccess: () => {
      invalidate(false)
      toast.success('Analyse du document mise en file')
    },
    onError: (error) => toast.error(error.message),
  })
  const classificationMutation = useMutation({
    mutationFn: () => retryDocumentClassification(consultationId, document.id),
    onSuccess: () => {
      invalidate(false)
      toast.success('Classement du document mis en file')
    },
    onError: (error) => toast.error(error.message),
  })
  const embeddingMutation = useMutation({
    mutationFn: () => retryDocumentEmbedding(consultationId, document.id),
    onSuccess: () => {
      invalidate(false)
      toast.success('Indexation du document mise en file')
    },
    onError: (error) => toast.error(error.message),
  })
  const pending =
    categoryMutation.isPending ||
    detachMutation.isPending ||
    analysisMutation.isPending ||
    classificationMutation.isPending ||
    embeddingMutation.isPending
  const processing = getDocumentProcessingPresentation(
    document.analysis,
    document.classification,
    document.embedding
  )
  const retryMutation =
    processing.retryTarget === 'classification'
      ? classificationMutation
      : processing.retryTarget === 'embedding'
        ? embeddingMutation
        : analysisMutation

  return (
    <TableRow>
      <TableCell className='min-w-72'>
        <div className='flex items-start gap-3'>
          <FileText
            className='mt-0.5 size-4 shrink-0 text-muted-foreground'
            aria-hidden='true'
          />
          <div className='min-w-0'>
            <p className='truncate font-medium'>{document.originalFileName}</p>
            <p className='mt-0.5 text-xs text-muted-foreground'>
              {formatFileSize(document.size)} ·{' '}
              {formatDateTime(document.addedAt)}
            </p>
          </div>
        </div>
      </TableCell>
      <TableCell className='w-60'>
        <div className='space-y-1.5'>
          <Select
            value={document.kind}
            onValueChange={(value) =>
              categoryMutation.mutate(value as ConsultationDocumentKind)
            }
            disabled={pending}
          >
            <SelectTrigger
              size='sm'
              className='w-full'
              aria-label={`Catégorie de ${document.originalFileName}`}
            >
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {consultationDocumentKinds.map((kind) => (
                <SelectItem key={kind} value={kind}>
                  {consultationDocumentKindLabels[kind]}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      </TableCell>
      <TableCell className='w-44'>
        <div className='flex items-center gap-2'>
          <Badge
            variant={processing.retryTarget ? 'destructive' : 'outline'}
            className={processing.isActive ? 'gap-1.5' : ''}
          >
            {processing.isActive && <Loader2 className='animate-spin' />}
            {processing.label}
          </Badge>
          {processing.actionLabel && (
            <Button
              variant='ghost'
              size='icon'
              className='size-7'
              disabled={pending}
              onClick={() => retryMutation.mutate()}
              aria-label={`Relancer le traitement de ${document.originalFileName}`}
              title='Relancer le traitement'
            >
              {retryMutation.isPending ? (
                <Loader2 className='animate-spin' />
              ) : (
                <RotateCcw />
              )}
            </Button>
          )}
        </div>
      </TableCell>
      <TableCell className='w-20 text-right'>
        <div className='flex justify-end gap-1'>
          <Button
            variant='ghost'
            size='icon'
            className='size-8'
            title='Télécharger le document'
            asChild
          >
            <a
              href={document.downloadUrl}
              download
              aria-label={`Télécharger ${document.originalFileName}`}
            >
              <Download />
            </a>
          </Button>
          <AlertDialog>
            <AlertDialogTrigger asChild>
              <Button
                variant='ghost'
                size='icon'
                className='size-8'
                disabled={pending}
                aria-label={`Détacher ${document.originalFileName}`}
                title='Détacher le document'
              >
                {detachMutation.isPending ? (
                  <Loader2 className='animate-spin' />
                ) : (
                  <Trash2 />
                )}
              </Button>
            </AlertDialogTrigger>
            <AlertDialogContent>
              <AlertDialogHeader>
                <AlertDialogTitle>Détacher ce document ?</AlertDialogTitle>
                <AlertDialogDescription>
                  {document.originalFileName} restera stocké, mais ne fera plus
                  partie de cette consultation.
                </AlertDialogDescription>
              </AlertDialogHeader>
              <AlertDialogFooter>
                <AlertDialogCancel disabled={detachMutation.isPending}>
                  Annuler
                </AlertDialogCancel>
                <AlertDialogAction
                  className='bg-destructive text-white hover:bg-destructive/90'
                  disabled={detachMutation.isPending}
                  onClick={() => detachMutation.mutate()}
                >
                  Détacher
                </AlertDialogAction>
              </AlertDialogFooter>
            </AlertDialogContent>
          </AlertDialog>
        </div>
      </TableCell>
    </TableRow>
  )
}
