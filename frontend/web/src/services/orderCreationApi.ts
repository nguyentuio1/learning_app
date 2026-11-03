import { apiClient } from './apiClient'
import type { CreatedOrder, CreateOrderValues, OrderAddress, OrderPackage, OrderStation } from '../types/orderCreation'

export const orderCreationApi = {
  async packages(signal?: AbortSignal) {
    return (await apiClient.get<OrderPackage[]>('/api/packages', { signal })).data
  },
  async addresses(signal?: AbortSignal) {
    return (await apiClient.get<OrderAddress[]>('/api/customer-addresses', { signal })).data
  },
  async stations(signal?: AbortSignal) {
    return (await apiClient.get<OrderStation[]>('/api/landing-stations', { signal, params: { status: 'OPERATIONAL' } })).data
  },
  async create(values: CreateOrderValues) {
    const body = {
      packageId: values.packageId, originStationId: values.originStationId,
      destinationStationId: values.destinationStationId, deliveryAddressId: values.deliveryAddressId || null,
      recipientName: values.recipientName.trim(), recipientPhone: values.recipientPhone.trim(),
    }
    return (await apiClient.post<CreatedOrder>('/api/delivery-orders', body)).data
  },
}
