import { useId } from 'react'
import { Select } from 'antd'

// Reuse the draft's accessible Ant Design select presentation, with real filter values.
export function UserFilterSelect({ label, value, options, onChange }: {
  label: string
  value: string
  options: { value: string; label: string }[]
  onChange: (value: string) => void
}) {
  const id = useId()
  return <Select id={id} aria-label={label} value={value} options={options} onChange={onChange}
    showSearch={false} virtual={false}
    classNames={{ root: 'users-select', popup: { root: 'users-select-popup' } }}
    suffixIcon={<svg width="16" height="16" viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.6" aria-hidden="true"><path d="m5.5 7.5 4.5 4.5 4.5-4.5" /></svg>}
    menuItemSelectedIcon={null}
    labelRender={selected => <span className="users-control-value">{selected.label}</span>}
    optionRender={option => <span className="users-control-choice"><span>{option.data.label}</span>
      {value === option.data.value && <svg width="16" height="16" viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.8" aria-hidden="true"><path d="m4.5 10 3.5 3.5 7.5-8" /></svg>}
    </span>} />
}
