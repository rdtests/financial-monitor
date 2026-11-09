import type { TransactionStatus } from '../../shared/api/types'

// An icon as well as a color, so the status does not rely on color alone.
const ICONS: Record<TransactionStatus, string> = {
  Pending: '●',
  Completed: '✓',
  Failed: '✕',
}

export function StatusBadge({ status }: { status: TransactionStatus }) {
  return (
    <span className={`status-badge status-badge--${status.toLowerCase()}`}>
      <span aria-hidden="true">{ICONS[status]}</span> {status}
    </span>
  )
}
