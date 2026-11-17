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

export function operationDestination(path: unknown, role?: string) {
  if (typeof path !== 'string' || !path.startsWith('/') || path.startsWith('//') || !role) return
  for (const feature of operationFeatures) {
    const match = matchRoutes(feature.routes, path)?.at(-1)
    if (match?.params.id && !/^[a-f0-9-]{36}$/i.test(match.params.id)) continue
    if (match && (match.route.roles ?? feature.roles).some(allowed => allowed === role)) return path
  }
}
