interface Props {
  message: string
  onRetry: () => void
}

/** Shown when loading the snapshot failed. Live events keep arriving meanwhile. */
export function ErrorBanner({ message, onRetry }: Props) {
  return (
    <div className="error-banner" role="alert">
      <span>Could not load transactions: {message} Live updates continue.</span>
      <button type="button" onClick={onRetry}>
        Retry
      </button>
    </div>
  )
}
