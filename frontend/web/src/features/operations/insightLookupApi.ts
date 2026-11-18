import { apiClient } from '../../services/apiClient'
import type { OrderStatus } from '../../types/customerOrder'

export interface InsightLookupFilters {
  searchKeyword?: string
  status?: OrderStatus
  fromDate?: string
  toDate?: string
}

interface InsightLookupPage {
  items: {
    id: string
    trackingNumber: string
    status: OrderStatus
    originStationName: string
    destinationStationName: string
    createdAt: string
  }[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export async function findInsightOrders(filters: InsightLookupFilters, page: number, pageSize: number, signal: AbortSignal) {
  const { data } = await apiClient.get<InsightLookupPage>('/api/delivery-insights/orders', {
    params: { ...filters, page, pageSize },
    signal,
  })
  return data
}
