import { apiClient } from '../../services/apiClient'
import type { OrderPackage, OrderStation } from '../../types/orderResources'
import type { OrderStatusLog } from '../../types/customerOrder'

export interface DispatchOrder {
  orderId: string; trackingNumber: string; status: string
  package: OrderPackage; originStation: OrderStation; destinationStation: OrderStation
  scheduledDepartureTime: string | null; estimatedDeliveryTime: string | null
  actualDepartureTime: string | null; actualDeliveryTime: string | null
  assignmentStatus: 'ASSIGNED' | 'UNASSIGNED'; currentMission: {
    id: string; flightStatus: string; drone: { id: string; serialNumber: string; modelName: string; status: string }
    startSlotId: string | null; destinationSlotId: string | null; startTime: string | null; endTime: string | null
  } | null
  statusLogs: OrderStatusLog[]; createdAt: string; updatedAt: string
}
export interface ApprovedOrder {
  orderId: string; trackingNumber: string; status: string; packageId: string; packageName: string; weightGram: number
  originStation: { id: string; name: string; stationCode: string }; destinationStation: { id: string; name: string; stationCode: string }
  scheduledDepartureTime: string | null; estimatedDeliveryTime: string | null; assignmentStatus: 'ASSIGNED' | 'UNASSIGNED'
  missionId: string | null; droneId: string | null; droneSerialNumber: string | null; createdAt: string; updatedAt: string
}
export interface QueueFilters {
  originStationId?: string; destinationStationId?: string; searchKeyword?: string
  fromDate?: string; toDate?: string; scheduledFrom?: string; scheduledTo?: string
  assignmentStatus?: 'ASSIGNED' | 'UNASSIGNED'; page: number; pageSize: number
}
interface Queue { items: ApprovedOrder[]; page: number; pageSize: number; totalCount: number; totalPages: number }
export interface Schedule { orderId: string; scheduledDepartureTime: string; estimatedDeliveryTime: string; estimatedFlightMinutes: number }
export interface DroneCandidate {
  droneId: string; serialNumber: string; modelName: string; maxPayloadGram: number; maxFlightRangeKm: number
  currentStationId: string | null; status: string; isEligible: boolean; reasonsNotEligible: string[]
}
export interface Assignment { id: string; orderId: string; droneId: string; flightStatus: string; destinationSlotId: string; scheduledDepartureTime: string }
const path = (id: string) => '/api/delivery-orders/' + encodeURIComponent(id)
// delivery-workflow-api.md section 3, including LA-339 staff read contracts.
export const schedulingApi = {
  async queue(params: QueueFilters, signal: AbortSignal) { return (await apiClient.get<Queue>('/api/dispatch/approved-orders', { params, signal })).data },
  async order(id: string, signal: AbortSignal) { return (await apiClient.get<DispatchOrder>('/api/dispatch/orders/' + encodeURIComponent(id), { signal })).data },
  async schedule(id: string, scheduledDepartureTime: string) { return (await apiClient.put<Schedule>(path(id) + '/schedule', { scheduledDepartureTime })).data },
  async candidates(id: string, signal: AbortSignal) { return (await apiClient.get<DroneCandidate[]>(path(id) + '/available-drones', { signal })).data },
  async assign(id: string, droneId: string) { return (await apiClient.post<Assignment>(path(id) + '/flight-mission', { droneId })).data },
}
