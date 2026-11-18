import type { ComponentType } from 'react'
import { matchRoutes } from 'react-router-dom'
import type { Role } from '../../types/auth'

export interface OperationFeature {
  path: string
  label: string
  roles: readonly Role[]
  routes: { path: string; Page: ComponentType; roles?: readonly Role[] }[]
}

const modules = import.meta.glob<OperationFeature>('../*/operationFeature.ts', { eager: true, import: 'default' })
export const operationFeatures = Object.values(modules).sort((a, b) => a.label.localeCompare(b.label))

export function workspaceDestination(role?: string) {
  switch (role) {
    case 'MANAGER': return '/manager/analytics'
    case 'CUSTOMER': return '/addresses'
    default: return '/'
  }
}

const operationGroups = [
  { label: 'Delivery operations', paths: ['/dispatcher/approvals', '/dispatcher/scheduling', '/delivery-tracking', '/dispatcher/failures', '/delivery-map'] },
  { label: 'Fleet and stations', paths: ['/drones', '/stations', '/station-availability', '/station-slots'] },
  { label: 'Reports and insights', paths: ['/manager/analytics', '/delivery-eta', '/delivery-summary'] },
  { label: 'Help', paths: ['/delivery-assistant'] },
]

export function operationNavigation(role?: string) {
  const allowed = new Map(operationFeatures.filter(feature => feature.roles.some(value => value === role))
    .map(feature => [feature.path, feature]))
  const groups = operationGroups.map(({ label, paths }) => ({
    label,
    features: paths.flatMap(path => {
      const feature = allowed.get(path)
      allowed.delete(path)
      return feature ? [feature] : []
    }),
  }))
  groups.push({ label: 'Other tools', features: [...allowed.values()] })
  return groups.filter(group => group.features.length > 0)
}

export function operationDestination(path: unknown, role?: string) {
  if (typeof path !== 'string' || !path.startsWith('/') || path.startsWith('//') || !role) return
  for (const feature of operationFeatures) {
    const match = matchRoutes(feature.routes, path)?.at(-1)
    if (match?.params.id && !/^[a-f0-9-]{36}$/i.test(match.params.id)) continue
    if (match && (match.route.roles ?? feature.roles).some(allowed => allowed === role)) return path
  }
}
