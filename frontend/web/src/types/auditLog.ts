/** LA-177: GET /api/audit-logs (docs/api/auth-api.md, "Audit logs"). */
export interface AuditLogItem {
  id: number
  timestamp: string
  userId: string | null
  userEmail: string | null
  userFullName: string | null
  action: string
  entityName: string
  entityId: string
  ipAddress: string | null
  userAgent: string | null
  reason: string | null
}

export interface AuditLogPage {
  items: AuditLogItem[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export interface AuditLogFilters {
  userId?: string
  action?: string
  /** Calendar days (YYYY-MM-DD) in Asia/Ho_Chi_Minh, inclusive. */
  fromDate?: string
  toDate?: string
  page: number
  pageSize: number
}
