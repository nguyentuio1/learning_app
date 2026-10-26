import { apiClient } from './apiClient'
import type { UserProfile, ProfileValues, PasswordValues } from '../types/profile'

function requestOptions(signal?: AbortSignal) {
  if (!apiClient.defaults.baseURL) {
    throw new Error('The API address is not configured. Set VITE_API_BASE_URL before loading your profile.')
  }
  return { signal, timeout: 20000 }
}

// Authentication belongs to the shared apiClient/auth workstream, not this feature.
export const profileApi = {
  async get(signal?: AbortSignal): Promise<UserProfile> {
    const response = await apiClient.get<UserProfile>('/api/users/me', requestOptions(signal))
    return response.data
  },
  async update(values: ProfileValues, signal?: AbortSignal): Promise<UserProfile> {
    const response = await apiClient.put<UserProfile>('/api/users/me', {
      fullName: values.fullName.trim(),
      phoneNumber: values.phoneNumber.trim(),
    }, requestOptions(signal))
    return response.data
  },
  async changePassword(values: PasswordValues, signal?: AbortSignal): Promise<void> {
    await apiClient.put('/api/users/me/password', {
      currentPassword: values.currentPassword,
      newPassword: values.newPassword,
    }, requestOptions(signal))
  },
  async uploadAvatar(file: File, signal?: AbortSignal): Promise<string> {
    const body = new FormData()
    body.append('file', file)
    const response = await apiClient.post<{ avatarUrl: string }>('/api/users/me/avatar', body, {
      ...requestOptions(signal),
      timeout: 60000,
      // Let the browser set the multipart boundary, even if the shared client defaults to JSON.
      headers: { 'Content-Type': undefined },
    })
    return response.data.avatarUrl
  },
}
