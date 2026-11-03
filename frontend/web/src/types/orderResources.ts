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
