import { useCallback, useMemo, useReducer } from 'react'
import { apiGet } from '../../shared/api/apiClient'
import type { Transaction } from '../../shared/api/types'
import { TransactionList } from './TransactionList'
import { initialTransactionsState, selectTransactions, transactionsReducer } from './transactionsReducer'
import { useTransactionsHub } from './useTransactionsHub'

/** Live dashboard: snapshot from GET on connect, then real-time updates from SignalR. */
export function MonitorPage() {
  const [state, dispatch] = useReducer(transactionsReducer, initialTransactionsState)

  const onTransaction = useCallback(
    (transaction: Transaction) => dispatch({ type: 'batchReceived', transactions: [transaction] }),
    [],
  )

  // Connect first, then load the snapshot, so no event is missed in between.
  // The reducer ignores older data, so an event that arrived before the snapshot is not overwritten.
  const onConnected = useCallback(() => {
    apiGet<Transaction[]>('/api/transactions')
      .then((transactions) => dispatch({ type: 'snapshotLoaded', transactions }))
      .catch((error: unknown) => console.error('Loading transactions failed', error))
  }, [])

  useTransactionsHub({ onTransaction, onConnected })

  const transactions = useMemo(() => selectTransactions(state), [state])

  return (
    <section>
      <h1>Live Dashboard</h1>
      <TransactionList transactions={transactions} />
    </section>
  )
}
