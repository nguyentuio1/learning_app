import { apiClient } from '../../services/apiClient'
import type { Station, StationSlot, StationStatus } from '../operations/stationTypes'

export interface StationDetails extends Station { slots: StationSlot[]; hostedDroneCount: number }
export interface StationValues { stationCode: string; name: string; address: string; latitude: number; longitude: number; totalSlots: number; operatorId?: string | null }
const path = (id: string) => '/api/landing-stations/' + encodeURIComponent(id)
function editable(values: StationValues) {
  return { name: values.name.trim(), address: values.address.trim(), latitude: values.latitude, longitude: values.longitude, operatorId: values.operatorId || null }
}
export const stationApi = {
  async list(signal: AbortSignal, filters: { search?: string; status?: StationStatus } = {}) {
    return (await apiClient.get<Station[]>('/api/landing-stations', { signal, params: filters })).data
  },
  async get(id: string, signal: AbortSignal) { return (await apiClient.get<StationDetails>(path(id), { signal })).data },
  async create(values: StationValues) {
    return (await apiClient.post<Station>('/api/landing-stations', { ...editable(values), stationCode: values.stationCode.trim(), totalSlots: values.totalSlots })).data
  },
  async update(id: string, values: StationValues) { return (await apiClient.put<Station>(path(id), editable(values))).data },
  async status(id: string, newStatus: StationStatus, reason: string) {
    return (await apiClient.put<{ station: Station; rerouteWarning: boolean; affectedMissionIds: string[] }>(path(id) + '/status', { newStatus, reason: reason.trim() })).data
  },
}
