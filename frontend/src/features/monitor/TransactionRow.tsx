import { memo } from 'react'
import type { Transaction } from '../../shared/api/types'
import { StatusBadge } from './StatusBadge'

/**
 * Memoized: the reducer keeps the same object for a transaction that did not change, so when a batch
 * arrives only new and updated rows re-render, not all 500.
 */
export const TransactionRow = memo(function TransactionRow({ transaction }: { transaction: Transaction }) {
  return (
    <tr>
      <td>{new Date(transaction.timestamp).toLocaleTimeString()}</td>
      <td className="mono">{transaction.transactionId}</td>
      <td className="amount">{formatAmount(transaction.amount, transaction.currency)}</td>
      <td>
        <StatusBadge status={transaction.status} />
      </td>
    </tr>
  )
})

// Creating Intl.NumberFormat is relatively expensive, so one formatter per currency is reused.
const formatters = new Map<string, Intl.NumberFormat>()

function formatAmount(amount: number, currency: string) {
  let formatter = formatters.get(currency)
  if (!formatter) {
    formatter = new Intl.NumberFormat(undefined, { style: 'currency', currency })
    formatters.set(currency, formatter)
  }
  return formatter.format(amount)
}
