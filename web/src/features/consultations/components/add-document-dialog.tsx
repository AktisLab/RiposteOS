import { type FormEvent, useRef, useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
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
  attachDocument,
  consultationDocumentsQueryKey,
  consultationQueryKey,
  consultationsQueryRoot,
  uploadDocument,
} from '../api'

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
  const inputRef = useRef<HTMLInputElement>(null)
  const [files, setFiles] = useState<File[]>([])
  const [completedCount, setCompletedCount] = useState(0)
  const [errors, setErrors] = useState<string[]>([])
  const [isImporting, setIsImporting] = useState(false)

  function close() {
    if (inputRef.current) inputRef.current.value = ''
    setFiles([])
    setCompletedCount(0)
    setErrors([])
    onOpenChange(false)
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (files.length === 0) return

    setIsImporting(true)
    setCompletedCount(0)
    setErrors([])
    const importErrors: string[] = []

    for (const [index, file] of files.entries()) {
      try {
        const uploadedDocument = await uploadDocument(file)
        await attachDocument(consultationId, uploadedDocument.id)
      } catch (error) {
        importErrors.push(
          `${file.name} : ${error instanceof Error ? error.message : 'échec de l’ajout.'}`
        )
      }
      setCompletedCount(index + 1)
    }

    setErrors(importErrors)
    setIsImporting(false)
    void queryClient.invalidateQueries({
      queryKey: consultationDocumentsQueryKey(consultationId),
    })
    void queryClient.invalidateQueries({
      queryKey: consultationQueryKey(consultationId),
    })
    void queryClient.invalidateQueries({ queryKey: consultationsQueryRoot })
    if (importErrors.length === 0) {
      toast.success(
        files.length === 1
          ? 'Document ajouté à la consultation'
          : `${files.length} documents ajoutés à la consultation`
      )
      close()
    }
  }

  return (
    <Dialog
      open={open}
      onOpenChange={(nextOpen) => {
        if (!isImporting) {
          if (nextOpen) onOpenChange(true)
          else close()
        }
      }}
    >
      <DialogContent>
        <form onSubmit={submit} className='space-y-5'>
          <DialogHeader>
            <DialogTitle>Ajouter un document</DialogTitle>
            <DialogDescription>
              Ajoutez les pièces du DCE. Leur catégorie sera déterminée après
              analyse.
            </DialogDescription>
          </DialogHeader>

          <div className='space-y-4'>
            <div className='space-y-2'>
              <Label htmlFor='consultation-document'>Fichier</Label>
              <Input
                ref={inputRef}
                id='consultation-document'
                type='file'
                accept='.pdf,.doc,.docx,.xls,.xlsx,.zip'
                multiple
                required={files.length === 0}
                disabled={isImporting}
                onChange={(event) => {
                  setFiles(Array.from(event.target.files ?? []))
                  setCompletedCount(0)
                  setErrors([])
                }}
              />
              <p className='text-xs text-muted-foreground'>
                PDF, Word, Excel ou archive ZIP. Les fichiers sont ajoutés dans
                l’ordre de sélection.
              </p>
            </div>
            {isImporting && (
              <p role='status' className='text-sm text-muted-foreground'>
                {completedCount} documents sur {files.length} ajoutés
              </p>
            )}
            {errors.length > 0 && (
              <div
                role='alert'
                className='flex gap-2 rounded-md border border-destructive/40 bg-destructive/5 p-3 text-sm text-destructive'
              >
                <AlertCircle className='mt-0.5 size-4 shrink-0' />
                <div>
                  <p>Certains documents n’ont pas pu être ajoutés.</p>
                  <ul className='mt-1 list-disc pl-4'>
                    {errors.map((error) => (
                      <li key={error}>{error}</li>
                    ))}
                  </ul>
                </div>
              </div>
            )}
          </div>

          <DialogFooter>
            <Button
              type='button'
              variant='outline'
              onClick={close}
              disabled={isImporting}
            >
              Fermer
            </Button>
            <Button type='submit' disabled={isImporting || files.length === 0}>
              {isImporting && <Loader2 className='animate-spin' />}
              {isImporting
                ? 'Ajout en cours…'
                : files.length > 1
                  ? `Ajouter ${files.length} documents`
                  : 'Ajouter le document'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
