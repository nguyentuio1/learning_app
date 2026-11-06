import { apiClient } from '../../services/apiClient'
import type { Station, StationStatus } from '../operations/stationTypes'
export interface Availability {
  stationId: string
  stationStatus: StationStatus
  totalSlots: number
  emptySlots: number
  occupiedSlots: number
  reservedSlots: number
  errorSlots: number
  isAvailableForArrival: boolean
  isAvailableForDeparture: boolean
  checkedAt: string
}
export const availabilityApi = {
  async stations(signal: AbortSignal) { return (await apiClient.get<Station[]>('/api/landing-stations', { signal })).data },
  async get(id: string, signal: AbortSignal) {
    return (await apiClient.get<Availability>('/api/landing-stations/' + encodeURIComponent(id) + '/availability', { signal })).data
  },
}
