import { apiClient } from '../../services/apiClient'
import type { Station, StationSlot } from '../operations/stationTypes'
const stationPath = (id: string) => '/api/landing-stations/' + encodeURIComponent(id)
const slotPath = (id: string) => '/api/station-slots/' + encodeURIComponent(id)
export const slotApi = {
  async stations(signal: AbortSignal) { return (await apiClient.get<Station[]>('/api/landing-stations', { signal })).data },
  async inventory(id: string, signal: AbortSignal) {
    const [station, slots] = await Promise.all([
      apiClient.get<Station>(stationPath(id), { signal }),
      apiClient.get<StationSlot[]>(stationPath(id) + '/slots', { signal }),
    ])
    return { station: station.data, slots: slots.data }
  },
  async add(stationId: string, slotNumber?: number) {
    return (await apiClient.post<StationSlot>(stationPath(stationId) + '/slots', slotNumber == null ? {} : { slotNumber })).data
  },
  async remove(id: string) { await apiClient.delete(slotPath(id)) },
  async status(id: string, targetStatus: 'EMPTY' | 'ERROR', reason: string) {
    return (await apiClient.put<StationSlot>(slotPath(id) + '/status', { targetStatus, reason: reason.trim() })).data
  },
}
