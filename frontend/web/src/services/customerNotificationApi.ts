import { apiClient } from './apiClient'

export interface CustomerNotification {
  id: string
  orderId?: string | null
  type: string
  title: string
  message: string
  isRead: boolean
  createdAt: string
  readAt?: string | null
}

export interface NotificationPage {
  items: CustomerNotification[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export interface UnreadCountResponse {
  unreadCount: number
}

export interface ReadAllResponse {
  updatedCount: number
}

export interface NotificationQuery {
  isRead?: boolean
  page?: number
  pageSize?: number
}

export const customerNotificationApi = {
  async list(query?: NotificationQuery, signal?: AbortSignal): Promise<NotificationPage> {
    const response = await apiClient.get<NotificationPage>('/api/notifications', {
      params: query,
      signal,
    })
    return response.data
  },

  async unreadCount(signal?: AbortSignal): Promise<number> {
    const response = await apiClient.get<UnreadCountResponse>('/api/notifications/unread-count', {
      signal,
    })
    return response.data.unreadCount
  },

  async markAsRead(id: string): Promise<CustomerNotification> {
    const response = await apiClient.put<CustomerNotification>(`/api/notifications/${encodeURIComponent(id)}/read`)
    return response.data
  },

  async markAllAsRead(): Promise<number> {
    const response = await apiClient.put<ReadAllResponse>('/api/notifications/read-all')
    return response.data.updatedCount
  },
}
