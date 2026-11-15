import { apiClient } from '../../services/apiClient'
import type { OrderStatus } from '../../types/customerOrder'

// delivery-workflow-api.md, sections 5.1 and 5.2.
export interface ActiveDelivery {
  orderId: string
  trackingNumber: string
  orderStatus: OrderStatus
  missionId: string
  flightStatus: string
  droneSerialNumber: string
  originStationId: string
  destinationStationId: string
  scheduledDepartureTime: string | null
  estimatedDeliveryTime: string | null
  lastTelemetryAt: string | null
  telemetryStale: boolean
}
export interface TrackingStation { id: string; name: string; latitude: number; longitude: number }
export interface Position {
  latitude: number; longitude: number; altitudeMeters: number
  speedMps: number; batteryPercentage: number; timestamp: string
}
export interface TrackingSnapshot {
  orderId: string; trackingNumber: string; status: OrderStatus
  originStation: TrackingStation; destinationStation: TrackingStation
  estimatedDeliveryTime: string | null
  lastPosition: Position | null
  telemetryStale: boolean
}
export const trackingApi = {
  async active(signal: AbortSignal) {
    return (await apiClient.get<ActiveDelivery[]>('/api/dispatch/active-deliveries', { signal })).data
  },
  async snapshot(id: string, signal: AbortSignal) {
    return (await apiClient.get<TrackingSnapshot>(`/api/delivery-orders/${encodeURIComponent(id)}/tracking`, { signal })).data
  },
}
