import axios from 'axios'
import type { ManagedUser } from '../types/userManagement'
export interface UserManagementError { message: string; fields: Record<string, string[]>; status?: number }
export function userManagementError(error: unknown): UserManagementError {
  if (!axios.isAxiosError(error)) return { message: error instanceof Error ? error.message : 'Unable to complete this request.', fields: {} }
  const status = error.response?.status
  const problem = error.response?.data
  const result: UserManagementError = { status, message: 'Unable to complete this request.', fields: {} }
  if (!status) result.message = error.code === 'ECONNABORTED' ? 'The request timed out. Please try again.' : 'Unable to reach the server. Check your connection and try again.'
  else if (status === 401) result.message = 'Your session has expired. Please sign in again.'
  else if (status === 403) result.message = 'Administrator access is required, or your account is no longer active.'
  else if (status === 404) result.message = 'This user could not be found.'
  else if (status >= 500) result.message = 'The service is temporarily unavailable. Please try again.'
  else if (status === 400 || status === 409) {
    result.message = typeof problem?.detail === 'string' ? problem.detail : 'Please review the account details.'
    if (status === 400 && problem?.errors && typeof problem.errors === 'object') {
      const allowed = ['email', 'fullName', 'phoneNumber', 'password', 'role']
      result.fields = Object.fromEntries(Object.entries(problem.errors).filter(([field, messages]) =>
        allowed.includes(field) && Array.isArray(messages) && messages.every(value => typeof value === 'string'))) as Record<string, string[]>
    }
    if (status === 409) {
      if (result.message === 'Email is already registered.') result.fields.email = [result.message]
      if (result.message === 'Phone number is already registered.') result.fields.phoneNumber = [result.message]
    }
  }
  return result
}
export function filterUsers(users: ManagedUser[], search: string, role: string, status: string) {
  const query = search.trim().toLowerCase()
  return users.filter(user => (!role || user.roleCode === role) && (!status || user.status === status)
    && `${user.fullName} ${user.email} ${user.phoneNumber}`.toLowerCase().includes(query))
}
