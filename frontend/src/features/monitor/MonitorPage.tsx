import { useCallback, useMemo, useState } from 'react'
import type { Transaction } from '../../shared/api/types'
import { TransactionList } from './TransactionList'
import { useTransactionsHub } from './useTransactionsHub'

async function fetchTransactions(): Promise<Transaction[]> {
  const response = await fetch('/api/transactions')
  if (!response.ok) throw new Error(`Request failed with status ${response.status}`)
  return response.json() as Promise<Transaction[]>
}

/** Live dashboard: snapshot from GET on connect, then real-time updates from SignalR. */
export function MonitorPage() {
  // Keyed by transactionId, so an update replaces the existing row instead of adding a new one.
  // Walking-skeleton merge: last write wins. The "ignore older timestamp" rule arrives with the reducer.
  const [byId, setById] = useState(() => new Map<string, Transaction>())

  const merge = useCallback((incoming: Transaction[]) => {
    setById((previous) => {
      const next = new Map(previous)
      for (const transaction of incoming) next.set(transaction.transactionId, transaction)
      return next
    })
  }, [])

  const onTransaction = useCallback((transaction: Transaction) => merge([transaction]), [merge])

  // Connect first, then load the snapshot, so no event is missed in between.
  const onConnected = useCallback(() => {
    fetchTransactions()
      .then(merge)
      .catch((error: unknown) => console.error('Loading transactions failed', error))
  }, [merge])

  useTransactionsHub({ onTransaction, onConnected })

  const transactions = useMemo(
    () => [...byId.values()].sort((a, b) => b.timestamp.localeCompare(a.timestamp)),
    [byId],
  )

  return (
    <section>
      <h1>Live Dashboard</h1>
      <TransactionList transactions={transactions} />
    </section>
  )
}
