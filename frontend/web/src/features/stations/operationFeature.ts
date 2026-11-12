import { lazy } from 'react'
import type { OperationFeature } from '../operations/navigation'

const StationsPage = lazy(() => import('./StationsPage').then(module => ({ default: module.StationsPage })))
const StationPage = lazy(() => import('./StationPage').then(module => ({ default: module.StationPage })))

export default {
  path: '/stations',
  label: 'Landing stations',
  roles: ['ADMIN', 'STATION_OPERATOR', 'DISPATCHER'],
  routes: [
    { path: '/stations', Page: StationsPage },
    { path: '/stations/new', Page: StationPage, roles: ['ADMIN'] },
    { path: '/stations/:id', Page: StationPage },
  ],
} satisfies OperationFeature
