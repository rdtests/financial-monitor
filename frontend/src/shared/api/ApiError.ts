/**
 * - validation: the server rejected the input (400); fieldErrors holds the messages per field.
 * - server: the server failed (500 etc.); nothing the user can fix, so no technical details are kept.
 * - network: the server could not be reached.
 */
export type ApiErrorKind = 'validation' | 'server' | 'network'

/** The single error type the UI handles, so components never parse HTTP responses themselves. */
export class ApiError extends Error {
  readonly kind: ApiErrorKind
  /** Validation messages keyed by field name ("amount", "currency", ...). Empty for other kinds. */
  readonly fieldErrors: Readonly<Record<string, string[]>>

  constructor(kind: ApiErrorKind, message: string, fieldErrors: Record<string, string[]> = {}) {
    super(message)
    this.name = 'ApiError'
    this.kind = kind
    this.fieldErrors = fieldErrors
  }
}

/** apiClient only throws ApiError; anything else is a bug, reported to the user as a generic server error. */
export function toApiError(error: unknown): ApiError {
  return error instanceof ApiError ? error : new ApiError('server', 'Unexpected error.')
}
