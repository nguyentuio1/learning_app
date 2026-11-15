import { apiClient } from '../../services/apiClient'

interface StationRef { id: string; stationCode: string; name: string }
export interface PendingOrder {
  orderId: string; trackingNumber: string; customerName: string; customerPhone: string
  packageName: string; weightGram: number; lengthCm: number; widthCm: number; heightCm: number; isFragile: boolean
  originStation: StationRef; destinationStation: StationRef; createdAt: string
}
export interface QueueFilters { originStationId?: string; destinationStationId?: string; fromDate?: string; toDate?: string; page: number; pageSize: number }
interface Queue { items: PendingOrder[]; page: number; pageSize: number; totalCount: number; totalPages: number }
export interface OrderDecision { orderId: string; trackingNumber: string; status: string; updatedAt: string }
const path = (id: string) => '/api/delivery-orders/' + encodeURIComponent(id)
// delivery-workflow-api.md section 2; no customer-only detail endpoint is used.
export const approvalApi = {
  async queue(filters: QueueFilters, signal: AbortSignal) {
    return (await apiClient.get<Queue>('/api/dispatch/pending-orders', { params: filters, signal })).data
  },
  async approve(id: string) { return (await apiClient.post<OrderDecision>(path(id) + '/approve')).data },
  async reject(id: string, reason: string) { return (await apiClient.post<OrderDecision>(path(id) + '/reject', { reason: reason.trim() })).data },
}
