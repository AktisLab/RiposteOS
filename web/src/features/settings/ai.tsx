import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import {
  Bot,
  ChevronLeft,
  CircleAlert,
  CircleCheck,
  CircleDashed,
  Cpu,
  Loader2,
  Pencil,
  Plus,
  Save,
  ScrollText,
  Trash2,
  Wifi,
} from 'lucide-react'
import { toast } from 'sonner'
import { Badge } from '@/components/ui/badge'
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
import { Switch } from '@/components/ui/switch'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { StateMessage } from '@/components/state-message'
import { aiProviderHealthPresentation } from './ai-health'
import {
  type AiProvider,
  type AiProviderRequest,
  aiProviderCapabilities,
  aiTaskAssignmentQueryKey,
  assignAiTaskProvider,
  aiProvidersQueryKey,
  assignDocumentClassificationProvider,
  createAiProvider,
  deleteAiProvider,
  documentClassificationAssignmentQueryKey,
  getAiProviders,
  getAiTaskAssignment,
  getDocumentClassificationAssignment,
  testAiProviderConnection,
  updateAiProvider,
} from './api'
import { AiExecutionLog } from './components/ai-execution-log'

const emptyProvider: AiProviderRequest = {
  name: '',
  protocol: 'OpenAiCompatible',
  baseUrl: '',
  model: '',
  apiKeyEnvironmentVariableName: null,
  isEnabled: true,
  capabilities: 1,
}

const tabTriggerClassName =
  'h-12 flex-none rounded-none border-0 border-b-2 border-transparent px-0 text-muted-foreground shadow-none data-[state=active]:border-primary data-[state=active]:bg-transparent data-[state=active]:text-foreground data-[state=active]:shadow-none'

export function AiSettings() {
  const queryClient = useQueryClient()
  const [isCreateOpen, setIsCreateOpen] = useState(false)
  const [editingProvider, setEditingProvider] = useState<AiProvider | null>(
    null
  )
  const providersQuery = useQuery({
    queryKey: aiProvidersQueryKey,
    queryFn: getAiProviders,
  })
  const assignmentQuery = useQuery({
    queryKey: documentClassificationAssignmentQueryKey,
    queryFn: getDocumentClassificationAssignment,
  })
  const embeddingAssignmentQuery = useQuery({
    queryKey: aiTaskAssignmentQueryKey('DocumentEmbedding'),
    queryFn: () => getAiTaskAssignment('DocumentEmbedding'),
  })
  const chatAssignmentQuery = useQuery({
    queryKey: aiTaskAssignmentQueryKey('ConsultationChat'),
    queryFn: () => getAiTaskAssignment('ConsultationChat'),
  })
  const invalidate = () => {
    void queryClient.invalidateQueries({ queryKey: aiProvidersQueryKey })
    void queryClient.invalidateQueries({
      queryKey: documentClassificationAssignmentQueryKey,
    })
    void queryClient.invalidateQueries({
      queryKey: aiTaskAssignmentQueryKey('DocumentEmbedding'),
    })
    void queryClient.invalidateQueries({
      queryKey: aiTaskAssignmentQueryKey('ConsultationChat'),
    })
  }
  const assignMutation = useMutation({
    mutationFn: assignDocumentClassificationProvider,
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: documentClassificationAssignmentQueryKey,
      })
      toast.success('Fournisseur affecté au classement')
    },
    onError: (error) => toast.error(error.message),
  })
  const assignEmbeddingMutation = useMutation({
    mutationFn: (providerId: string) =>
      assignAiTaskProvider('DocumentEmbedding', providerId),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: aiTaskAssignmentQueryKey('DocumentEmbedding'),
      })
      toast.success('Fournisseur affecté à l’indexation')
    },
    onError: (error) => toast.error(error.message),
  })
  const assignChatMutation = useMutation({
    mutationFn: (providerId: string) =>
      assignAiTaskProvider('ConsultationChat', providerId),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: aiTaskAssignmentQueryKey('ConsultationChat'),
      })
      toast.success('Fournisseur affecté à l’assistant')
    },
    onError: (error) => toast.error(error.message),
  })

  return (
    <>
      <header className='flex flex-col gap-4 border-b pb-6'>
        <Link
          to='/settings'
          className='inline-flex w-fit items-center gap-1 text-sm text-muted-foreground transition-colors hover:text-foreground focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none'
        >
          <ChevronLeft aria-hidden='true' className='size-4' />
          Tous les paramètres
        </Link>
        <div className='flex items-start gap-4'>
          <span className='mt-0.5 flex size-11 shrink-0 items-center justify-center rounded-lg bg-violet-500/10 text-violet-700 dark:text-violet-400'>
            <Bot aria-hidden='true' className='size-5' />
          </span>
          <div className='space-y-1.5'>
            <h1 className='text-2xl font-bold tracking-tight md:text-3xl'>
              Automatisations IA
            </h1>
            <p className='max-w-2xl text-pretty text-muted-foreground'>
              Choisissez le modèle utilisé pour chaque automatisation.
            </p>
          </div>
        </div>
      </header>

      <Tabs defaultValue='configuration' className='max-w-4xl gap-0'>
        <TabsList className='h-auto w-full justify-start gap-6 overflow-x-auto rounded-none border-b bg-transparent pt-2'>
          <TabsTrigger value='configuration' className={tabTriggerClassName}>
            <Bot aria-hidden='true' />
            Configuration
          </TabsTrigger>
          <TabsTrigger value='executions' className={tabTriggerClassName}>
            <ScrollText aria-hidden='true' />
            Journal d’exécution
          </TabsTrigger>
        </TabsList>

        <TabsContent value='configuration' className='m-0 py-6 md:py-8'>
          {providersQuery.isPending ||
          assignmentQuery.isPending ||
          embeddingAssignmentQuery.isPending ||
          chatAssignmentQuery.isPending ? (
            <StateMessage
              icon={<Loader2 className='animate-spin' />}
              role='status'
            >
              Chargement de la configuration IA…
            </StateMessage>
          ) : providersQuery.isError ||
            assignmentQuery.isError ||
            embeddingAssignmentQuery.isError ||
            chatAssignmentQuery.isError ? (
            <StateMessage icon={<Bot />} role='alert'>
              {providersQuery.error?.message ??
                assignmentQuery.error?.message ??
                embeddingAssignmentQuery.error?.message ??
                chatAssignmentQuery.error?.message}
            </StateMessage>
          ) : (
            <div className='space-y-10'>
              <section
                aria-label='Affectation des modèles aux automatisations'
                className='divide-y border-y'
              >
                <ClassificationConfiguration
                  providers={providersQuery.data}
                  providerId={assignmentQuery.data?.providerId ?? null}
                  isSaving={assignMutation.isPending}
                  onSelect={assignMutation.mutate}
                />
                <TaskConfiguration
                  id='embedding-provider'
                  title='Indexation des passages'
                  providerId={embeddingAssignmentQuery.data?.providerId ?? null}
                  providers={providersQuery.data}
                  capability={aiProviderCapabilities.embedding}
                  isSaving={assignEmbeddingMutation.isPending}
                  onSelect={assignEmbeddingMutation.mutate}
                />
                <TaskConfiguration
                  id='consultation-chat-provider'
                  title='Assistant du dossier'
                  providerId={chatAssignmentQuery.data?.providerId ?? null}
                  providers={providersQuery.data}
                  capability={
                    aiProviderCapabilities.chat |
                    aiProviderCapabilities.toolCalling
                  }
                  isSaving={assignChatMutation.isPending}
                  onSelect={assignChatMutation.mutate}
                />
              </section>

              <section aria-labelledby='ai-providers'>
                <div className='flex flex-col justify-between gap-4 sm:flex-row sm:items-center'>
                  <h2
                    id='ai-providers'
                    className='text-base font-semibold tracking-tight'
                  >
                    Infrastructure IA
                  </h2>
                  <Button size='sm' onClick={() => setIsCreateOpen(true)}>
                    <Plus /> Ajouter un serveur
                  </Button>
                </div>

                <div className='mt-4 divide-y border-y'>
                  {providersQuery.data.length === 0 ? (
                    <div className='py-10 text-center'>
                      <Cpu
                        aria-hidden='true'
                        className='mx-auto size-6 text-muted-foreground'
                      />
                      <p className='mt-3 font-medium'>Aucun serveur IA</p>
                      <p className='mt-1 text-sm text-muted-foreground'>
                        Ajoutez un endpoint compatible OpenAI, puis affectez-le
                        à une automatisation.
                      </p>
                    </div>
                  ) : (
                    providersQuery.data.map((provider) => (
                      <ProviderRow
                        key={provider.id}
                        provider={provider}
                        onEdit={() => setEditingProvider(provider)}
                      />
                    ))
                  )}
                </div>
              </section>
            </div>
          )}
        </TabsContent>

        <TabsContent value='executions' className='m-0 py-6 md:py-8'>
          <AiExecutionLog />
        </TabsContent>
      </Tabs>

      <CreateProviderDialog
        open={isCreateOpen}
        onOpenChange={setIsCreateOpen}
        onCreated={invalidate}
      />
      {editingProvider && (
        <ProviderDialog
          provider={editingProvider}
          onOpenChange={(open) => !open && setEditingProvider(null)}
          onSaved={invalidate}
        />
      )}
    </>
  )
}

function ClassificationConfiguration({
  providers,
  providerId,
  isSaving,
  onSelect,
}: {
  providers: AiProvider[]
  providerId: string | null
  isSaving: boolean
  onSelect: (providerId: string) => void
}) {
  return (
    <TaskConfiguration
      id='classification-provider'
      title='Classement des documents'
      providers={providers}
      providerId={providerId}
      capability={aiProviderCapabilities.chat}
      isSaving={isSaving}
      onSelect={onSelect}
    />
  )
}

function TaskConfiguration({
  id,
  title,
  providers,
  providerId,
  capability,
  isSaving,
  onSelect,
}: {
  id: string
  title: string
  providers: AiProvider[]
  providerId: string | null
  capability: number
  isSaving: boolean
  onSelect: (providerId: string) => void
}) {
  const compatibleProviders = providers.filter(
    (provider) => (provider.capabilities & capability) === capability
  )
  return (
    <div
      aria-labelledby={id}
      className='grid gap-2 py-3 sm:grid-cols-[minmax(0,1fr)_18rem] sm:items-center'
    >
      <p id={id} className='text-sm font-medium'>
        {title}
      </p>
      <Select
        value={providerId ?? ''}
        onValueChange={onSelect}
        disabled={isSaving || compatibleProviders.length === 0}
      >
        <SelectTrigger
          aria-label={`Modèle pour ${title}`}
          className='w-full bg-muted/30'
        >
          <SelectValue placeholder='Choisir un modèle' />
        </SelectTrigger>
        <SelectContent>
          {compatibleProviders.map((provider) => (
            <SelectItem
              key={provider.id}
              value={provider.id}
              disabled={!provider.isEnabled}
            >
              {provider.model} · {provider.name}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    </div>
  )
}

function ProviderRow({
  provider,
  onEdit,
}: {
  provider: AiProvider
  onEdit: () => void
}) {
  return (
    <div className='flex flex-col gap-3 py-4 sm:flex-row sm:items-center sm:justify-between'>
      <div className='min-w-0'>
        <div className='flex flex-wrap items-center gap-x-2 gap-y-1'>
          <h3 className='truncate text-sm font-medium'>{provider.name}</h3>
          {provider.isEnabled && <ProviderHealthStatus provider={provider} />}
        </div>
        <p className='mt-1 flex flex-wrap items-center gap-x-2 gap-y-1 text-xs text-muted-foreground'>
          <span className='font-mono text-foreground/80'>{provider.model}</span>
          <span aria-hidden='true'>·</span>
          <span className='font-mono'>{provider.baseUrl}</span>
        </p>
        {!provider.isEnabled && (
          <Badge variant='secondary' className='mt-2'>
            Désactivé
          </Badge>
        )}
      </div>
      <Button
        type='button'
        variant='outline'
        size='sm'
        className='shrink-0'
        onClick={onEdit}
      >
        <Pencil /> Gérer
      </Button>
    </div>
  )
}

function ProviderHealthStatus({ provider }: { provider: AiProvider }) {
  const presentation = aiProviderHealthPresentation[provider.healthStatus]
  const Icon = {
    success: CircleCheck,
    danger: CircleAlert,
    muted: CircleDashed,
  }[presentation.tone]
  const className = {
    success: 'text-emerald-700 dark:text-emerald-400',
    danger: 'text-destructive',
    muted: 'text-muted-foreground',
  }[presentation.tone]

  return (
    <span className={`inline-flex items-center gap-1 text-xs ${className}`}>
      <Icon aria-hidden='true' className='size-3.5' />
      {presentation.label}
    </span>
  )
}

function CreateProviderDialog({
  open,
  onOpenChange,
  onCreated,
}: {
  open: boolean
  onOpenChange: (open: boolean) => void
  onCreated: () => void
}) {
  const [value, setValue] = useState<AiProviderRequest>(emptyProvider)
  const createMutation = useMutation({
    mutationFn: () => createAiProvider(value),
    onSuccess: () => {
      setValue(emptyProvider)
      onCreated()
      onOpenChange(false)
      toast.success('Fournisseur IA ajouté')
    },
    onError: (error) => toast.error(error.message),
  })

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className='max-w-xl'>
        <form
          onSubmit={(event) => {
            event.preventDefault()
            createMutation.mutate()
          }}
        >
          <DialogHeader>
            <DialogTitle>Ajouter un serveur IA</DialogTitle>
            <DialogDescription>
              RiposteOS utilise les endpoints compatibles OpenAI. La clé, si
              nécessaire, reste une variable de votre environnement.
            </DialogDescription>
          </DialogHeader>
          <div className='mt-6 grid gap-4 sm:grid-cols-2'>
            <ProviderFields
              value={value}
              onChange={setValue}
              idPrefix='new-provider'
            />
          </div>
          <DialogFooter className='mt-6'>
            <Button type='submit' disabled={createMutation.isPending}>
              {createMutation.isPending ? (
                <Loader2 className='animate-spin' />
              ) : (
                <Plus />
              )}
              Ajouter le serveur
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}

function ProviderDialog({
  provider,
  onOpenChange,
  onSaved,
}: {
  provider: AiProvider
  onOpenChange: (open: boolean) => void
  onSaved: () => void
}) {
  const [value, setValue] = useState<AiProviderRequest>({
    name: provider.name,
    protocol: provider.protocol,
    baseUrl: provider.baseUrl,
    model: provider.model,
    apiKeyEnvironmentVariableName: provider.apiKeyEnvironmentVariableName,
    isEnabled: provider.isEnabled,
    capabilities: provider.capabilities,
  })
  const updateMutation = useMutation({
    mutationFn: () => updateAiProvider(provider.id, value),
    onSuccess: () => {
      onSaved()
      onOpenChange(false)
      toast.success('Fournisseur IA enregistré')
    },
    onError: (error) => toast.error(error.message),
  })
  const deleteMutation = useMutation({
    mutationFn: () => deleteAiProvider(provider.id),
    onSuccess: () => {
      onSaved()
      onOpenChange(false)
      toast.success('Fournisseur IA supprimé')
    },
    onError: (error) => toast.error(error.message),
  })
  const testMutation = useMutation({
    mutationFn: () => testAiProviderConnection(provider.id),
    onSuccess: () => {
      onSaved()
      toast.success('Connexion au serveur vérifiée')
    },
    onError: (error) => toast.error(error.message),
  })

  return (
    <Dialog open onOpenChange={onOpenChange}>
      <DialogContent className='max-w-xl'>
        <form
          onSubmit={(event) => {
            event.preventDefault()
            updateMutation.mutate()
          }}
        >
          <DialogHeader>
            <DialogTitle>Gérer le serveur IA</DialogTitle>
            <DialogDescription>
              Modifiez ce serveur sans changer les classifications déjà
              réalisées.
            </DialogDescription>
          </DialogHeader>
          <div className='mt-6 grid gap-4 sm:grid-cols-2'>
            <ProviderFields
              value={value}
              onChange={setValue}
              idPrefix={provider.id}
            />
          </div>
          <DialogFooter className='mt-6 gap-2 sm:justify-between'>
            <Button
              type='button'
              variant='ghost'
              className='text-destructive hover:text-destructive'
              disabled={
                updateMutation.isPending ||
                deleteMutation.isPending ||
                testMutation.isPending
              }
              onClick={() => deleteMutation.mutate()}
            >
              {deleteMutation.isPending ? (
                <Loader2 className='animate-spin' />
              ) : (
                <Trash2 />
              )}
              Supprimer
            </Button>
            <Button
              type='button'
              variant='outline'
              disabled={
                !value.isEnabled ||
                updateMutation.isPending ||
                deleteMutation.isPending ||
                testMutation.isPending
              }
              onClick={() => testMutation.mutate()}
            >
              {testMutation.isPending ? (
                <Loader2 className='animate-spin' />
              ) : (
                <Wifi />
              )}
              Tester
            </Button>
            <Button
              type='submit'
              disabled={
                updateMutation.isPending ||
                deleteMutation.isPending ||
                testMutation.isPending
              }
            >
              {updateMutation.isPending ? (
                <Loader2 className='animate-spin' />
              ) : (
                <Save />
              )}
              Enregistrer
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}

function ProviderFields({
  value,
  onChange,
  idPrefix,
}: {
  value: AiProviderRequest
  onChange: React.Dispatch<React.SetStateAction<AiProviderRequest>>
  idPrefix: string
}) {
  const update = <K extends keyof AiProviderRequest>(
    key: K,
    next: AiProviderRequest[K]
  ) => onChange((current) => ({ ...current, [key]: next }))

  return (
    <>
      <div className='space-y-2'>
        <Label htmlFor={`${idPrefix}-name`}>Nom</Label>
        <Input
          id={`${idPrefix}-name`}
          value={value.name}
          onChange={(event) => update('name', event.target.value)}
          required
          placeholder='Ex. Ollama local'
        />
      </div>
      <div className='space-y-2'>
        <Label htmlFor={`${idPrefix}-model`}>Modèle</Label>
        <Input
          id={`${idPrefix}-model`}
          value={value.model}
          onChange={(event) => update('model', event.target.value)}
          required
          placeholder='Ex. gpt-oss:20b'
        />
      </div>
      <div className='space-y-2 sm:col-span-2'>
        <Label htmlFor={`${idPrefix}-base-url`}>URL compatible OpenAI</Label>
        <Input
          id={`${idPrefix}-base-url`}
          type='url'
          value={value.baseUrl}
          onChange={(event) => update('baseUrl', event.target.value)}
          required
          placeholder='https://…/v1'
        />
      </div>
      <div className='space-y-2'>
        <Label htmlFor={`${idPrefix}-api-key`}>
          Variable de clé API{' '}
          <span className='font-normal text-muted-foreground'>
            (facultatif)
          </span>
        </Label>
        <Input
          id={`${idPrefix}-api-key`}
          value={value.apiKeyEnvironmentVariableName ?? ''}
          onChange={(event) =>
            update('apiKeyEnvironmentVariableName', event.target.value || null)
          }
          placeholder='OPENAI_API_KEY'
        />
      </div>
      <div className='flex items-end gap-3 pb-2'>
        <Switch
          id={`${idPrefix}-enabled`}
          checked={value.isEnabled}
          onCheckedChange={(checked) => update('isEnabled', checked)}
        />
        <Label htmlFor={`${idPrefix}-enabled`}>Connexion active</Label>
      </div>
      <div className='space-y-2 sm:col-span-2'>
        <p className='text-sm font-medium'>Capacités</p>
        <div className='flex flex-wrap gap-5'>
          <div className='flex items-center gap-2'>
            <Switch
              id={`${idPrefix}-chat`}
              checked={(value.capabilities & 1) !== 0}
              onCheckedChange={(checked) =>
                update(
                  'capabilities',
                  checked ? value.capabilities | 1 : value.capabilities & ~1
                )
              }
            />
            <Label htmlFor={`${idPrefix}-chat`}>Conversation</Label>
          </div>
          <div className='flex items-center gap-2'>
            <Switch
              id={`${idPrefix}-embedding`}
              checked={(value.capabilities & 2) !== 0}
              onCheckedChange={(checked) =>
                update(
                  'capabilities',
                  checked ? value.capabilities | 2 : value.capabilities & ~2
                )
              }
            />
            <Label htmlFor={`${idPrefix}-embedding`}>Embeddings</Label>
          </div>
          <div className='flex items-center gap-2'>
            <Switch
              id={`${idPrefix}-tool-calling`}
              checked={(value.capabilities & 4) !== 0}
              onCheckedChange={(checked) =>
                update(
                  'capabilities',
                  checked ? value.capabilities | 4 : value.capabilities & ~4
                )
              }
            />
            <Label htmlFor={`${idPrefix}-tool-calling`}>
              Outils de recherche
            </Label>
          </div>
          <div className='flex items-center gap-2'>
            <Switch
              id={`${idPrefix}-reasoning`}
              checked={
                (value.capabilities & aiProviderCapabilities.reasoning) !== 0
              }
              onCheckedChange={(checked) =>
                update(
                  'capabilities',
                  checked
                    ? value.capabilities | aiProviderCapabilities.reasoning
                    : value.capabilities & ~aiProviderCapabilities.reasoning
                )
              }
            />
            <Label htmlFor={`${idPrefix}-reasoning`}>Raisonnement</Label>
          </div>
        </div>
        <p className='text-xs text-muted-foreground'>
          Activez le raisonnement uniquement si le serveur prend en charge l’API
          Responses et les résumés de raisonnement.
        </p>
      </div>
    </>
  )
}
