import axios from 'axios'

export function orderFeedback(error: unknown, fallback: string) {
  if (!axios.isAxiosError(error)) return fallback
  const status = error.response?.status
  if (!error.response) return 'Unable to reach the server. Check your connection and try again.'
  if (status === 401) return 'Your session has expired. Please sign in again.'
  if (status === 403) return 'This action requires an active customer account.'
  if (status === 404) return 'The requested order or selected item is no longer available.'
  if (status === 409) return 'The order or package has changed. Refresh the latest details before trying again.'
  const fields = error.response?.data?.errors as Record<string, string[]> | undefined
  if (status === 400 && fields) return Object.values(fields).flat().join(' ') || 'Check the entered details.'
  return status === 400 ? 'Check the entered details and selected items.' : fallback
}
