import { apiPost } from '../../shared/api/apiClient'
import type { IngestResponse, Transaction } from '../../shared/api/types'

/** What the form sends: amount may be missing, so the server's "required" validation can be seen. */
export type TransactionDraft = Omit<Transaction, 'amount'> & { amount: number | null }

/** Sends a transaction to the ingestion API. Rejects with an ApiError (validation / server / network). */
export function postTransaction(transaction: TransactionDraft): Promise<IngestResponse> {
  return apiPost<IngestResponse>('/api/transactions', transaction)
}
