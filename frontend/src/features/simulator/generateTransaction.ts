import type { Transaction, TransactionStatus } from '../../shared/api/types'

export const CURRENCIES = ['USD', 'EUR', 'GBP', 'ILS', 'JPY']
export const STATUSES: TransactionStatus[] = ['Pending', 'Completed', 'Failed']

/** A random new transaction, as the external system might send it. Most start as Pending. */
export function generateTransaction(now = new Date()): Transaction {
  return {
    transactionId: crypto.randomUUID(),
    amount: randomAmount(),
    currency: pickRandom(CURRENCIES),
    status: randomStatus(),
    timestamp: now.toISOString(),
  }
}

/** The next step in a transaction's lifecycle: same id, new status, current time. */
export function lifecycleUpdate(transaction: Transaction, status: TransactionStatus, now = new Date()): Transaction {
  return { ...transaction, status, timestamp: now.toISOString() }
}

/**
 * An out-of-order update: same id, but a timestamp one minute older than the last one sent.
 * The server must answer IgnoredStale and keep the newer data.
 */
export function staleUpdate(transaction: Transaction): Transaction {
  const oneMinuteEarlier = new Date(Date.parse(transaction.timestamp) - 60_000)
  return { ...transaction, status: 'Pending', timestamp: oneMinuteEarlier.toISOString() }
}

// 0.01 .. 5000.00, always two decimals.
function randomAmount(): number {
  return Math.max(1, Math.round(Math.random() * 500_000)) / 100
}

// Roughly 60% Pending, 30% Completed, 10% Failed.
function randomStatus(): TransactionStatus {
  const roll = Math.random()
  if (roll < 0.6) return 'Pending'
  if (roll < 0.9) return 'Completed'
  return 'Failed'
}

function pickRandom<T>(items: T[]): T {
  return items[Math.floor(Math.random() * items.length)]
}
