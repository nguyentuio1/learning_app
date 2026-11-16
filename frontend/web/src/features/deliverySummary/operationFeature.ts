import { lazy } from 'react'
import type { OperationFeature } from '../operations/navigation'

const Page = lazy(() => import('./DeliverySummaryPage').then(module => ({ default: module.DeliverySummaryPage })))

export default {
  path: '/delivery-summary',
  label: 'Delivery summaries',
  roles: ['CUSTOMER', 'DISPATCHER', 'MANAGER'],
  routes: [ { path: '/delivery-summary', Page }, { path: '/delivery-summary/:id', Page } ],
} satisfies OperationFeature
