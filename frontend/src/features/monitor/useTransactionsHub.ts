import { HubConnectionBuilder } from '@microsoft/signalr'
import { useEffect, useState } from 'react'
import type { Transaction } from '../../shared/api/types'

export type ConnectionState = 'connecting' | 'connected' | 'reconnecting'

interface HubHandlers {
  /** Called for every "TransactionUpdated" event pushed by the server. */
  onTransaction: (transaction: Transaction) => void
  /**
   * Called every time the connection comes up: on first connect and after each reconnect.
   * SignalR does not replay events missed while disconnected, so this is where the snapshot is (re)loaded.
   */
  onConnected: () => void
}

const MAX_RETRY_DELAY_MS = 10_000

// 1s, 2s, 4s, 8s, then every 10s, forever. SignalR's default gives up after 4 attempts, which would leave
// a support agent's long-open dashboard silently disconnected until they notice and reload.
function retryDelay(attempt: number): number {
  return Math.min(1000 * 2 ** attempt, MAX_RETRY_DELAY_MS)
}

/**
 * Keeps a SignalR connection to the backend hub open while the component is mounted and reports its state.
 * Handlers must be stable (e.g. wrapped in useCallback), otherwise the connection is rebuilt on every render.
 */
export function useTransactionsHub({ onTransaction, onConnected }: HubHandlers): ConnectionState {
  const [state, setState] = useState<ConnectionState>('connecting')

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl('/hubs/transactions')
      // Never returns null, so automatic reconnect never gives up.
      .withAutomaticReconnect({ nextRetryDelayInMilliseconds: (context) => retryDelay(context.previousRetryCount) })
      .build()

    let isDisposed = false
    let retryTimer: ReturnType<typeof setTimeout> | undefined

    connection.on('TransactionUpdated', onTransaction)
    connection.onreconnecting(() => setState('reconnecting'))
    connection.onreconnected(() => {
      setState('connected')
      onConnected()
    })

    // Automatic reconnect only covers a connection that was up. The first start (e.g. the backend is down
    // when the page opens) needs its own retry loop.
    async function start(attempt: number) {
      try {
        await connection.start()
        if (isDisposed) return
        setState('connected')
        onConnected()
      } catch {
        // Stopping during start (e.g. React StrictMode's mount → unmount → mount in dev) is expected.
        if (isDisposed) return
        setState('reconnecting')
        retryTimer = setTimeout(() => void start(attempt + 1), retryDelay(attempt))
      }
    }

    void start(0)

    return () => {
      isDisposed = true
      clearTimeout(retryTimer)
      void connection.stop()
    }
  }, [onTransaction, onConnected])

  return state
}
