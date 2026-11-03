import { apiClient } from './apiClient'
import type { CustomerOrderDetail } from '../types/customerOrder'

export const customerOrderApi = {
  async detail(id: string, signal?: AbortSignal) {
    return (await apiClient.get<CustomerOrderDetail>(`/api/delivery-orders/${encodeURIComponent(id)}`, { signal })).data
  },
}
