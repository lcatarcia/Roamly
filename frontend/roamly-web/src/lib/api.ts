/**
 * Client HTTP minimale verso le API Roamly.
 *
 * Flusso CSRF (SECURITY.md §1, ADR-0003): ogni richiesta non-GET porta
 * l'header custom `X-Roamly-Request: 1` (prima barriera) E il token
 * antiforgery ottenuto da `GET /api/v1/csrf-token`, rimandato come header
 * `X-XSRF-TOKEN` (seconda barriera). I token sono legati all'identita' della
 * richiesta (anonimo vs autenticato): vanno rifatturati dopo ogni login e
 * ogni logout, prima della chiamata non-GET successiva. Per questo NON si
 * mette in cache il token oltre la singola operazione: `fetchCsrfToken()`
 * viene chiamato appena prima di ogni mutazione.
 */

const JSON_HEADERS = { 'Content-Type': 'application/json' } as const
const ROAMLY_HEADER = { 'X-Roamly-Request': '1' } as const

export class ApiError extends Error {
  readonly status: number
  readonly problem: unknown

  constructor(status: number, message: string, problem: unknown) {
    super(message)
    this.status = status
    this.problem = problem
  }
}

/** GET /api/v1/csrf-token — va richiamato prima di OGNI richiesta non-GET. */
export async function fetchCsrfToken(): Promise<string> {
  const response = await fetch('/api/v1/csrf-token', {
    method: 'GET',
    credentials: 'include',
  })

  if (!response.ok) {
    throw new ApiError(response.status, 'Impossibile ottenere il token di sicurezza.', null)
  }

  const body = (await response.json()) as { token: string }
  return body.token
}

interface MutationOptions {
  method: 'POST' | 'PUT' | 'DELETE' | 'PATCH'
  body?: unknown
}

/** Esegue una richiesta non-GET con le due barriere CSRF gia' applicate. */
async function mutate<T>(path: string, options: MutationOptions): Promise<T> {
  const csrfToken = await fetchCsrfToken()

  const response = await fetch(path, {
    method: options.method,
    credentials: 'include',
    headers: {
      ...JSON_HEADERS,
      ...ROAMLY_HEADER,
      'X-XSRF-TOKEN': csrfToken,
    },
    body: options.body !== undefined ? JSON.stringify(options.body) : undefined,
  })

  if (!response.ok) {
    let problem: unknown = null
    try {
      problem = await response.json()
    } catch {
      // corpo assente o non-JSON: si passa il messaggio generico sotto
    }

    const title =
      problem && typeof problem === 'object' && 'title' in problem && typeof (problem as { title?: unknown }).title === 'string'
        ? (problem as { title: string }).title
        : 'Si e\' verificato un errore imprevisto.'

    throw new ApiError(response.status, title, problem)
  }

  if (response.status === 204) {
    return undefined as T
  }

  return (await response.json()) as T
}

export interface LoginProfile {
  id: string
  email: string
  displayName: string | null
}

export interface MeResponse {
  id: string
  email: string
  displayName: string | null
  reportingCurrency: string
}

/** POST /api/v1/auth/login — credenziali non valide restituiscono un messaggio
 * generico (nessun oracolo su quale campo sia sbagliato). */
export function login(email: string, password: string): Promise<LoginProfile> {
  return mutate<LoginProfile>('/api/v1/auth/login', {
    method: 'POST',
    body: { email, password },
  })
}

/** POST /api/v1/auth/logout — idempotente per costruzione. */
export function logout(): Promise<void> {
  return mutate<void>('/api/v1/auth/logout', { method: 'POST' })
}

/** GET /api/v1/me — usato per sapere se la sessione cookie e' ancora valida. */
export async function fetchMe(): Promise<MeResponse | null> {
  const response = await fetch('/api/v1/me', {
    method: 'GET',
    credentials: 'include',
  })

  if (response.status === 401) {
    return null
  }

  if (!response.ok) {
    throw new ApiError(response.status, 'Impossibile leggere il profilo.', null)
  }

  return (await response.json()) as MeResponse
}
