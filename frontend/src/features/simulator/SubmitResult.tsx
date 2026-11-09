import type { ApiError } from '../../shared/api/ApiError'
import type { IngestResponse } from '../../shared/api/types'

export type SubmitOutcome =
  | { kind: 'sent'; response: IngestResponse }
  | { kind: 'failed'; error: ApiError; extraMessages: string[] }
  | { kind: 'burst'; total: number; failed: number }

/** One-line feedback for the last action on the simulator page. */
export function SubmitResult({ outcome }: { outcome: SubmitOutcome }) {
  const { tone, text, details } = describe(outcome)

  return (
    <div className={`submit-result submit-result--${tone}`} role="status">
      <p>{text}</p>
      {details.length > 0 && (
        <ul>
          {details.map((message) => (
            <li key={message}>{message}</li>
          ))}
        </ul>
      )}
    </div>
  )
}

type Tone = 'success' | 'info' | 'warning' | 'error'

function describe(outcome: SubmitOutcome): { tone: Tone; text: string; details: string[] } {
  switch (outcome.kind) {
    case 'sent': {
      const { outcome: result, transactionId } = outcome.response
      if (result === 'IgnoredStale') {
        // Not an error: the external system sent an out-of-order update and the server kept the newer data.
        return { tone: 'info', text: 'Ignored: a newer update for this transaction already exists.', details: [] }
      }
      return { tone: 'success', text: `${result}: ${transactionId}`, details: [] }
    }

    case 'failed':
      if (outcome.error.kind === 'validation') {
        return { tone: 'error', text: 'The server rejected the transaction. Fix the highlighted fields.', details: outcome.extraMessages }
      }
      return { tone: 'error', text: outcome.error.message, details: [] }

    case 'burst':
      if (outcome.failed === 0) {
        return { tone: 'success', text: `Sent ${outcome.total} transactions.`, details: [] }
      }
      return {
        tone: 'warning',
        text: `${outcome.total - outcome.failed} sent, ${outcome.failed} failed.`,
        details: [],
      }
  }
}
