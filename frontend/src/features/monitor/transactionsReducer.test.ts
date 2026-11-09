import { describe, expect, it } from 'vitest'
import type { Transaction, TransactionStatus } from '../../shared/api/types'
import {
  initialTransactionsState,
  MAX_TRANSACTIONS,
  selectTransactions,
  transactionsReducer,
  type TransactionsState,
} from './transactionsReducer'

const BASE_TIME = Date.parse('2024-01-15T10:00:00Z')

function transaction(id: string, status: TransactionStatus = 'Pending', secondsAfterBase = 0): Transaction {
  return {
    transactionId: id,
    amount: 100,
    currency: 'USD',
    status,
    timestamp: new Date(BASE_TIME + secondsAfterBase * 1000).toISOString(),
  }
}

function load(...transactions: Transaction[]): TransactionsState {
  return transactionsReducer(initialTransactionsState, { type: 'snapshotLoaded', transactions })
}

describe('transactionsReducer', () => {
  it('fills the state from a snapshot', () => {
    const state = load(transaction('a'), transaction('b'))

    expect(selectTransactions(state).map((t) => t.transactionId).sort()).toEqual(['a', 'b'])
  })

  it('adds new transactions from a batch', () => {
    const state = transactionsReducer(load(transaction('a')), {
      type: 'batchReceived',
      transactions: [transaction('b'), transaction('c')],
    })

    expect(selectTransactions(state)).toHaveLength(3)
  })

  it('replaces an existing transaction with a newer update', () => {
    const updated = transaction('a', 'Completed', 5)

    const state = transactionsReducer(load(transaction('a', 'Pending', 0)), {
      type: 'batchReceived',
      transactions: [updated],
    })

    expect(selectTransactions(state)).toEqual([updated])
  })

  it('accepts an update with the same timestamp, like the server', () => {
    const updated = transaction('a', 'Completed', 0)

    const state = transactionsReducer(load(transaction('a', 'Pending', 0)), {
      type: 'batchReceived',
      transactions: [updated],
    })

    expect(selectTransactions(state)).toEqual([updated])
  })

  it('ignores an update older than the stored one', () => {
    const current = transaction('a', 'Completed', 5)

    const state = transactionsReducer(load(current), {
      type: 'batchReceived',
      transactions: [transaction('a', 'Pending', 0)],
    })

    expect(selectTransactions(state)).toEqual([current])
  })

  it('does not let an older snapshot overwrite a newer live event', () => {
    // The live event can arrive before the snapshot request completes.
    const live = transaction('a', 'Completed', 5)
    const afterEvent = transactionsReducer(initialTransactionsState, { type: 'batchReceived', transactions: [live] })

    const state = transactionsReducer(afterEvent, {
      type: 'snapshotLoaded',
      transactions: [transaction('a', 'Pending', 0)],
    })

    expect(selectTransactions(state)).toEqual([live])
  })

  it('compares timestamps as instants, not as strings', () => {
    // 12:00+02:00 is 10:00Z: older than 11:00Z even though the string sorts higher.
    const current = { ...transaction('a', 'Completed'), timestamp: '2024-01-15T11:00:00Z' }
    const older = { ...transaction('a', 'Pending'), timestamp: '2024-01-15T12:00:00+02:00' }

    const state = transactionsReducer(load(current), { type: 'batchReceived', transactions: [older] })

    expect(selectTransactions(state)).toEqual([current])
  })

  it(`keeps at most ${MAX_TRANSACTIONS} transactions, dropping the oldest`, () => {
    const many = Array.from({ length: MAX_TRANSACTIONS + 10 }, (_, i) => transaction(`t${i}`, 'Pending', i))

    const state = load(...many)

    const ids = selectTransactions(state).map((t) => t.transactionId)
    expect(ids).toHaveLength(MAX_TRANSACTIONS)
    expect(ids).not.toContain('t0')
    expect(ids).toContain(`t${MAX_TRANSACTIONS + 9}`)
  })

  it('returns transactions newest first', () => {
    const state = load(transaction('old', 'Pending', 0), transaction('new', 'Pending', 10), transaction('mid', 'Pending', 5))

    expect(selectTransactions(state).map((t) => t.transactionId)).toEqual(['new', 'mid', 'old'])
  })
})
