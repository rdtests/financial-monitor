import type { Transaction } from '../../shared/api/types'

interface Props {
  transactions: Transaction[]
}

export function TransactionList({ transactions }: Props) {
  if (transactions.length === 0) {
    return <p>No transactions yet. Send one from the Simulator.</p>
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
          <tr key={t.transactionId}>
            <td>{new Date(t.timestamp).toLocaleTimeString()}</td>
            <td className="mono">{t.transactionId}</td>
            <td className="amount">{formatAmount(t.amount, t.currency)}</td>
            <td>{t.status}</td>
          </tr>
        ))}
      </tbody>
    </table>
  )
}

function formatAmount(amount: number, currency: string) {
  return new Intl.NumberFormat(undefined, { style: 'currency', currency }).format(amount)
}
