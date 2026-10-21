import type { AuthUser } from './auth'

export type UserProfile = AuthUser

export interface ProfileValues {
  fullName: string
  phoneNumber: string
}

export interface PasswordValues {
  currentPassword: string
  newPassword: string
}
