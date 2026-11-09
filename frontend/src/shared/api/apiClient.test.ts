import { afterEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from './ApiError'
import { apiGet, apiPost } from './apiClient'

function stubFetchResponse(status: number, body: unknown, contentType = 'application/json') {
  const response = new Response(JSON.stringify(body), { status, headers: { 'Content-Type': contentType } })
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(response))
}

async function captureError(promise: Promise<unknown>): Promise<ApiError> {
  try {
    await promise
  } catch (error) {
    expect(error).toBeInstanceOf(ApiError)
    return error as ApiError
  }
  throw new Error('Expected the request to fail')
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('apiClient', () => {
  it('returns the parsed JSON body on success', async () => {
    stubFetchResponse(201, { transactionId: 'a', outcome: 'Created' })

    await expect(apiPost('/api/transactions', {})).resolves.toEqual({ transactionId: 'a', outcome: 'Created' })
  })

  it('turns 400 into a validation error with errors per field', async () => {
    stubFetchResponse(
      400,
      {
        title: 'One or more validation errors occurred.',
        status: 400,
        errors: {
          amount: ['Amount must be greater than 0.'],
          // Errors from JSON deserialization are keyed by JSON path.
          '$.status': ['The JSON value could not be converted.'],
        },
      },
      'application/problem+json',
    )

    const error = await captureError(apiPost('/api/transactions', {}))

    expect(error.kind).toBe('validation')
    expect(error.fieldErrors).toEqual({
      amount: ['Amount must be greater than 0.'],
      status: ['The JSON value could not be converted.'],
    })
  })

  it('turns 500 into a server error without technical details', async () => {
    stubFetchResponse(500, { title: 'An error occurred while processing your request.', status: 500, traceId: 'x' })

    const error = await captureError(apiGet('/api/transactions'))

    expect(error.kind).toBe('server')
    expect(error.message).toBe('Server error, please try again.')
  })

  it('turns a failed fetch into a network error', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('Failed to fetch')))

    const error = await captureError(apiGet('/api/transactions'))

    expect(error.kind).toBe('network')
    expect(error.message).toBe('Server is unavailable.')
  })

  it.each([502, 503, 504])('treats %i from the dev proxy as the server being unavailable', async (status) => {
    stubFetchResponse(status, {})

    const error = await captureError(apiGet('/api/transactions'))

    expect(error.kind).toBe('network')
  })
})
