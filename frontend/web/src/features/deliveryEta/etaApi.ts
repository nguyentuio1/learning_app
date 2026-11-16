import { apiClient } from '../../services/apiClient'
import type { OrderStatus } from '../../types/customerOrder'

// Sprint 7 API contract (#209), section 3.
export interface EtaPrediction {
  calculatedDistanceKm: number
  predictedDurationMinutes: number
  weatherCondition: string | null
  confidenceScore: number | null
  createdAt: string
}

export interface DeliveryEta {
  orderId: string
  status: OrderStatus
  estimatedDeliveryTime: string | null
  actualDeliveryTime: string | null
  aiEtaPrediction: EtaPrediction | null
}

export async function getDeliveryEta(id: string, signal: AbortSignal) {
  const { data } = await apiClient.get<DeliveryEta>(
    '/api/delivery-orders/' + encodeURIComponent(id) + '/eta',
    { signal },
  )
  if (data.orderId.toLowerCase() !== id.toLowerCase()) throw new Error('Unexpected delivery estimate')
  return data
}
