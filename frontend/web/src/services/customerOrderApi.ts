import { apiClient } from './apiClient'
import type { CustomerOrderDetail, OrderPage, OrderQuery } from '../types/customerOrder'

export const customerOrderApi = {
  async list(query: OrderQuery, signal?: AbortSignal) {
    return (await apiClient.get<OrderPage>('/api/delivery-orders', { params: query, signal })).data
  },
  async detail(id: string, signal?: AbortSignal) {
    return (await apiClient.get<CustomerOrderDetail>(`/api/delivery-orders/${encodeURIComponent(id)}`, { signal })).data
  },
}
