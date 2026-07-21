import { Link } from '@tanstack/react-router'
import {
  ArrowLeft,
  ArrowUpRight,
  PanelRightClose,
  PanelRightOpen,
} from 'lucide-react'
import { Button } from '@/components/ui/button'
import { SourcingSourceLogo } from '@/components/sourcing-source-logo'
import { type Consultation } from '../api'
import { formatConsultationDeadline } from '../presentation'

type ConsultationHeaderProps = {
  consultation: Consultation
  assistantOpen: boolean
  onAssistantOpenChange: (open: boolean) => void
}

export function ConsultationHeader({
  consultation,
  assistantOpen,
  onAssistantOpenChange,
}: ConsultationHeaderProps) {
  return (
    <header className='shrink-0 border-b px-4 py-4 sm:px-6 lg:px-8'>
      <div className='flex flex-wrap items-start justify-between gap-4'>
        <div className='min-w-0'>
          <Button
            variant='ghost'
            size='sm'
            className='-ml-2 h-7 text-muted-foreground'
            asChild
          >
            <Link to='/consultations'>
              <ArrowLeft aria-hidden='true' />
              Consultations
            </Link>
          </Button>
          <div className='mt-2 flex min-w-0 flex-wrap items-baseline gap-x-3 gap-y-1'>
            <h1 className='truncate text-xl font-semibold tracking-tight sm:text-2xl'>
              {consultation.title}
            </h1>
            {consultation.buyer && (
              <p className='truncate text-sm text-muted-foreground'>
                {consultation.buyer}
              </p>
            )}
          </div>
        </div>

        <div className='flex shrink-0 items-center gap-2'>
          {consultation.noticeUrl && (
            <Button variant='outline' size='sm' asChild>
              <a href={consultation.noticeUrl} target='_blank' rel='noreferrer'>
                <span className='hidden sm:inline'>Ouvrir l’avis</span>
                <ArrowUpRight aria-hidden='true' />
              </a>
            </Button>
          )}
          <Button
            variant={assistantOpen ? 'secondary' : 'default'}
            size='sm'
            aria-expanded={assistantOpen}
            aria-controls='consultation-assistant'
            onClick={() => onAssistantOpenChange(!assistantOpen)}
          >
            {assistantOpen ? (
              <PanelRightClose aria-hidden='true' />
            ) : (
              <PanelRightOpen aria-hidden='true' />
            )}
            <span className='hidden sm:inline'>Assistant IA</span>
            <span className='sr-only sm:hidden'>Assistant IA</span>
          </Button>
        </div>
      </div>

      <dl className='mt-3 flex flex-wrap items-center gap-x-6 gap-y-2 text-xs'>
        <div className='flex items-center gap-2'>
          <dt className='text-muted-foreground'>Échéance</dt>
          <dd className='font-medium tabular-nums'>
            {formatConsultationDeadline(consultation.responseDeadline)}
          </dd>
        </div>
        {consultation.source && (
          <div className='flex items-center gap-2'>
            <dt className='text-muted-foreground'>Source</dt>
            <dd className='inline-flex items-center gap-1.5 font-medium'>
              <SourcingSourceLogo
                source={consultation.source}
                className='size-3.5'
              />
              {consultation.source.toUpperCase()}
            </dd>
          </div>
        )}
      </dl>
    </header>
  )
}
