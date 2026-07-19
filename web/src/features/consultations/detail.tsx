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
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
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
  formatDateTime,
  hasActiveDocumentAnalysis,
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
      query.state.data && hasActiveDocumentAnalysis(query.state.data)
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
      <Main className='flex flex-1 flex-col gap-6'>
        <Button variant='ghost' className='w-fit' asChild>
          <Link to='/consultations'>
            <ArrowLeft />
            Retour aux consultations
          </Link>
        </Button>

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
              <div className='flex flex-wrap items-start justify-between gap-4'>
                <div className='max-w-4xl'>
                  <h1 className='text-3xl leading-tight font-bold tracking-tight'>
                    {consultationQuery.data.title}
                  </h1>
                  <p className='mt-2 text-base text-muted-foreground'>
                    {consultationQuery.data.buyer}
                  </p>
                </div>
                {consultationQuery.data.noticeUrl && (
                  <Button variant='outline' asChild>
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

              <dl className='mt-6 grid gap-x-8 gap-y-4 text-sm sm:grid-cols-2 lg:grid-cols-4'>
                <div>
                  <dt className='text-muted-foreground'>Source</dt>
                  <dd className='mt-1 font-medium'>
                    {consultationQuery.data.source ? (
                      <span className='inline-flex items-center gap-2'>
                        <SourcingSourceLogo
                          source={consultationQuery.data.source}
                          className='size-4'
                        />
                        {consultationQuery.data.source.toUpperCase()}
                        {consultationQuery.data.sourceId &&
                          ` · ${consultationQuery.data.sourceId}`}
                      </span>
                    ) : (
                      'Création manuelle'
                    )}
                  </dd>
                </div>
                <div>
                  <dt className='text-muted-foreground'>Échéance</dt>
                  <dd className='mt-1 font-medium'>
                    {formatConsultationDeadline(
                      consultationQuery.data.responseDeadline
                    )}
                  </dd>
                </div>
                <div>
                  <dt className='text-muted-foreground'>Créée le</dt>
                  <dd className='mt-1 font-medium'>
                    {formatDateTime(consultationQuery.data.createdAt)}
                  </dd>
                </div>
                <div>
                  <dt className='text-muted-foreground'>Documents</dt>
                  <dd className='mt-1 font-medium tabular-nums'>
                    {consultationQuery.data.documentCount}
                  </dd>
                </div>
              </dl>
            </section>

            <Card className='overflow-hidden py-0'>
              <CardHeader className='flex flex-row items-center justify-between gap-4 border-b py-4'>
                <div>
                  <CardTitle>Documents</CardTitle>
                  <p className='mt-1 text-sm text-muted-foreground'>
                    Pièces rattachées à cette consultation.
                  </p>
                </div>
                <Button onClick={() => setDialogOpen(true)}>
                  <Plus />
                  Ajouter un document
                </Button>
              </CardHeader>
              <CardContent className='p-0'>
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
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Nom</TableHead>
                        <TableHead>Type métier</TableHead>
                        <TableHead>Taille</TableHead>
                        <TableHead>Ajouté le</TableHead>
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
              </CardContent>
            </Card>
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
