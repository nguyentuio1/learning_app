import { lazy } from 'react'
import type { OperationFeature } from '../operations/navigation'
const SchedulingPage = lazy(() => import('./SchedulingPage').then(module => ({ default: module.SchedulingPage })))
export default {
  path: '/dispatcher/scheduling', label: 'Delivery scheduling', roles: ['ADMIN', 'DISPATCHER'],
  routes: [{ path: '/dispatcher/scheduling', Page: SchedulingPage }, { path: '/dispatcher/scheduling/:id', Page: SchedulingPage }],
} satisfies OperationFeature
