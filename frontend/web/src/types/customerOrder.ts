import type { OrderAddress, OrderPackage, OrderStation } from './orderResources'

export const orderStatuses = ['PENDING', 'APPROVED', 'READY_FOR_TAKEOFF', 'IN_TRANSIT', 'ARRIVED', 'DELIVERED', 'CANCELLED', 'FAILED'] as const
export type OrderStatus = typeof orderStatuses[number]
export interface CustomerOrder {
  id: string
  trackingNumber: string
  status: OrderStatus
  recipientName: string
  recipientPhone: string
  packageId: string
  packageName: string
  originStationId: string
  originStationName: string
  destinationStationId: string
  destinationStationName: string
  deliveryAddressId: string | null
  createdAt: string
  updatedAt: string
}
export interface OrderStatusLog {
  fromStatus: OrderStatus | null
  toStatus: OrderStatus
  remarks: string | null
  createdAt: string
}
export interface CustomerOrderDetail extends CustomerOrder {
  package: OrderPackage
  originStation: OrderStation
  destinationStation: OrderStation
  deliveryAddress: OrderAddress | null
  cancelReason: string | null
  scheduledDepartureTime: string | null
  actualDepartureTime: string | null
  estimatedDeliveryTime: string | null
  actualDeliveryTime: string | null
  statusLogs: OrderStatusLog[]
}
export interface OrderPage {
  items: CustomerOrder[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}
export interface OrderQuery {
  page: number
  pageSize: number
  status?: OrderStatus
  searchKeyword?: string
  fromDate?: string
  toDate?: string
}
