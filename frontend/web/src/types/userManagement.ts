import type { Role } from './auth'

// LA-92 response; unlike /api/users/me, role is a display name, not a nested object.
export interface ManagedUser {
  id: string
  email: string
  role: string
  roleCode: Role
  fullName: string
  phoneNumber: string
  status: string
  avatarUrl: string | null
  createdAt: string
}
export interface UserDetailsValues { fullName: string; email: string; phoneNumber: string }
export interface CreateUserValues extends UserDetailsValues { password: string; role: Role }

export const userRoles: { value: Role; label: string; description: string }[] = [
  { value: 'CUSTOMER', label: 'Customer', description: 'Delivery requests, packages and tracking.' },
  { value: 'DISPATCHER', label: 'Dispatcher', description: 'Review requests and coordinate delivery operations.' },
  { value: 'STATION_OPERATOR', label: 'Station Operator', description: 'Station availability, package receipt and pickup.' },
  { value: 'MANAGER', label: 'Logistics Manager', description: 'Delivery performance, reports and analytics.' },
  { value: 'ADMIN', label: 'System Administrator', description: 'User accounts, role assignments and system administration.' },
]
export const staffRoles = userRoles.filter(role => role.value !== 'CUSTOMER')
export const statusLabels: Record<string, string> = { ACTIVE: 'Active', INACTIVE: 'Inactive', SUSPENDED: 'Suspended' }
