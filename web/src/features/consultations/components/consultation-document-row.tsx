import { useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Bug, Download, Loader2, RotateCcw, Trash2 } from 'lucide-react'
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
  updateConsultationDocumentKind,
} from '../api'
import {
  consultationDocumentKindLabels,
  formatDateTime,
  formatFileSize,
  getDocumentAnalysisPresentation,
} from '../presentation'
import { DocumentAnalysisDrawer } from './document-analysis-drawer'

type ConsultationDocumentRowProps = {
  consultationId: string
  document: ConsultationDocument
}

export function ConsultationDocumentRow({
  consultationId,
  document,
}: ConsultationDocumentRowProps) {
  const queryClient = useQueryClient()
  const [debugOpen, setDebugOpen] = useState(false)
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
  const pending =
    categoryMutation.isPending ||
    detachMutation.isPending ||
    analysisMutation.isPending
  const analysis = getDocumentAnalysisPresentation(document.analysis)

  return (
    <>
      <TableRow>
        <TableCell>
          <p className='font-medium'>{document.originalFileName}</p>
          <p className='text-xs text-muted-foreground'>
            {document.contentType}
          </p>
        </TableCell>
        <TableCell>
          <Select
            value={document.kind}
            onValueChange={(value) =>
              categoryMutation.mutate(value as ConsultationDocumentKind)
            }
            disabled={pending}
          >
            <SelectTrigger
              size='sm'
              className='min-w-52'
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
        </TableCell>
        <TableCell className='tabular-nums'>
          {formatFileSize(document.size)}
        </TableCell>
        <TableCell>{formatDateTime(document.addedAt)}</TableCell>
        <TableCell>
          <div className='flex min-w-48 items-center gap-2'>
            <Badge
              variant={
                document.analysis.status === 'Failed'
                  ? 'destructive'
                  : 'outline'
              }
              className={
                document.analysis.status === 'Running' ? 'gap-1.5' : ''
              }
            >
              {document.analysis.status === 'Running' && (
                <Loader2 className='animate-spin' />
              )}
              {analysis.label}
            </Badge>
            {analysis.actionLabel && (
              <Button
                variant='ghost'
                size='sm'
                disabled={pending}
                onClick={() => analysisMutation.mutate()}
              >
                {analysisMutation.isPending ? (
                  <Loader2 className='animate-spin' />
                ) : (
                  <RotateCcw />
                )}
                {analysis.actionLabel}
              </Button>
            )}
            {import.meta.env.DEV &&
              document.analysis.status === 'Completed' && (
                <Button
                  variant='ghost'
                  size='sm'
                  onClick={() => setDebugOpen(true)}
                >
                  <Bug />
                  DEBUG
                </Button>
              )}
          </div>
        </TableCell>
        <TableCell className='text-right'>
          <div className='flex justify-end gap-1'>
            <Button variant='ghost' size='sm' asChild>
              <a href={document.downloadUrl} download>
                <Download />
                Télécharger
              </a>
            </Button>
            <AlertDialog>
              <AlertDialogTrigger asChild>
                <Button
                  variant='ghost'
                  size='icon'
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
                    {document.originalFileName} restera stocké, mais ne fera
                    plus partie de cette consultation.
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
      {import.meta.env.DEV && (
        <DocumentAnalysisDrawer
          consultationId={consultationId}
          documentId={document.id}
          documentName={document.originalFileName}
          open={debugOpen}
          onOpenChange={setDebugOpen}
        />
      )}
    </>
  )
}
