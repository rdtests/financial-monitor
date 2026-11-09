import { apiPost } from '../../shared/api/apiClient'
import type { IngestResponse, Transaction } from '../../shared/api/types'

/** Sends a transaction to the ingestion API. Rejects with an ApiError (validation / server / network). */
export function postTransaction(transaction: Transaction): Promise<IngestResponse> {
  return apiPost<IngestResponse>('/api/transactions', transaction)
}
