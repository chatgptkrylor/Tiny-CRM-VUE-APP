import { state } from '../auth'

export class ApiError extends Error {
  status: number
  errors?: Record<string, string[]>

  constructor(status: number, message: string, errors?: Record<string, string[]>) {
    super(message)
    this.status = status
    this.errors = errors
  }
}

// Requests exempt from the global 401 handler below:
//  - the login POST itself: a 401 there is a normal "wrong credentials" result
//    that the login form renders inline (useAuth().login() must keep returning
//    that error string, not trigger a redirect).
//  - the auth probe used by the router guard on every navigation: its 401 on
//    first load is already handled by that guard's own redirect, so treating
//    it here too would race/loop with it (e.g. reloading the login page from
//    itself).
const EXEMPT_FROM_401_REDIRECT = new Set(['/api/auth/login', '/api/auth/me'])

export async function api<T>(path: string, init: RequestInit = {}): Promise<T> {
  const res = await fetch(path, {
    credentials: 'same-origin',
    headers: { 'Content-Type': 'application/json', ...(init.headers ?? {}) },
    ...init,
  })

  if (res.status === 401) {
    if (!EXEMPT_FROM_401_REDIRECT.has(path)) {
      // Spec §7: a 401 from any other call (e.g. the 30-minute sliding session
      // cookie expiring mid-use) clears auth state and sends the user back to
      // sign in, instead of leaving a view showing stale data forever.
      state.user = null
      window.location.href =
        '/login?returnUrl=' + encodeURIComponent(window.location.pathname + window.location.search)
    }
    throw new ApiError(401, 'Unauthorized')
  }
  if (res.status === 400) {
    const body = await res.json().catch(() => ({}))
    throw new ApiError(400, 'Validation failed', body.errors)
  }
  if (!res.ok) throw new ApiError(res.status, res.statusText)
  if (res.status === 204) return undefined as T
  return (await res.json()) as T
}
