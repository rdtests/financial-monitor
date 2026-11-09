// Hand-written mirror of the backend contracts (TransactionRequest / TransactionResponse / IngestResponse).
// Keep in sync with backend/src/FinancialMonitor.Api/Contracts.

export type TransactionStatus = 'Pending' | 'Completed' | 'Failed'

/** A transaction as sent by POST and returned by GET and the SignalR "TransactionUpdated" event. */
export interface Transaction {
  transactionId: string
  amount: number
  currency: string
  status: TransactionStatus
  /** ISO 8601 date-time. */
  timestamp: string
}

export type IngestOutcome = 'Created' | 'Updated' | 'IgnoredStale'

/** Body of the POST /api/transactions response. */
export interface IngestResponse {
  transactionId: string
  outcome: IngestOutcome
}
