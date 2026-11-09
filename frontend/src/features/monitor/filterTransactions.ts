import type { Transaction, TransactionStatus } from '../../shared/api/types'

/** "Failed" is offered in the UI as "Errors only". */
export type StatusFilter = 'All' | TransactionStatus

/** Pure client-side filter; keeps the order of the input list. */
export function filterTransactions(transactions: Transaction[], filter: StatusFilter): Transaction[] {
  if (filter === 'All') return transactions
  return transactions.filter((t) => t.status === filter)
}
