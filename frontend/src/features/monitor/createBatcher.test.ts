import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createBatcher } from './createBatcher'

const INTERVAL_MS = 100

beforeEach(() => {
  vi.useFakeTimers()
})

afterEach(() => {
  vi.useRealTimers()
})

describe('createBatcher', () => {
  it('flushes 100 quickly added items once, with all of them', () => {
    const onFlush = vi.fn()
    const batcher = createBatcher<number>(onFlush, INTERVAL_MS)

    for (let i = 0; i < 100; i++) {
      batcher.add(i)
      vi.advanceTimersByTime(0.5) // 100 items within 50ms
    }
    vi.advanceTimersByTime(INTERVAL_MS)

    expect(onFlush).toHaveBeenCalledTimes(1)
    expect(onFlush).toHaveBeenCalledWith(Array.from({ length: 100 }, (_, i) => i))
  })

  it('does not flush before the interval has passed', () => {
    const onFlush = vi.fn()
    const batcher = createBatcher<number>(onFlush, INTERVAL_MS)

    batcher.add(1)
    vi.advanceTimersByTime(INTERVAL_MS - 1)

    expect(onFlush).not.toHaveBeenCalled()
  })

  it('collects items that arrive after a flush into the next batch', () => {
    const onFlush = vi.fn()
    const batcher = createBatcher<number>(onFlush, INTERVAL_MS)

    batcher.add(1)
    batcher.add(2)
    vi.advanceTimersByTime(INTERVAL_MS)
    batcher.add(3)
    vi.advanceTimersByTime(INTERVAL_MS)

    expect(onFlush).toHaveBeenNthCalledWith(1, [1, 2])
    expect(onFlush).toHaveBeenNthCalledWith(2, [3])
  })

  it('does not flush when no items were added', () => {
    const onFlush = vi.fn()
    createBatcher<number>(onFlush, INTERVAL_MS)

    vi.advanceTimersByTime(INTERVAL_MS * 10)

    expect(onFlush).not.toHaveBeenCalled()
  })

  it('dispose() cancels a pending flush, so no state update happens after unmount', () => {
    const onFlush = vi.fn()
    const batcher = createBatcher<number>(onFlush, INTERVAL_MS)

    batcher.add(1)
    batcher.dispose()
    vi.advanceTimersByTime(INTERVAL_MS)

    expect(onFlush).not.toHaveBeenCalled()
  })
})
