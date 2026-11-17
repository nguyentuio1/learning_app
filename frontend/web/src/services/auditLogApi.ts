import { apiClient } from './apiClient'
import type { AuditLogFilters, AuditLogPage } from '../types/auditLog'

/** Vietnam has no daylight saving time, so a calendar day is a fixed UTC+07:00 range. */
const VIETNAM_OFFSET = '+07:00'

export function auditLogQuery(filters: AuditLogFilters): Record<string, string> {
  const query: Record<string, string> = { page: String(filters.page), pageSize: String(filters.pageSize) }
  if (filters.userId) query.userId = filters.userId
  if (filters.action) query.action = filters.action
  if (filters.fromDate) query.from = `${filters.fromDate}T00:00:00${VIETNAM_OFFSET}`
  if (filters.toDate) query.to = `${filters.toDate}T23:59:59.999${VIETNAM_OFFSET}`
  return query
}

function options(signal?: AbortSignal) {
  if (!apiClient.defaults.baseURL) throw new Error('The API address is not configured.')
  return { signal }
}

export const auditLogApi = {
  async search(filters: AuditLogFilters, signal?: AbortSignal) {
    return (await apiClient.get<AuditLogPage>('/api/audit-logs', { ...options(signal), params: auditLogQuery(filters) })).data
  },
  async actions(signal?: AbortSignal) {
    return (await apiClient.get<string[]>('/api/audit-logs/actions', options(signal))).data
  },
}
