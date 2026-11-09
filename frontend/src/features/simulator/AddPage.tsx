import { useState, type FormEvent } from 'react'
import type { TransactionStatus } from '../../shared/api/types'
import { postTransaction } from './transactionsApi'

const CURRENCIES = ['USD', 'EUR', 'GBP', 'ILS']
const STATUSES: TransactionStatus[] = ['Pending', 'Completed', 'Failed']

/** Simulates the external system: builds a transaction from the form and POSTs it to the backend. */
export function AddPage() {
  const [amount, setAmount] = useState('1500.50')
  const [currency, setCurrency] = useState(CURRENCIES[0])
  const [status, setStatus] = useState<TransactionStatus>('Pending')
  const [isSending, setIsSending] = useState(false)
  const [message, setMessage] = useState<string | null>(null)

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setIsSending(true)
    setMessage(null)

    try {
      const result = await postTransaction({
        transactionId: crypto.randomUUID(),
        amount: Number(amount),
        currency,
        status,
        timestamp: new Date().toISOString(),
      })
      setMessage(`${result.outcome}: ${result.transactionId}`)
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'Request failed')
    } finally {
      setIsSending(false)
    }
  }

  return (
    <section>
      <h1>Transaction Simulator</h1>

      <form className="form" onSubmit={handleSubmit}>
        <label>
          Amount
          <input
            type="number"
            min="0.01"
            step="0.01"
            required
            value={amount}
            onChange={(e) => setAmount(e.target.value)}
          />
        </label>

        <label>
          Currency
          <select value={currency} onChange={(e) => setCurrency(e.target.value)}>
            {CURRENCIES.map((c) => (
              <option key={c}>{c}</option>
            ))}
          </select>
        </label>

        <label>
          Status
          <select value={status} onChange={(e) => setStatus(e.target.value as TransactionStatus)}>
            {STATUSES.map((s) => (
              <option key={s}>{s}</option>
            ))}
          </select>
        </label>

        <button type="submit" disabled={isSending}>
          {isSending ? 'Sending…' : 'Send transaction'}
        </button>
      </form>

      {message && <p role="status">{message}</p>}
    </section>
  )
}
