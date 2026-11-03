export interface CustomerPackage {
  id: string
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
  packageImageUrl?: string | null
  packageName: string
  weightGram: number
  lengthCm: number
  widthCm: number
  heightCm: number
  isFragile: boolean
}
