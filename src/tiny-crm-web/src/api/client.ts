export class ApiError extends Error {
  status: number
  errors?: Record<string, string[]>

  constructor(status: number, message: string, errors?: Record<string, string[]>) {
    super(message)
    this.status = status
    this.errors = errors
  }
}

export async function api<T>(path: string, init: RequestInit = {}): Promise<T> {
  const res = await fetch(path, {
    credentials: 'same-origin',
    headers: { 'Content-Type': 'application/json', ...(init.headers ?? {}) },
    ...init,
  })

  if (res.status === 401) throw new ApiError(401, 'Unauthorized')
  if (res.status === 400) {
    const body = await res.json().catch(() => ({}))
    throw new ApiError(400, 'Validation failed', body.errors)
  }
  if (!res.ok) throw new ApiError(res.status, res.statusText)
  if (res.status === 204) return undefined as T
  return (await res.json()) as T
}
