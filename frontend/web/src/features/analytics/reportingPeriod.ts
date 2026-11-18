import type { PeriodQuery } from './analyticsApi'

export const reportingTimeZone = 'Asia/Ho_Chi_Minh'
export const reportingPresets = [
  { days: 1, label: 'Today' },
  { days: 7, label: 'Last 7 days' },
  { days: 30, label: 'Last 30 days' },
] as const
export type PresetDays = typeof reportingPresets[number]['days']
const dayMilliseconds = 86_400_000
const reportingDay = new Intl.DateTimeFormat('en-CA', {
  timeZone: reportingTimeZone,
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
})

export function presetPeriod(days: PresetDays, now = new Date()): PeriodQuery {
  const parts = reportingDay.formatToParts(now)
  const year = parts.find(part => part.type === 'year')?.value
  const month = parts.find(part => part.type === 'month')?.value
  const day = parts.find(part => part.type === 'day')?.value
  const toDate = `${year}-${month}-${day}`
  const to = Date.parse(toDate + 'T00:00:00Z')
  return {
    fromDate: new Date(to - (days - 1) * dayMilliseconds).toISOString().slice(0, 10),
    toDate,
  }
}

function calendarTimestamp(value: string): number {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value) || value < '0001-01-01') return NaN
  const timestamp = Date.parse(value + 'T00:00:00Z')
  return Number.isFinite(timestamp) && new Date(timestamp).toISOString().slice(0, 10) === value ? timestamp : NaN
}

export function reportingStartError(from: string) {
  if (!Number.isFinite(calendarTimestamp(from))) return 'Enter a valid start date.'
  if (from < '0001-01-02') return 'The start date falls outside the supported UTC timestamp range.'
}

export function reportingPeriodError(from: string | undefined, to: string | undefined) {
  if (!from || !to) return 'Choose both reporting dates.'
  const startError = reportingStartError(from)
  if (startError) return startError
  const end = calendarTimestamp(to)
  if (!Number.isFinite(end)) return 'Enter a valid end date.'
  if (to >= '9999-12-31') return 'The end date must allow a representable next day.'
  if (from > to) return 'The start date must not be after the end date.'
  if (end - calendarTimestamp(from) >= 366 * dayMilliseconds) return 'Choose a period of no more than 366 inclusive days.'
}
