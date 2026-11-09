import type { IngestResponse, Transaction } from '../../shared/api/types'

// Minimal for the walking skeleton; error handling by kind (validation / server / network) comes with apiClient.
export async function postTransaction(transaction: Transaction): Promise<IngestResponse> {
  const response = await fetch('/api/transactions', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(transaction),
  })

  if (!response.ok) {
    throw new Error(`Request failed with status ${response.status}`)
  }

  return response.json() as Promise<IngestResponse>
}
