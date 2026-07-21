import { useState } from 'react'
import { Archive, Check, MessageSquare, Pencil, X } from 'lucide-react'
import { cn } from '@/lib/utils'
import { Button } from '@/components/ui/button'
import { type AssistantConversation } from '../types'

type ConversationListProps = {
  conversations: AssistantConversation[]
  activeConversationId: string | null
  onSelect: (id: string) => void
  onArchive: (id: string) => void
  onRename: (id: string, title: string) => void
}

export function ConversationList({
  conversations,
  activeConversationId,
  onSelect,
  onArchive,
  onRename,
}: ConversationListProps) {
  const [editingId, setEditingId] = useState<string | null>(null)
  const [title, setTitle] = useState('')

  if (conversations.length === 0) {
    return (
      <p className='px-2 py-3 text-xs leading-5 text-muted-foreground'>
        Aucune conversation pour le moment.
      </p>
    )
  }

  return (
    <div className='space-y-1' aria-label='Conversations'>
      {conversations.map((conversation) => {
        const active = activeConversationId === conversation.id
        const editing = editingId === conversation.id
        return (
          <div
            key={conversation.id}
            className={cn(
              'group flex min-h-9 items-center rounded-md border border-transparent',
              active && 'border-border bg-background shadow-xs'
            )}
          >
            {editing ? (
              <form
                className='flex min-w-0 flex-1 items-center gap-1 px-1'
                onSubmit={(event) => {
                  event.preventDefault()
                  if (title.trim()) onRename(conversation.id, title.trim())
                  setEditingId(null)
                }}
              >
                <input
                  autoFocus
                  value={title}
                  onChange={(event) => setTitle(event.target.value)}
                  aria-label='Nom de la conversation'
                  className='h-7 min-w-0 flex-1 rounded border bg-background px-2 text-xs outline-none focus-visible:ring-2 focus-visible:ring-ring'
                />
                <Button
                  type='submit'
                  size='icon'
                  variant='ghost'
                  className='size-7'
                  aria-label='Enregistrer le nom'
                >
                  <Check />
                </Button>
                <Button
                  type='button'
                  size='icon'
                  variant='ghost'
                  className='size-7'
                  aria-label='Annuler le renommage'
                  onClick={() => setEditingId(null)}
                >
                  <X />
                </Button>
              </form>
            ) : (
              <>
                <button
                  type='button'
                  onClick={() => onSelect(conversation.id)}
                  className='flex min-w-0 flex-1 items-center gap-2 px-2 py-2 text-left text-xs outline-none focus-visible:ring-2 focus-visible:ring-ring'
                >
                  <MessageSquare
                    aria-hidden='true'
                    className={cn(
                      'size-3.5 shrink-0 text-muted-foreground',
                      active && 'text-foreground'
                    )}
                  />
                  <span className={cn('truncate', active && 'font-medium')}>
                    {conversation.title}
                  </span>
                </button>
                <div
                  className={cn(
                    'flex pr-1 opacity-0 transition-opacity group-focus-within:opacity-100 group-hover:opacity-100',
                    active && 'opacity-100'
                  )}
                >
                  <Button
                    variant='ghost'
                    size='icon'
                    className='size-7'
                    aria-label={`Renommer ${conversation.title}`}
                    onClick={() => {
                      setTitle(conversation.title)
                      setEditingId(conversation.id)
                    }}
                  >
                    <Pencil />
                  </Button>
                  <Button
                    variant='ghost'
                    size='icon'
                    className='size-7'
                    aria-label={`Archiver ${conversation.title}`}
                    onClick={() => onArchive(conversation.id)}
                  >
                    <Archive />
                  </Button>
                </div>
              </>
            )}
          </div>
        )
      })}
    </div>
  )
}
