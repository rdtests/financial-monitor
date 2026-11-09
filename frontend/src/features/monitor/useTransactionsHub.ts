import { HubConnectionBuilder } from '@microsoft/signalr'
import { useEffect } from 'react'
import type { Transaction } from '../../shared/api/types'

interface HubHandlers {
  /** Called for every "TransactionUpdated" event pushed by the server. */
  onTransaction: (transaction: Transaction) => void
  /** Called once the connection is up: the right moment to load the snapshot. */
  onConnected: () => void
}

/**
 * Keeps a SignalR connection to the backend hub open while the component is mounted.
 * Handlers must be stable (e.g. wrapped in useCallback), otherwise the connection is rebuilt on every render.
 */
export function useTransactionsHub({ onTransaction, onConnected }: HubHandlers) {
  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl('/hubs/transactions')
      .withAutomaticReconnect()
      .build()

    connection.on('TransactionUpdated', onTransaction)

    let isDisposed = false
    connection
      .start()
      .then(() => {
        if (!isDisposed) onConnected()
      })
      .catch((error: unknown) => {
        // Stopping during start (e.g. React StrictMode's mount → unmount → mount in dev) is expected.
        if (!isDisposed) console.error('SignalR connection failed', error)
      })

    return () => {
      isDisposed = true
      void connection.stop()
    }
  }, [onTransaction, onConnected])
}
