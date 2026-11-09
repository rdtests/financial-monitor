import { ApiError } from './ApiError'

const SERVER_UNAVAILABLE = 'Server is unavailable.'
const SERVER_ERROR = 'Server error, please try again.'

// 502/503/504 are what the Vite dev proxy (or an ingress) returns when the backend is down.
const UNAVAILABLE_STATUSES = new Set([502, 503, 504])

export function apiGet<T>(path: string): Promise<T> {
  return request<T>(path)
}

export function apiPost<T>(path: string, body: unknown): Promise<T> {
  return request<T>(path, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })
}

/** Plain fetch that turns every failure into an ApiError. */
async function request<T>(path: string, init?: RequestInit): Promise<T> {
  let response: Response
  try {
    response = await fetch(path, init)
  } catch {
    throw new ApiError('network', SERVER_UNAVAILABLE)
  }

  if (response.ok) {
    return (await response.json()) as T
  }

  if (response.status === 400) {
    const problem = await readJson(response)
    throw new ApiError('validation', problem?.title ?? 'The request is invalid.', normalizeFieldErrors(problem?.errors))
  }

  if (UNAVAILABLE_STATUSES.has(response.status)) {
    throw new ApiError('network', SERVER_UNAVAILABLE)
  }

  throw new ApiError('server', SERVER_ERROR)
}

interface ValidationProblem {
  title?: string
  errors?: Record<string, string[]>
}

async function readJson(response: Response): Promise<ValidationProblem | null> {
  try {
    return (await response.json()) as ValidationProblem
  } catch {
    return null
  }
}

// The server keys rule errors by field ("amount") but JSON-format errors by JSON path ("$.amount").
// Both are mapped to the plain field name, so the form looks errors up one way.
function normalizeFieldErrors(errors: Record<string, string[]> = {}): Record<string, string[]> {
  const result: Record<string, string[]> = {}
  for (const [key, messages] of Object.entries(errors)) {
    const field = key.startsWith('$.') ? key.slice(2) : key
    result[field] = [...(result[field] ?? []), ...messages]
  }
  return result
}
