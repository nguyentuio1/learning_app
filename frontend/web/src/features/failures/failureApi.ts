import { apiClient } from '../../services/apiClient'
import type { OrderPackage, OrderStation } from '../../types/orderResources'
import type { OrderStatusLog } from '../../types/customerOrder'

export type FailureType = 'MISSION_ABORTED' | 'EMERGENCY_LANDING' | 'HARDWARE_FAULT'
export type RecordedFailureType = FailureType | 'COLLECTION_TIMEOUT' | 'UNKNOWN'
export type Resolution = 'RESCHEDULE' | 'RETURN_TO_ORIGIN' | 'CANCEL'
export type ResolutionStatus = 'UNRESOLVED' | 'RETURN_REQUESTED' | 'RESOLVED'
export interface Decision { orderId: string; trackingNumber: string; status: string; updatedAt: string }
export interface FailureEpisode {
  failureLogId: number; failureType: RecordedFailureType; failureReason: string | null; failedAt: string
  resolutionStatus: ResolutionStatus; lastResolutionAction: Resolution | null; resolvedAt: string | null
}
export interface FailedOrder extends FailureEpisode {
  orderId: string; trackingNumber: string; status: string; packageName: string
  originStation: { id: string; name: string; stationCode: string }; destinationStation: { id: string; name: string; stationCode: string }; updatedAt: string
}
export interface FailureOrder {
  orderId: string; trackingNumber: string; status: string; package: OrderPackage; originStation: OrderStation; destinationStation: OrderStation
  scheduledDepartureTime: string | null; estimatedDeliveryTime: string | null; actualDepartureTime: string | null; actualDeliveryTime: string | null
  assignmentStatus: 'ASSIGNED' | 'UNASSIGNED'; currentMission: {
    id: string; flightStatus: string; drone: { id: string; serialNumber: string; modelName: string; status: string }
    startSlotId: string | null; destinationSlotId: string | null; startTime: string | null; endTime: string | null
  } | null
  statusLogs: OrderStatusLog[]; createdAt: string; updatedAt: string
  latestFailure: FailureEpisode; returnToOriginStatus: 'NOT_REQUESTED' | 'REQUESTED' | 'CONFIRMED'
  parcelLocation: { stationId: string; slotId: string | null } | null
  failureHistory: FailureEpisode[]
  resolutionHistory: { failureLogId: number; action: Resolution | 'RETURN_CONFIRMED' | 'PARCEL_CLEARED'; note: string | null; performedBy: string | null; createdAt: string }[]
  resolutionHistoryComplete: boolean
}
export interface FailureFilters {
  originStationId?: string; destinationStationId?: string; searchKeyword?: string; fromDate?: string; toDate?: string
  failureType?: RecordedFailureType; resolutionStatus?: ResolutionStatus | 'ALL'; page: number; pageSize: number
}
interface Queue { items: FailedOrder[]; page: number; pageSize: number; totalCount: number; totalPages: number }
export interface ActiveDelivery {
  orderId: string; trackingNumber: string; orderStatus: string; missionId: string; flightStatus: string; droneSerialNumber: string
  originStationId: string; destinationStationId: string; scheduledDepartureTime: string | null; estimatedDeliveryTime: string | null
  lastTelemetryAt: string | null; telemetryStale: boolean
}
const path = (id: string) => '/api/delivery-orders/' + encodeURIComponent(id)
// delivery-workflow-api.md sections 5.1, 6.1/6.2 and LA-339 staff reads 6.4/6.5.
export const failureApi = {
  async queue(params: FailureFilters, signal: AbortSignal) { return (await apiClient.get<Queue>('/api/dispatch/failed-orders', { params, signal })).data },
  async order(id: string, signal: AbortSignal) { return (await apiClient.get<FailureOrder>('/api/dispatch/failed-orders/' + encodeURIComponent(id), { signal })).data },
  async active(signal: AbortSignal) { return (await apiClient.get<ActiveDelivery[]>('/api/dispatch/active-deliveries', { signal })).data },
  async report(id: string, failureType: FailureType, reason: string) { return (await apiClient.post<Decision>(path(id) + '/fail', { failureType, reason: reason.trim() })).data },
  async resolve(id: string, action: Resolution, note: string, packageVerified: boolean) {
    return (await apiClient.post<Decision>(path(id) + '/resolve-failure', { action, note: note.trim(), ...(action === 'RESCHEDULE' ? { packageVerified } : {}) })).data
  },
}
