export interface CustomerPackage {
  id: string
  senderId: string
  packageName: string
  weightGram: number
  lengthCm: number
  widthCm: number
  heightCm: number
  packageImageUrl: string | null
  isFragile: boolean
  createdAt: string
}

export interface PackageValues {
  packageName: string
  weightGram: number
  lengthCm: number
  widthCm: number
  heightCm: number
  isFragile: boolean
}
