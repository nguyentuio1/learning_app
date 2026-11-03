export function orderStatusLabel(status: string) {
  return status.toLowerCase().split('_').map(word => word.charAt(0).toUpperCase() + word.slice(1)).join(' ')
}
export function orderTime(value: string | null | undefined) {
  if (!value) return 'Not available yet'
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? 'Not available' : date.toLocaleString()
}
// HTML date inputs represent calendar days in the customer's local timezone.
// Convert both ends to UTC; include the whole end date instead of midnight only.
export function orderDateBoundary(day: string | undefined, end = false) {
  return day ? new Date(`${day}T${end ? '23:59:59.999' : '00:00:00.000'}`).toISOString() : undefined
}
