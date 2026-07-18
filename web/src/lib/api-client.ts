type ApiRequestOptions = RequestInit & {
  errorMessage: string
}

export async function apiRequest<T>(
  path: string,
  { errorMessage, ...options }: ApiRequestOptions
): Promise<T> {
  const response = await fetch(path, options)

  if (!response.ok) {
    throw new Error(await getApiErrorMessage(response, errorMessage))
  }

  if (response.status === 204) return undefined as T

  return response.json()
}

export async function getApiErrorMessage(response: Response, fallback: string) {
  const body = await response.text()
  if (!body) return fallback

  try {
    const payload: unknown = JSON.parse(body)
    if (typeof payload === 'string' && payload.trim()) return payload
    if (!isRecord(payload)) return fallback
    if (typeof payload.detail === 'string' && payload.detail.trim()) {
      return payload.detail
    }

    const validationMessage = firstValidationMessage(payload.errors)
    if (validationMessage) return validationMessage
    if (typeof payload.message === 'string' && payload.message.trim()) {
      return payload.message
    }
    if (typeof payload.title === 'string' && payload.title.trim()) {
      return payload.title
    }
  } catch {
    if (response.headers.get('content-type')?.startsWith('text/plain')) {
      return body
    }
  }

  return fallback
}

function firstValidationMessage(value: unknown) {
  if (!isRecord(value)) return null
  for (const messages of Object.values(value)) {
    if (Array.isArray(messages)) {
      const message = messages.find(
        (candidate): candidate is string =>
          typeof candidate === 'string' && candidate.trim().length > 0
      )
      if (message) return message
    }
  }
  return null
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null
}
