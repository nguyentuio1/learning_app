import { apiClient } from './apiClient'
import type { CustomerPackage, PackageValues } from '../types/customerPackage'

function options(signal?: AbortSignal) {
  if (!apiClient.defaults.baseURL) throw new Error('The API address is not configured.')
  return { signal }
}

function packagePath(id: string) { return `/api/packages/${encodeURIComponent(id)}` }

function packagePayload(values: PackageValues) {
  return {
    packageName: values.packageName.trim(),
    weightGram: values.weightGram,
    lengthCm: values.lengthCm,
    widthCm: values.widthCm,
    heightCm: values.heightCm,
    isFragile: values.isFragile,
    packageImageUrl: values.packageImageUrl ?? null,
  }
}

export const customerPackageApi = {
  async listPackages(signal?: AbortSignal) {
    return (await apiClient.get<CustomerPackage[]>('/api/packages', options(signal))).data
  },
  async getPackage(id: string, signal?: AbortSignal) {
    return (await apiClient.get<CustomerPackage>(packagePath(id), options(signal))).data
  },
  async createPackage(values: PackageValues, signal?: AbortSignal) {
    return (await apiClient.post<CustomerPackage>('/api/packages', packagePayload(values), options(signal))).data
  },
  async updatePackage(id: string, values: PackageValues, signal?: AbortSignal) {
    return (await apiClient.put<CustomerPackage>(packagePath(id), packagePayload(values), options(signal))).data
  },
  async deletePackage(id: string, signal?: AbortSignal) {
    await apiClient.delete(packagePath(id), options(signal))
  },
  async uploadPackageImage(id: string, file: File, signal?: AbortSignal) {
    const body = new FormData()
    body.append('file', file)
    return (await apiClient.post<{ imageUrl: string }>(`${packagePath(id)}/image`, body, {
      ...options(signal), headers: { 'Content-Type': 'multipart/form-data' },
    })).data
  },
}
