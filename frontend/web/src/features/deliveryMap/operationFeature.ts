import { lazy } from 'react'
import type { OperationFeature } from '../operations/navigation'
const DeliveryMapPage = lazy(() => import('./DeliveryMapPage').then(module => ({ default: module.DeliveryMapPage })))
const CustomerDeliveryMapPage = lazy(() => import('./DeliveryMapPage').then(module => ({ default: module.CustomerDeliveryMapPage })))
export default {
  path: '/delivery-map', label: 'Delivery map', roles: ['DISPATCHER', 'MANAGER', 'ADMIN'],
  routes: [
    { path: '/delivery-map', Page: DeliveryMapPage },
    { path: '/delivery-map/:id', Page: DeliveryMapPage },
    { path: '/orders/:id/map', Page: CustomerDeliveryMapPage, roles: ['CUSTOMER'] },
  ],
} satisfies OperationFeature
