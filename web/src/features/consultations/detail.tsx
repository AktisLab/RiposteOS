import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { getRouteApi, Link } from '@tanstack/react-router'
import {
  ArrowLeft,
  ArrowUpRight,
  FileSearch,
  FileText,
  Loader2,
  Plus,
} from 'lucide-react'
import { Button } from '@/components/ui/button'
import {
  Table,
  TableBody,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { ConfigDrawer } from '@/components/config-drawer'
import { Header } from '@/components/layout/header'
import { Main } from '@/components/layout/main'
import { Search } from '@/components/search'
import { SourcingSourceLogo } from '@/components/sourcing-source-logo'
import { StateMessage } from '@/components/state-message'
import { ThemeSwitch } from '@/components/theme-switch'
import {
  consultationDocumentsQueryKey,
  consultationQueryKey,
  getConsultation,
  getConsultationDocuments,
} from './api'
import { AddDocumentDialog } from './components/add-document-dialog'
import { ConsultationDocumentRow } from './components/consultation-document-row'
import {
  formatConsultationDeadline,
  hasActiveDocumentProcessing,
} from './presentation'

const route = getRouteApi('/_authenticated/consultations/$consultationId')

export function ConsultationDetail() {
  const { consultationId } = route.useParams()
  const [dialogOpen, setDialogOpen] = useState(false)
  const consultationQuery = useQuery({
    queryKey: consultationQueryKey(consultationId),
    queryFn: () => getConsultation(consultationId),
  })
  const documentsQuery = useQuery({
    queryKey: consultationDocumentsQueryKey(consultationId),
    queryFn: () => getConsultationDocuments(consultationId),
    refetchInterval: (query) =>
      query.state.data && hasActiveDocumentProcessing(query.state.data)
        ? 2_000
        : false,
  })

  return (
    <>
      <Header>
        <Search />
        <ThemeSwitch />
        <ConfigDrawer />
      </Header>
      <Main className='flex flex-1 flex-col gap-8'>
        {consultationQuery.isPending ? (
          <StateMessage
            icon={<Loader2 className='animate-spin' />}
            role='status'
          >
            Chargement de la consultation…
          </StateMessage>
        ) : consultationQuery.isError ? (
          <StateMessage icon={<FileSearch />} role='alert'>
            {consultationQuery.error.message}
          </StateMessage>
        ) : (
          <>
            <section className='border-b pb-6'>
              <Button
                variant='ghost'
                size='sm'
                className='-ml-2 text-muted-foreground'
                asChild
              >
                <Link to='/consultations'>
                  <ArrowLeft />
                  Consultations
                </Link>
              </Button>
              <div className='flex flex-wrap items-start justify-between gap-4'>
                <div className='mt-4 max-w-4xl'>
                  <h1 className='text-3xl leading-tight font-bold tracking-tight'>
                    {consultationQuery.data.title}
                  </h1>
                  {consultationQuery.data.buyer && (
                    <p className='mt-2 text-base text-muted-foreground'>
                      {consultationQuery.data.buyer}
                    </p>
                  )}
                </div>
                {consultationQuery.data.noticeUrl && (
                  <Button variant='outline' className='mt-4' asChild>
                    <a
                      href={consultationQuery.data.noticeUrl}
                      target='_blank'
                      rel='noreferrer'
                    >
                      Ouvrir l’avis
                      <ArrowUpRight />
                    </a>
                  </Button>
                )}
              </div>

              <dl className='mt-6 flex flex-wrap gap-x-10 gap-y-4 text-sm'>
                <div>
                  <dt className='text-muted-foreground'>Échéance</dt>
                  <dd className='mt-1 font-medium'>
                    {formatConsultationDeadline(
                      consultationQuery.data.responseDeadline
                    )}
                  </dd>
                </div>
                {consultationQuery.data.source && (
                  <div>
                    <dt className='text-muted-foreground'>Source</dt>
                    <dd className='mt-1 font-medium'>
                      <span className='inline-flex items-center gap-2'>
                        <SourcingSourceLogo
                          source={consultationQuery.data.source}
                          className='size-4'
                        />
                        {consultationQuery.data.source.toUpperCase()}
                      </span>
                    </dd>
                  </div>
                )}
              </dl>
            </section>

            <section aria-labelledby='documents-heading'>
              <div className='flex flex-wrap items-center justify-between gap-4'>
                <h2
                  id='documents-heading'
                  className='text-lg font-semibold tracking-tight'
                >
                  Documents
                  <span className='ml-2 text-sm font-normal text-muted-foreground tabular-nums'>
                    {documentsQuery.data?.length ??
                      consultationQuery.data.documentCount}
                  </span>
                </h2>
                <Button onClick={() => setDialogOpen(true)}>
                  <Plus />
                  Ajouter un document
                </Button>
              </div>
              <div className='mt-4 border-y'>
                {documentsQuery.isPending ? (
                  <StateMessage
                    icon={<Loader2 className='animate-spin' />}
                    role='status'
                    className='min-h-52'
                  >
                    Chargement des documents…
                  </StateMessage>
                ) : documentsQuery.isError ? (
                  <StateMessage
                    icon={<FileSearch />}
                    role='alert'
                    className='min-h-52'
                  >
                    {documentsQuery.error.message}
                  </StateMessage>
                ) : documentsQuery.data.length === 0 ? (
                  <StateMessage icon={<FileText />} className='min-h-52'>
                    Aucun document rattaché. Ajoutez le DCE pour commencer.
                  </StateMessage>
                ) : (
                  <Table className='min-w-180'>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Document</TableHead>
                        <TableHead>Catégorie</TableHead>
                        <TableHead>Analyse</TableHead>
                        <TableHead className='text-right'>Actions</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {documentsQuery.data.map((document) => (
                        <ConsultationDocumentRow
                          key={document.id}
                          consultationId={consultationId}
                          document={document}
                        />
                      ))}
                    </TableBody>
                  </Table>
                )}
              </div>
            </section>
          </>
        )}
      </Main>

      <AddDocumentDialog
        consultationId={consultationId}
        open={dialogOpen}
        onOpenChange={setDialogOpen}
      />
    </>
  )
}
