import { apiClient } from '../../services/apiClient'
import type { OrderStatus } from '../../types/customerOrder'

// Sprint 6 contract, section 5 (LA-341): proposal for LA-153/154/155.
export interface PeriodQuery { fromDate: string; toDate: string }
export interface ReportingPeriod extends PeriodQuery {
  timeZone: 'Asia/Ho_Chi_Minh'; startUtc: string; endExclusiveUtc: string; cohortField: 'createdAt'
}
export interface DashboardResult {
  period: ReportingPeriod; metricDefinition: string; asOf: string; generatedAt: string; totalOrders: number
  ordersByStatus: { status: OrderStatus; count: number }[]
  successRate: { deliveredCount: number; failedCount: number; eligibleCount: number; percent: number | null }
  averageDelivery: { minutes: number | null; sampleCount: number; excludedCount: number }
  stationUsageMetric: 'THROUGHPUT'
  stationUsage: { stationId: string; stationCode: string; stationName: string; originOrderCount: number; destinationOrderCount: number; totalDistinctOrders: number }[]
}
interface ReportStation { id: string; stationCode: string; name: string }
export interface ReportRow {
  orderId: string; trackingNumber: string; status: OrderStatus
  originStation: ReportStation; destinationStation: ReportStation; createdAt: string
  actualDepartureTime: string | null; actualDeliveryTime: string | null; durationMinutes: number | null
}
export interface DeliveryReport {
  period: ReportingPeriod; metricDefinition: string; asOf: string; items: ReportRow[]
  page: number; pageSize: number; totalCount: number; totalPages: number
}
export const analyticsApi = {
  async dashboard(period: PeriodQuery, signal: AbortSignal) {
    return (await apiClient.get<DashboardResult>('/api/analytics/dashboard', { params: { fromDate: period.fromDate, toDate: period.toDate }, signal })).data
  },
  async report(period: PeriodQuery, page: number, pageSize: number, signal: AbortSignal) {
    const read = async (requestedPage: number) => (await apiClient.get<DeliveryReport>('/api/analytics/reports/deliveries', { params: { fromDate: period.fromDate, toDate: period.toDate, page: requestedPage, pageSize }, signal })).data
    const result = await read(page)
    return result.totalPages > 0 && page > result.totalPages ? read(result.totalPages) : result
  },
}
