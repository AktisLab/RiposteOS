import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { getRouteApi } from '@tanstack/react-router'
import { FileSearch, Loader2 } from 'lucide-react'
import { ConfigDrawer } from '@/components/config-drawer'
import { Header } from '@/components/layout/header'
import { Main } from '@/components/layout/main'
import { Search } from '@/components/search'
import { StateMessage } from '@/components/state-message'
import { ThemeSwitch } from '@/components/theme-switch'
import {
  consultationDocumentsQueryKey,
  consultationQueryKey,
  getConsultation,
  getConsultationDocuments,
} from './api'
import { ConsultationAssistant, type AssistantEvidence } from './assistant'
import { AddDocumentDialog } from './components/add-document-dialog'
import { ConsultationDocuments } from './components/consultation-documents'
import { ConsultationHeader } from './components/consultation-header'
import { ConsultationWorkspace } from './components/consultation-workspace'
import { DocumentAnalysisDrawer } from './components/document-analysis-drawer'
import { hasActiveDocumentProcessing } from './presentation'

const route = getRouteApi('/_authenticated/consultations/$consultationId')

export function ConsultationDetail() {
  const { consultationId } = route.useParams()
  const search = route.useSearch()
  const navigate = route.useNavigate()
  const [dialogOpen, setDialogOpen] = useState(false)
  const [assistantOpen, setAssistantOpen] = useState(
    () => window.matchMedia('(min-width: 1280px)').matches
  )
  const [selectedEvidence, setSelectedEvidence] =
    useState<AssistantEvidence | null>(null)
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
  const indexedDocumentAvailable =
    documentsQuery.data?.some(
      (document) =>
        document.embedding.status === 'Completed' &&
        document.embedding.indexedPassageCount > 0
    ) ?? false
  const assistantUnavailableReason = documentsQuery.isPending
    ? 'Chargement des documents…'
    : documentsQuery.data?.length === 0
      ? 'Ajoutez puis analysez un document pour interroger le dossier.'
      : hasActiveDocumentProcessing(documentsQuery.data ?? [])
        ? 'Les documents sont en cours d’analyse ou d’indexation.'
        : 'Aucun passage indexé n’est encore disponible.'

  return (
    <>
      <Header>
        <Search />
        <ThemeSwitch />
        <ConfigDrawer />
      </Header>
      <Main fixed fluid className='p-0 sm:p-0 lg:p-0'>
        {consultationQuery.isPending ? (
          <StateMessage
            icon={<Loader2 className='animate-spin' />}
            role='status'
            className='flex-1'
          >
            Chargement de la consultation…
          </StateMessage>
        ) : consultationQuery.isError ? (
          <StateMessage icon={<FileSearch />} role='alert' className='flex-1'>
            {consultationQuery.error.message}
          </StateMessage>
        ) : (
          <div className='flex min-h-0 flex-1 flex-col'>
            <ConsultationHeader
              consultation={consultationQuery.data}
              assistantOpen={assistantOpen}
              onAssistantOpenChange={setAssistantOpen}
            />
            <ConsultationWorkspace
              assistantOpen={assistantOpen}
              onAssistantOpenChange={setAssistantOpen}
              assistant={
                <ConsultationAssistant
                  consultationId={consultationId}
                  activeConversationId={search.conversation ?? null}
                  onActiveConversationChange={(conversationId) =>
                    void navigate({
                      search: (current) => ({
                        ...current,
                        conversation: conversationId ?? undefined,
                      }),
                      replace: true,
                    })
                  }
                  canAsk={indexedDocumentAvailable}
                  unavailableReason={assistantUnavailableReason}
                  onEvidenceSelected={setSelectedEvidence}
                />
              }
            >
              <ConsultationDocuments
                consultationId={consultationId}
                documents={documentsQuery.data}
                documentCount={consultationQuery.data.documentCount}
                loading={documentsQuery.isPending}
                errorMessage={documentsQuery.error?.message ?? null}
                onAdd={() => setDialogOpen(true)}
              />
            </ConsultationWorkspace>
          </div>
        )}
      </Main>

      <AddDocumentDialog
        consultationId={consultationId}
        open={dialogOpen}
        onOpenChange={setDialogOpen}
      />
      {selectedEvidence && (
        <DocumentAnalysisDrawer
          consultationId={consultationId}
          documentId={selectedEvidence.documentId}
          documentName={selectedEvidence.documentName}
          targetOrdinal={selectedEvidence.ordinal}
          open
          onOpenChange={(open) => {
            if (!open) setSelectedEvidence(null)
          }}
        />
      )}
    </>
  )
}
