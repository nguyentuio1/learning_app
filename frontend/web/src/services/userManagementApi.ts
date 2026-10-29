import { apiClient } from './apiClient'
import type { CreateUserValues, ManagedUser, UserDetailsValues } from '../types/userManagement'
import type { Role } from '../types/auth'

function options(signal?: AbortSignal) {
  if (!apiClient.defaults.baseURL) throw new Error('The API address is not configured.')
  return { signal }
}
function path(id: string) { return `/api/users/${encodeURIComponent(id)}` }
function details(values: UserDetailsValues): UserDetailsValues {
  return { fullName: values.fullName.trim(), email: values.email.trim().toLowerCase(), phoneNumber: values.phoneNumber.trim() }
}
export const userManagementApi = {
  async list(signal?: AbortSignal) {
    return (await apiClient.get<ManagedUser[]>('/api/users', options(signal))).data
  },
  async get(id: string, signal?: AbortSignal) {
    return (await apiClient.get<ManagedUser>(path(id), options(signal))).data
  },
  async create(values: CreateUserValues, signal?: AbortSignal) {
    return (await apiClient.post<ManagedUser>('/api/users',
      { ...details(values), password: values.password, role: values.role }, options(signal))).data
  },
  async update(id: string, values: UserDetailsValues, signal?: AbortSignal) {
    return (await apiClient.put<ManagedUser>(path(id), details(values), options(signal))).data
  },
  async assignRole(id: string, role: Role, signal?: AbortSignal) {
    await apiClient.put(`${path(id)}/role`, { role }, options(signal))
  },
  async deactivate(id: string, signal?: AbortSignal) {
    await apiClient.put(`${path(id)}/deactivate`, undefined, options(signal))
  },
}
