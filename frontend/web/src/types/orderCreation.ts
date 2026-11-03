export interface OrderPackage {
  id: string
  packageName: string
  weightGram: number
  lengthCm: number
  widthCm: number
  heightCm: number
  packageImageUrl: string | null
  isFragile: boolean
}
export interface OrderAddress {
  id: string
  label: string
  contactName: string
  contactPhone: string
  addressLine: string
  isDefault: boolean
}
export interface OrderStation {
  id: string
  stationCode: string
  name: string
  address: string
  status: string
}
export interface CreateOrderValues {
  packageId: string
  originStationId: string
  destinationStationId: string
  deliveryAddressId?: string | null
  recipientName: string
  recipientPhone: string
}
// The pickup code belongs only to the creation response, never to a detail/list model.
export interface CreatedOrder {
  id: string
  trackingNumber: string
  status: string
  pickupSecureCode: string
}
