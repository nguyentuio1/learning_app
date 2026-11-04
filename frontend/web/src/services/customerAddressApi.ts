import { apiClient } from './apiClient'
import type { AddressValues, CustomerAddress } from '../types/customerAddress'

function options(signal?: AbortSignal) {
  if (!apiClient.defaults.baseURL) throw new Error('The API address is not configured.')
  return { signal }
}

function addressPath(id: string) { return `/api/customer-addresses/${encodeURIComponent(id)}` }

function addressPayload(values: AddressValues) {
  return {
    label: values.label.trim(),
    contactName: values.contactName.trim(),
    contactPhone: values.contactPhone.trim(),
    addressLine: values.addressLine.trim(),
    latitude: values.latitude ?? null,
    longitude: values.longitude ?? null,
    isDefault: values.isDefault,
  }
}

export const customerAddressApi = {
  async listAddresses(signal?: AbortSignal) {
    return (await apiClient.get<CustomerAddress[]>('/api/customer-addresses', options(signal))).data
  },
  async getAddress(id: string, signal?: AbortSignal) {
    return (await apiClient.get<CustomerAddress>(addressPath(id), options(signal))).data
  },
  async createAddress(values: AddressValues, signal?: AbortSignal) {
    return (await apiClient.post<CustomerAddress>('/api/customer-addresses', addressPayload(values), options(signal))).data
  },
  async updateAddress(id: string, values: AddressValues, signal?: AbortSignal) {
    return (await apiClient.put<CustomerAddress>(addressPath(id), addressPayload(values), options(signal))).data
  },
  async setDefaultAddress(id: string, signal?: AbortSignal) {
    return (await apiClient.put<CustomerAddress>(`${addressPath(id)}/default`, undefined, options(signal))).data
  },
  async deleteAddress(id: string, signal?: AbortSignal) {
    await apiClient.delete(addressPath(id), options(signal))
  },
}
