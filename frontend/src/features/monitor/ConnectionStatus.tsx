import type { ConnectionState } from './useTransactionsHub'

const LABELS: Record<ConnectionState, string> = {
  connecting: 'Connecting…',
  connected: 'Live',
  reconnecting: 'Reconnecting…',
}

/** No Reconnect button: the hub retries forever and reloads the snapshot when it is back. */
export function ConnectionStatus({ state }: { state: ConnectionState }) {
  return (
    <span className={`connection-status connection-status--${state}`} role="status">
      {LABELS[state]}
    </span>
  )
}
