import { apiClient } from '../../services/apiClient'
import type { Station } from '../operations/stationTypes'
export const droneStatuses = ['IDLE', 'ASSIGNED', 'FLYING', 'CHARGING', 'MAINTENANCE'] as const
export type DroneStatus = typeof droneStatuses[number]
export interface Drone {
  id: string
  serialNumber: string
  modelName: string
  maxPayloadGram: number
  maxFlightRangeKm: number
  batteryCapacityMah: number
  status: DroneStatus
  currentStationId: string | null
  currentStationName: string | null
  createdAt: string
}
export interface DroneDetails extends Drone { activeMissionId: string | null }
export interface DroneValues { serialNumber: string; modelName: string; maxPayloadGram: number; maxFlightRangeKm: number; batteryCapacityMah: number; currentStationId?: string | null }
export interface MaintenanceValues { action: 'START' | 'COMPLETE'; expectedStatus: DroneStatus; reason: string; inspectionPassed?: boolean; readyForService?: boolean }
const path = (id: string) => '/api/drones/' + encodeURIComponent(id)
function editable(values: DroneValues) {
  return { modelName: values.modelName.trim(), maxPayloadGram: values.maxPayloadGram, maxFlightRangeKm: values.maxFlightRangeKm, batteryCapacityMah: values.batteryCapacityMah }
}
export const droneApi = {
  async list(signal: AbortSignal, filters: { search?: string; stationId?: string; status?: DroneStatus } = {}) {
    return (await apiClient.get<Drone[]>('/api/drones', { signal, params: filters })).data
  },
  async get(id: string, signal: AbortSignal) { return (await apiClient.get<DroneDetails>(path(id), { signal })).data },
  async stations(signal: AbortSignal) { return (await apiClient.get<Station[]>('/api/landing-stations', { signal })).data },
  async create(values: DroneValues) {
    return (await apiClient.post<Drone>('/api/drones', { ...editable(values), serialNumber: values.serialNumber.trim(), currentStationId: values.currentStationId || null })).data
  },
  async update(id: string, values: DroneValues) { return (await apiClient.put<Drone>(path(id), editable(values))).data },
  async maintenance(id: string, values: MaintenanceValues) {
    const body = { action: values.action, expectedStatus: values.expectedStatus, reason: values.reason.trim() }
    return (await apiClient.put<Drone>(path(id) + '/maintenance', values.action === 'COMPLETE' ? { ...body, inspectionPassed: values.inspectionPassed, readyForService: values.readyForService } : body)).data
  },
}
