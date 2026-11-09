import type { Transaction } from '../../shared/api/types'

/** Upper bound on rows kept in the browser; the oldest are dropped beyond it. */
export const MAX_TRANSACTIONS = 500

export interface TransactionsState {
  /** Keyed by transactionId, so an update replaces its row instead of adding one. */
  byId: ReadonlyMap<string, Transaction>
}

export type TransactionsAction =
  | { type: 'snapshotLoaded'; transactions: Transaction[] }
  | { type: 'batchReceived'; transactions: Transaction[] }

export const initialTransactionsState: TransactionsState = { byId: new Map() }

/**
 * Both actions merge with the same rule the server uses: an update older than the stored one is ignored.
 * That makes merging idempotent and order-independent, so it does not matter whether a live event
 * arrives before or after the snapshot, or whether two broadcasts arrive in reverse order.
 */
export function transactionsReducer(state: TransactionsState, action: TransactionsAction): TransactionsState {
  switch (action.type) {
    case 'snapshotLoaded':
    case 'batchReceived':
      return { byId: merge(state.byId, action.transactions) }
  }
}

/** Transactions newest first. */
export function selectTransactions(state: TransactionsState): Transaction[] {
  return [...state.byId.values()].sort((a, b) => timeOf(b) - timeOf(a))
}

function merge(current: ReadonlyMap<string, Transaction>, incoming: Transaction[]): Map<string, Transaction> {
  const next = new Map(current)

  for (const transaction of incoming) {
    const stored = next.get(transaction.transactionId)
    // Equal timestamps are accepted, matching the server (e.g. Pending -> Completed in the same second).
    if (!stored || timeOf(transaction) >= timeOf(stored)) {
      next.set(transaction.transactionId, transaction)
    }
  }

  return next.size > MAX_TRANSACTIONS ? keepNewest(next) : next
}

function keepNewest(transactions: Map<string, Transaction>): Map<string, Transaction> {
  const newest = [...transactions.values()].sort((a, b) => timeOf(b) - timeOf(a)).slice(0, MAX_TRANSACTIONS)
  return new Map(newest.map((t) => [t.transactionId, t]))
}

// Timestamps are compared as instants: as strings, "12:00+02:00" would wrongly sort after "11:00Z".
function timeOf(transaction: Transaction): number {
  return Date.parse(transaction.timestamp)
}
