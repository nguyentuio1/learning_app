import axios from 'axios'

interface ProblemDetails {
  detail?: string
  errors?: Record<string, string[]>
}

export interface ProfileError {
  message: string
  fields: Record<string, string[]>
  accessDenied: boolean
}

export function profileError(error: unknown, operation: 'load' | 'profile' | 'password' | 'avatar'): ProfileError {
  const result: ProfileError = { message: 'Something went wrong. Please try again.', fields: {}, accessDenied: false }
  if (!axios.isAxiosError<ProblemDetails>(error)) {
    if (error instanceof Error) result.message = error.message
    return result
  }
  if (!error.response) {
    result.message = error.code === 'ECONNABORTED'
      ? 'The request timed out. Please try again.'
      : 'Unable to reach the server. Check your connection and try again.'
    return result
  }
  const { status, data } = error.response
  // A wrong current password is NOT an expired session (see auth-api.md).
  if (operation === 'password' && status === 401 && data?.detail === 'Current password is incorrect.') {
    return { ...result, message: data.detail, fields: { currentPassword: [data.detail] } }
  }
  if (status === 401 || status === 403) {
    return {
      ...result,
      accessDenied: true,
      message: status === 401
        ? 'Sign in to view your profile. Your session may be missing or expired.'
        : 'Your account is inactive or you do not have permission to access this profile.',
    }
  }
  if (operation === 'profile' && status === 409) {
    return { ...result, message: 'Phone number is already registered.', fields: { phoneNumber: ['Phone number is already registered.'] } }
  }
  if (status === 400) {
    const fields = Object.fromEntries(Object.entries(data?.errors || {}).filter(
      ([, messages]) => Array.isArray(messages) && messages.every(message => typeof message === 'string'),
    ))
    return { ...result, message: data?.detail || 'Please check the highlighted fields.', fields }
  }
  result.message = status >= 500
    ? 'The service is temporarily unavailable. Please try again later.'
    : 'Your request could not be completed. Please try again.'
  return result
}
