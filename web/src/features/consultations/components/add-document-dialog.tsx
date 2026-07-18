import { type FormEvent, useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { AlertCircle, Loader2 } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import {
  type ConsultationDocumentKind,
  attachDocument,
  consultationDocumentKinds,
  consultationDocumentsQueryKey,
  consultationQueryKey,
  consultationsQueryRoot,
  uploadDocument,
} from '../api'
import { consultationDocumentKindLabels } from '../presentation'

type AddDocumentDialogProps = {
  consultationId: string
  open: boolean
  onOpenChange: (open: boolean) => void
}

export function AddDocumentDialog({
  consultationId,
  open,
  onOpenChange,
}: AddDocumentDialogProps) {
  const queryClient = useQueryClient()
  const [file, setFile] = useState<File | null>(null)
  const [kind, setKind] = useState<ConsultationDocumentKind>('FullDce')
  const [uploadedDocumentId, setUploadedDocumentId] = useState<string | null>(
    null
  )
  const [errorMessage, setErrorMessage] = useState<string | null>(null)
  const mutation = useMutation({
    mutationFn: async () => {
      if (!file) throw new Error('Sélectionnez un fichier.')
      let documentId = uploadedDocumentId
      if (!documentId) {
        documentId = (await uploadDocument(file)).id
        setUploadedDocumentId(documentId)
      }
      return attachDocument(consultationId, documentId, kind)
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: consultationDocumentsQueryKey(consultationId),
      })
      void queryClient.invalidateQueries({
        queryKey: consultationQueryKey(consultationId),
      })
      void queryClient.invalidateQueries({ queryKey: consultationsQueryRoot })
      toast.success('Document ajouté à la consultation')
      setFile(null)
      setKind('FullDce')
      setUploadedDocumentId(null)
      setErrorMessage(null)
      onOpenChange(false)
    },
    onError: (error) => setErrorMessage(error.message),
  })

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setErrorMessage(null)
    mutation.mutate()
  }

  return (
    <Dialog
      open={open}
      onOpenChange={(nextOpen) => {
        if (!mutation.isPending) onOpenChange(nextOpen)
      }}
    >
      <DialogContent>
        <form onSubmit={submit} className='space-y-5'>
          <DialogHeader>
            <DialogTitle>Ajouter un document</DialogTitle>
            <DialogDescription>
              Téléversez une pièce puis indiquez son rôle dans la consultation.
            </DialogDescription>
          </DialogHeader>

          <div className='space-y-4'>
            <div className='space-y-2'>
              <Label htmlFor='consultation-document'>Fichier</Label>
              <Input
                id='consultation-document'
                type='file'
                accept='.pdf,.doc,.docx,.xls,.xlsx,.zip'
                required={!uploadedDocumentId}
                disabled={Boolean(uploadedDocumentId) || mutation.isPending}
                onChange={(event) => {
                  setFile(event.target.files?.[0] ?? null)
                  setUploadedDocumentId(null)
                  setErrorMessage(null)
                }}
              />
              <p className='text-xs text-muted-foreground'>
                PDF, Word, Excel ou archive ZIP.
              </p>
            </div>
            <div className='space-y-2'>
              <Label htmlFor='consultation-document-kind'>Type métier</Label>
              <Select
                value={kind}
                onValueChange={(value) =>
                  setKind(value as ConsultationDocumentKind)
                }
                disabled={mutation.isPending}
              >
                <SelectTrigger id='consultation-document-kind'>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {consultationDocumentKinds.map((value) => (
                    <SelectItem key={value} value={value}>
                      {consultationDocumentKindLabels[value]}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            {uploadedDocumentId && errorMessage && (
              <div
                role='alert'
                className='flex gap-2 rounded-md border border-destructive/40 bg-destructive/5 p-3 text-sm text-destructive'
              >
                <AlertCircle className='mt-0.5 size-4 shrink-0' />
                <p>
                  Le fichier est bien téléversé, mais son rattachement a échoué.
                  Vous pouvez réessayer sans le renvoyer. {errorMessage}
                </p>
              </div>
            )}
            {!uploadedDocumentId && errorMessage && (
              <p role='alert' className='text-sm text-destructive'>
                {errorMessage}
              </p>
            )}
          </div>

          <DialogFooter>
            <Button
              type='button'
              variant='outline'
              onClick={() => onOpenChange(false)}
              disabled={mutation.isPending}
            >
              Fermer
            </Button>
            <Button
              type='submit'
              disabled={mutation.isPending || (!file && !uploadedDocumentId)}
            >
              {mutation.isPending && <Loader2 className='animate-spin' />}
              {mutation.isPending
                ? uploadedDocumentId
                  ? 'Rattachement…'
                  : 'Téléversement…'
                : uploadedDocumentId
                  ? 'Réessayer le rattachement'
                  : 'Ajouter le document'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
