import { useCallback, useMemo, useReducer, useState } from 'react'
import { apiGet } from '../../shared/api/apiClient'
import { toApiError, type ApiError } from '../../shared/api/ApiError'
import type { Transaction } from '../../shared/api/types'
import { ConnectionStatus } from './ConnectionStatus'
import { ErrorBanner } from './ErrorBanner'
import { FilterBar } from './FilterBar'
import { filterTransactions, type StatusFilter } from './filterTransactions'
import './monitor.css'
import { TransactionList } from './TransactionList'
import { initialTransactionsState, selectTransactions, transactionsReducer } from './transactionsReducer'
import { useTransactionsHub } from './useTransactionsHub'

/** Live dashboard: snapshot from GET on (re)connect, then real-time updates from SignalR. */
export function MonitorPage() {
  const [state, dispatch] = useReducer(transactionsReducer, initialTransactionsState)
  const [filter, setFilter] = useState<StatusFilter>('All')
  const [loadError, setLoadError] = useState<ApiError | null>(null)

  const onTransaction = useCallback(
    (transaction: Transaction) => dispatch({ type: 'batchReceived', transactions: [transaction] }),
    [],
  )

  // Runs on first connect, after every reconnect, and from the Retry button.
  // The reducer ignores older data, so reloading never overwrites newer live events.
  const loadSnapshot = useCallback(() => {
    apiGet<Transaction[]>('/api/transactions')
      .then((transactions) => {
        setLoadError(null)
        dispatch({ type: 'snapshotLoaded', transactions })
      })
      .catch((error: unknown) => setLoadError(toApiError(error)))
  }, [])

  const connectionState = useTransactionsHub({ onTransaction, onConnected: loadSnapshot })

  const allTransactions = useMemo(() => selectTransactions(state), [state])
  const visibleTransactions = useMemo(() => filterTransactions(allTransactions, filter), [allTransactions, filter])

  return (
    <section>
      <div className="monitor-header">
        <h1>Live Dashboard</h1>
        <ConnectionStatus state={connectionState} />
      </div>

      {loadError && <ErrorBanner message={loadError.message} onRetry={loadSnapshot} />}

      <FilterBar value={filter} onChange={setFilter} />

      <TransactionList
        transactions={visibleTransactions}
        emptyMessage={
          allTransactions.length === 0
            ? 'No transactions yet. Send one from the Simulator.'
            : 'No transactions match this filter.'
        }
      />
    </section>
  )
}
