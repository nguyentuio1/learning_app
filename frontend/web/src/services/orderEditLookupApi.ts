import { apiClient } from './apiClient'
import type { OrderAddress, OrderStation } from '../types/orderResources'

export const orderEditLookupApi = {
  async addresses(signal?: AbortSignal) {
    return (await apiClient.get<OrderAddress[]>('/api/customer-addresses', { signal })).data
  },
  async stations(signal?: AbortSignal) {
    return (await apiClient.get<OrderStation[]>('/api/landing-stations', { signal, params: { status: 'OPERATIONAL' } })).data
  },
}
