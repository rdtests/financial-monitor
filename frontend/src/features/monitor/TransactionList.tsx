import type { Transaction } from '../../shared/api/types'
import { TransactionRow } from './TransactionRow'

interface Props {
  transactions: Transaction[]
  emptyMessage: string
}

export function TransactionList({ transactions, emptyMessage }: Props) {
  if (transactions.length === 0) {
    return <p className="empty">{emptyMessage}</p>
  }

  return (
    <table className="transactions">
      <thead>
        <tr>
          <th>Time</th>
          <th>Transaction ID</th>
          <th className="amount">Amount</th>
          <th>Status</th>
        </tr>
      </thead>
      <tbody>
        {transactions.map((t) => (
          <TransactionRow key={t.transactionId} transaction={t} />
        ))}
      </tbody>
    </table>
  )
}
