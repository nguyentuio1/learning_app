import axios from 'axios'

export function operationError(error: unknown): string {
  if (!axios.isAxiosError(error)) return 'Unable to complete this request. Please try again.'
  switch (error.response?.status) {
    case 400: return 'Check the highlighted fields and try again.'
    case 401: return 'Your session has expired. Sign in again.'
    case 403: return 'You do not have permission to perform this action.'
    case 404: return 'This record or service is unavailable. Refresh and try again.'
    case 409: return 'The record changed or is in use. Refresh its latest details before trying again.'
    default: return 'Unable to reach the service. Please try again.'
  }
}

export function validationFields<K extends string>(error: unknown, allowed: readonly K[]) {
  if (!axios.isAxiosError(error) || error.response?.status !== 400) return []
  const errors: unknown = error.response.data?.errors
  if (!errors || typeof errors !== 'object') return []
  return Object.entries(errors).flatMap(([name, messages]) => {
    const field = allowed.find(key => key === name.charAt(0).toLowerCase() + name.slice(1))
    return field ? [{ name: field, errors: Array.isArray(messages) ? messages.filter((item): item is string => typeof item === 'string') : [] }] : []
  })
}

export function uncertainMutation(error: unknown) {
  return axios.isAxiosError(error) && (!error.response || error.response.status >= 500)
}
