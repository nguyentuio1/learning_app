import { lazy } from 'react'
import type { OperationFeature } from '../operations/navigation'
const AnalyticsPage = lazy(() => import('./AnalyticsPage').then(module => ({ default: module.AnalyticsPage })))
export default {
  path: '/manager/analytics', label: 'Dashboard & reports', mobileMenu: 'drawer', roles: ['MANAGER'],
  routes: [{ path: '/manager/analytics', Page: AnalyticsPage }],
} satisfies OperationFeature
