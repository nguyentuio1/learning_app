import { apiClient } from './apiClient'

export interface UpdateOrderValues {
  recipientName: string
  recipientPhone: string
  destinationStationId: string
  deliveryAddressId?: string | null
}
export const orderMutationApi = {
  async update(id: string, values: UpdateOrderValues) {
    // Never spread a detail object: the API explicitly rejects extra properties.
    await apiClient.put(`/api/delivery-orders/${encodeURIComponent(id)}`, {
      recipientName: values.recipientName.trim(), recipientPhone: values.recipientPhone.trim(),
      destinationStationId: values.destinationStationId, deliveryAddressId: values.deliveryAddressId || null,
    })
  },
  async cancel(id: string, reason: string) {
    await apiClient.post(`/api/delivery-orders/${encodeURIComponent(id)}/cancel`, { cancellationReason: reason.trim() })
  },
}
