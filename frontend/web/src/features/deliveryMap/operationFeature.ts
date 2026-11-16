import { lazy } from 'react'
import type { OperationFeature } from '../operations/navigation'
const DeliveryMapPage = lazy(() => import('./DeliveryMapPage').then(module => ({ default: module.DeliveryMapPage })))
export default {
  path: '/delivery-map', label: 'Delivery map', roles: ['DISPATCHER', 'MANAGER', 'ADMIN'],
  routes: [{ path: '/delivery-map', Page: DeliveryMapPage }, { path: '/delivery-map/:id', Page: DeliveryMapPage }],
} satisfies OperationFeature
