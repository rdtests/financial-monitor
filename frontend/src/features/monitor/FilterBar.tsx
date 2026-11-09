import type { StatusFilter } from './filterTransactions'

const OPTIONS: { value: StatusFilter; label: string }[] = [
  { value: 'All', label: 'All' },
  { value: 'Pending', label: 'Pending' },
  { value: 'Completed', label: 'Completed' },
  { value: 'Failed', label: 'Errors only' },
]

interface Props {
  value: StatusFilter
  onChange: (filter: StatusFilter) => void
}

export function FilterBar({ value, onChange }: Props) {
  return (
    <div className="filter-bar" role="group" aria-label="Filter by status">
      {OPTIONS.map((option) => (
        <button
          key={option.value}
          type="button"
          aria-pressed={value === option.value}
          onClick={() => onChange(option.value)}
        >
          {option.label}
        </button>
      ))}
    </div>
  )
}
