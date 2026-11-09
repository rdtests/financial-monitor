import { describe, expect, it } from 'vitest'
import type { Transaction, TransactionStatus } from '../../shared/api/types'
import { filterTransactions } from './filterTransactions'

function transaction(id: string, status: TransactionStatus): Transaction {
  return { transactionId: id, amount: 10, currency: 'USD', status, timestamp: '2024-01-15T10:00:00Z' }
}

const list = [
  transaction('p1', 'Pending'),
  transaction('c1', 'Completed'),
  transaction('f1', 'Failed'),
  transaction('p2', 'Pending'),
  transaction('f2', 'Failed'),
]

const ids = (transactions: Transaction[]) => transactions.map((t) => t.transactionId)

describe('filterTransactions', () => {
  it('"All" returns the whole list', () => {
    expect(filterTransactions(list, 'All')).toEqual(list)
  })

  it('a specific status returns only transactions in that status', () => {
    expect(ids(filterTransactions(list, 'Pending'))).toEqual(['p1', 'p2'])
    expect(ids(filterTransactions(list, 'Completed'))).toEqual(['c1'])
  })

  it('"Errors only" (Failed) returns only failed transactions', () => {
    expect(ids(filterTransactions(list, 'Failed'))).toEqual(['f1', 'f2'])
  })

  it('keeps the order of the input list', () => {
    const reversed = [...list].reverse()

    expect(ids(filterTransactions(reversed, 'Pending'))).toEqual(['p2', 'p1'])
  })
})
