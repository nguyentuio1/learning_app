export const stationStatuses = ['OPERATIONAL', 'MAINTENANCE', 'OFFLINE'] as const
export type StationStatus = typeof stationStatuses[number]
export interface Station {
  id: string
  stationCode: string
  name: string
  address: string
  latitude: number
  longitude: number
  status: StationStatus
  totalSlots: number
  emptySlots: number
  operatorId: string | null
  operatorName: string | null
  createdAt: string
  updatedAt: string
}
export interface StationSlot {
  id: string
  stationId: string
  slotNumber: number
  status: 'EMPTY' | 'OCCUPIED' | 'RESERVED' | 'ERROR'
  lastUpdated: string
  currentOrderId: string | null
  currentMissionId: string | null
}
