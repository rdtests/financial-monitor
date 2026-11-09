import type { Transaction } from '../../shared/api/types'
import { StatusBadge } from './StatusBadge'

export function TransactionRow({ transaction }: { transaction: Transaction }) {
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
}

function formatAmount(amount: number, currency: string) {
  return new Intl.NumberFormat(undefined, { style: 'currency', currency }).format(amount)
}
