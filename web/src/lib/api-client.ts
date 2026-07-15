type ApiRequestOptions = RequestInit & {
  errorMessage: string
}

export async function apiRequest<T>(
  path: string,
  { errorMessage, ...options }: ApiRequestOptions
): Promise<T> {
  const response = await fetch(path, options)

  if (!response.ok) {
    throw new Error(errorMessage)
  }

  return response.json()
}
