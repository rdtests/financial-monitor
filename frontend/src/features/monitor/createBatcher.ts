export interface Batcher<T> {
  add(item: T): void
  /** Cancels a pending flush and drops buffered items (call on unmount). */
  dispose(): void
}

/**
 * Collects items and hands them to onFlush in one batch at most every intervalMs.
 * Used so that a burst of SignalR messages causes one React render instead of one per message.
 * Plain TypeScript with no React dependency, so the behavior can be tested with fake timers.
 *
 * The timer starts on the first item after a flush, so nothing runs while no messages arrive, and the
 * delay a user can notice is bounded by intervalMs (100ms by default: up to 10 renders per second).
 */
export function createBatcher<T>(onFlush: (items: T[]) => void, intervalMs = 100): Batcher<T> {
  let buffer: T[] = []
  let timer: ReturnType<typeof setTimeout> | undefined

  function flush() {
    const items = buffer
    buffer = []
    timer = undefined
    onFlush(items)
  }

  return {
    add(item) {
      buffer.push(item)
      timer ??= setTimeout(flush, intervalMs)
    },
    dispose() {
      clearTimeout(timer)
      timer = undefined
      buffer = []
    },
  }
}
