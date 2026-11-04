export interface CustomerAddress {
  id: string
  label: string
  contactName: string
  contactPhone: string
  addressLine: string
  latitude: number | null
  longitude: number | null
  isDefault: boolean
  createdAt: string
  updatedAt: string
}

export interface AddressValues {
  label: string
  contactName: string
  contactPhone: string
  addressLine: string
  latitude?: number
  longitude?: number
  isDefault: boolean
}

