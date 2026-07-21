import { type AssistantStreamEvent } from './types.ts'

function parseEvent(block: string) {
  const data = block
    .split(/\r?\n/)
    .find((line) => line.startsWith('data: '))
    ?.slice(6)
  return data ? (JSON.parse(data) as AssistantStreamEvent) : null
}

export function createAssistantEventParser(
  onEvent: (event: AssistantStreamEvent) => void
) {
  let buffer = ''

  const flush = (complete: boolean) => {
    const blocks = buffer.split(/\r?\n\r?\n/)
    buffer = complete ? '' : (blocks.pop() ?? '')
    for (const block of blocks) {
      const event = parseEvent(block)
      if (event) onEvent(event)
    }
    if (complete && buffer.trim()) {
      const event = parseEvent(buffer)
      if (event) onEvent(event)
      buffer = ''
    }
  }

  return {
    push(chunk: string) {
      buffer += chunk
      flush(false)
    },
    finish() {
      const finalBlock = buffer
      buffer = ''
      const event = finalBlock.trim() ? parseEvent(finalBlock) : null
      if (event) onEvent(event)
    },
  }
}
