import { useState, type SubmitEvent } from 'react'
import { ApiError } from '../../shared/api/ApiError'
import type { Transaction, TransactionStatus } from '../../shared/api/types'
import { CURRENCIES, generateTransaction, lifecycleUpdate, staleUpdate, STATUSES } from './generateTransaction'
import { SubmitResult, type SubmitOutcome } from './SubmitResult'
import { postTransaction, type TransactionDraft } from './transactionsApi'

const BURST_SIZE = 100
const FORM_FIELDS = ['amount', 'currency', 'status']

interface FormValues {
  amount: string
  currency: string
  status: TransactionStatus
}

/** Simulates the external system: sends new transactions, lifecycle updates and bursts to the backend. */
export function AddPage() {
  const [form, setForm] = useState<FormValues>({ amount: '1500.50', currency: 'USD', status: 'Pending' })
  const [fieldErrors, setFieldErrors] = useState<Readonly<Record<string, string[]>>>({})
  const [outcome, setOutcome] = useState<SubmitOutcome | null>(null)
  const [isBusy, setIsBusy] = useState(false)
  // The last transaction the server accepted, so follow-up updates can be sent for the same id.
  const [lastSent, setLastSent] = useState<Transaction | null>(null)

  // fromForm: errors from a form submission are shown next to the inputs; for the other buttons
  // (random, lifecycle updates) the form is not the source, so all messages go to the result line.
  async function send(draft: TransactionDraft, { fromForm = false } = {}) {
    setIsBusy(true)
    setOutcome(null)
    setFieldErrors({})

    try {
      const response = await postTransaction(draft)
      setOutcome({ kind: 'sent', response })
      if (response.outcome !== 'IgnoredStale' && draft.amount !== null) {
        setLastSent({ ...draft, amount: draft.amount })
      }
    } catch (error) {
      const apiError = error instanceof ApiError ? error : new ApiError('server', 'Unexpected error.')
      // Field errors are shown next to the inputs; anything else (e.g. timestamp) is listed in the result.
      const extraMessages = Object.entries(apiError.fieldErrors)
        .filter(([field]) => !fromForm || !FORM_FIELDS.includes(field))
        .flatMap(([, messages]) => messages)
      setOutcome({ kind: 'failed', error: apiError, extraMessages })
      if (fromForm) setFieldErrors(apiError.fieldErrors)
    } finally {
      setIsBusy(false)
    }
  }

  function handleSubmit(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault()
    const amount = form.amount.trim() === '' ? null : Number(form.amount)
    void send(
      {
        transactionId: crypto.randomUUID(),
        amount,
        currency: form.currency.trim(),
        status: form.status,
        timestamp: new Date().toISOString(),
      },
      { fromForm: true },
    )
  }

  async function sendBurst() {
    setIsBusy(true)
    setOutcome(null)
    setFieldErrors({})

    // allSettled, not all: one failed request must not hide how many others succeeded.
    const results = await Promise.allSettled(
      Array.from({ length: BURST_SIZE }, () => postTransaction(generateTransaction())),
    )
    const failed = results.filter((r) => r.status === 'rejected').length

    setOutcome({ kind: 'burst', total: BURST_SIZE, failed })
    setIsBusy(false)
  }

  return (
    <section>
      <h1>Transaction Simulator</h1>

      {/* noValidate: the server is the source of truth, so its validation errors are what the user sees. */}
      <form className="form" onSubmit={handleSubmit} noValidate>
        <label>
          Amount
          <input
            type="number"
            step="0.01"
            value={form.amount}
            aria-invalid={Boolean(fieldErrors.amount)}
            onChange={(e) => setForm({ ...form, amount: e.target.value })}
          />
          <FieldError messages={fieldErrors.amount} />
        </label>

        <label>
          Currency
          <select value={form.currency} onChange={(e) => setForm({ ...form, currency: e.target.value })}>
            {CURRENCIES.map((c) => (
              <option key={c}>{c}</option>
            ))}
          </select>
          <FieldError messages={fieldErrors.currency} />
        </label>

        <label>
          Status
          <select
            value={form.status}
            onChange={(e) => setForm({ ...form, status: e.target.value as TransactionStatus })}
          >
            {STATUSES.map((s) => (
              <option key={s}>{s}</option>
            ))}
          </select>
          <FieldError messages={fieldErrors.status} />
        </label>

        <div className="button-row">
          <button type="submit" disabled={isBusy}>
            Send
          </button>
          <button type="button" disabled={isBusy} onClick={() => void send(generateTransaction())}>
            Send random
          </button>
          <button type="button" disabled={isBusy} onClick={() => void sendBurst()}>
            Send burst of {BURST_SIZE}
          </button>
        </div>
      </form>

      {outcome && <SubmitResult outcome={outcome} />}

      {/* Follow-up updates for the same id: the lifecycle (Pending → Completed / Failed),
          and an out-of-order update with an older timestamp that the server must ignore. */}
      {lastSent && (
        <div className="last-sent">
          <span>
            Last sent: <span className="mono">{lastSent.transactionId}</span> ({lastSent.status})
          </span>
          <button type="button" disabled={isBusy} onClick={() => void send(lifecycleUpdate(lastSent, 'Completed'))}>
            Complete
          </button>
          <button type="button" disabled={isBusy} onClick={() => void send(lifecycleUpdate(lastSent, 'Failed'))}>
            Fail
          </button>
          <button
            type="button"
            disabled={isBusy}
            onClick={() => void send(staleUpdate(lastSent))}
            title="Sends Pending with a timestamp one minute older; the server should ignore it"
          >
            Stale update
          </button>
        </div>
      )}
    </section>
  )
}

function FieldError({ messages }: { messages?: string[] }) {
  if (!messages?.length) return null
  return <span className="field-error">{messages.join(' ')}</span>
}
