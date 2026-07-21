import { FileSearch, FileText, Loader2, Plus } from 'lucide-react'
import { Button } from '@/components/ui/button'
import {
  Table,
  TableBody,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { StateMessage } from '@/components/state-message'
import { type ConsultationDocument } from '../api'
import { ConsultationDocumentRow } from './consultation-document-row'

const tabTriggerClassName =
  'h-11 flex-none rounded-none border-0 border-b-2 border-transparent px-0 text-muted-foreground shadow-none data-[state=active]:border-primary data-[state=active]:bg-transparent data-[state=active]:text-foreground data-[state=active]:shadow-none'

type ConsultationDocumentsProps = {
  consultationId: string
  documents: ConsultationDocument[] | undefined
  documentCount: number
  loading: boolean
  errorMessage: string | null
  onAdd: () => void
}

export function ConsultationDocuments({
  consultationId,
  documents,
  documentCount,
  loading,
  errorMessage,
  onAdd,
}: ConsultationDocumentsProps) {
  return (
    <Tabs defaultValue='documents' className='min-h-full gap-0'>
      <div className='sticky top-0 z-10 flex items-center justify-between gap-4 border-b bg-background px-4 sm:px-6 lg:px-8'>
        <TabsList className='h-auto justify-start gap-6 rounded-none bg-transparent p-0'>
          <TabsTrigger value='documents' className={tabTriggerClassName}>
            Documents
            <span className='font-normal text-muted-foreground tabular-nums'>
              {documents?.length ?? documentCount}
            </span>
          </TabsTrigger>
        </TabsList>
        <Button size='sm' onClick={onAdd}>
          <Plus aria-hidden='true' />
          <span className='hidden sm:inline'>Ajouter un document</span>
          <span className='sr-only sm:hidden'>Ajouter un document</span>
        </Button>
      </div>

      <TabsContent value='documents' className='m-0 px-4 py-4 sm:px-6 lg:px-8'>
        <section aria-label='Documents de la consultation'>
          <div className='overflow-x-auto border-y'>
            {loading ? (
              <StateMessage
                icon={<Loader2 className='animate-spin' />}
                role='status'
                className='min-h-52'
              >
                Chargement des documents…
              </StateMessage>
            ) : errorMessage ? (
              <StateMessage
                icon={<FileSearch />}
                role='alert'
                className='min-h-52'
              >
                {errorMessage}
              </StateMessage>
            ) : documents?.length === 0 ? (
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
                  {documents?.map((document) => (
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
      </TabsContent>
    </Tabs>
  )
}
