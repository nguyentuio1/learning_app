import { lazy } from 'react'
import type { OperationFeature } from '../operations/navigation'

const Page = lazy(() => import('./DeliveryEtaPage').then(module => ({ default: module.DeliveryEtaPage })))

export default {
  path: '/delivery-eta',
  label: 'Arrival estimates',
  roles: ['CUSTOMER', 'DISPATCHER', 'MANAGER'],
  routes: [ { path: '/delivery-eta', Page }, { path: '/delivery-eta/:id', Page } ],
} satisfies OperationFeature
