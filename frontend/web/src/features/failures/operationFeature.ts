import { lazy } from 'react'
import type { OperationFeature } from '../operations/navigation'
const FailureHandlingPage = lazy(() => import('./FailureHandlingPage').then(module => ({ default: module.FailureHandlingPage })))
export default {
  path: '/dispatcher/failures', label: 'Failed deliveries', roles: ['ADMIN', 'DISPATCHER'],
  routes: [{ path: '/dispatcher/failures', Page: FailureHandlingPage }, { path: '/dispatcher/failures/:id', Page: FailureHandlingPage, roles: ['ADMIN', 'DISPATCHER'] }],
} satisfies OperationFeature
