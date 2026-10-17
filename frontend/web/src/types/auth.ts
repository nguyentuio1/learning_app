export interface AuthUser {
  id: string
  email: string
  fullName: string
  phoneNumber: string
  avatarUrl: string | null
  status: string
  role: { id: number; code: string; name: string }
  createdAt: string
}

export interface AuthResponse { accessToken: string; refreshToken: string; user: AuthUser; expiresIn: number }
export interface Session {
  status: 'loading' | 'anonymous' | 'authenticated'
  user: AuthUser | null
  accessToken: string | null
  refreshToken: string | null
  generation: number
  message: string
}

export const managementRoles = ['DISPATCHER', 'MANAGER', 'ADMIN'] as const
export const allRoles = ['CUSTOMER', 'DISPATCHER', 'STATION_OPERATOR', 'MANAGER', 'ADMIN'] as const
export type Role = typeof allRoles[number]
