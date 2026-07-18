import { type FormEvent, useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useNavigate } from '@tanstack/react-router'
import { Loader2 } from 'lucide-react'
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
import { consultationsQueryRoot, createConsultation } from '../api'
import { normalizeConsultationForm } from '../presentation'

type CreateConsultationDialogProps = {
  open: boolean
  onOpenChange: (open: boolean) => void
}

const initialForm = {
  title: '',
  buyer: '',
  responseDeadline: '',
  noticeUrl: '',
}

export function CreateConsultationDialog({
  open,
  onOpenChange,
}: CreateConsultationDialogProps) {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [form, setForm] = useState(initialForm)
  const mutation = useMutation({
    mutationFn: createConsultation,
    onSuccess: (consultation) => {
      void queryClient.invalidateQueries({ queryKey: consultationsQueryRoot })
      toast.success('Consultation créée')
      setForm(initialForm)
      onOpenChange(false)
      void navigate({
        to: '/consultations/$consultationId',
        params: { consultationId: consultation.id },
      })
    },
    onError: (error) => toast.error(error.message),
  })

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    mutation.mutate(normalizeConsultationForm(form))
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <form onSubmit={submit} className='space-y-5'>
          <DialogHeader>
            <DialogTitle>Ajouter une consultation</DialogTitle>
            <DialogDescription>
              Créez une consultation sans opportunité de sourcing associée.
            </DialogDescription>
          </DialogHeader>

          <div className='space-y-4'>
            <div className='space-y-2'>
              <Label htmlFor='consultation-title'>Titre</Label>
              <Input
                id='consultation-title'
                value={form.title}
                onChange={(event) =>
                  setForm((current) => ({
                    ...current,
                    title: event.target.value,
                  }))
                }
                maxLength={2000}
                required
                autoFocus
              />
            </div>
            <div className='space-y-2'>
              <Label htmlFor='consultation-buyer'>Acheteur</Label>
              <Input
                id='consultation-buyer'
                value={form.buyer}
                onChange={(event) =>
                  setForm((current) => ({
                    ...current,
                    buyer: event.target.value,
                  }))
                }
                maxLength={1000}
                required
              />
            </div>
            <div className='space-y-2'>
              <Label htmlFor='consultation-deadline'>Échéance</Label>
              <Input
                id='consultation-deadline'
                type='datetime-local'
                value={form.responseDeadline}
                onChange={(event) =>
                  setForm((current) => ({
                    ...current,
                    responseDeadline: event.target.value,
                  }))
                }
              />
              <p className='text-xs text-muted-foreground'>Facultative</p>
            </div>
            <div className='space-y-2'>
              <Label htmlFor='consultation-notice-url'>URL de l’avis</Label>
              <Input
                id='consultation-notice-url'
                type='url'
                value={form.noticeUrl}
                onChange={(event) =>
                  setForm((current) => ({
                    ...current,
                    noticeUrl: event.target.value,
                  }))
                }
                maxLength={2000}
                placeholder='https://…'
              />
              <p className='text-xs text-muted-foreground'>Facultative</p>
            </div>
          </div>

          <DialogFooter>
            <Button
              type='button'
              variant='outline'
              onClick={() => onOpenChange(false)}
              disabled={mutation.isPending}
            >
              Annuler
            </Button>
            <Button type='submit' disabled={mutation.isPending}>
              {mutation.isPending && <Loader2 className='animate-spin' />}
              {mutation.isPending ? 'Création…' : 'Créer la consultation'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
