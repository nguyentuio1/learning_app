import { lazy } from 'react'
import type { OperationFeature } from '../operations/navigation'

const DeliveryTrackingPage = lazy(() => import('./DeliveryTrackingPage').then(module => ({ default: module.DeliveryTrackingPage })))
export default {
  path: '/delivery-tracking', label: 'Delivery tracking', roles: ['DISPATCHER', 'MANAGER', 'ADMIN'],
  routes: [
    { path: '/delivery-tracking', Page: DeliveryTrackingPage },
    { path: '/delivery-tracking/:id', Page: DeliveryTrackingPage },
  ],
} satisfies OperationFeature
