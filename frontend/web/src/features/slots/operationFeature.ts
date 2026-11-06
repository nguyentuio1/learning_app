import { lazy } from 'react'
import type { OperationFeature } from '../operations/navigation'

const StationSlotsPage = lazy(() => import('./StationSlotsPage').then(module => ({ default: module.StationSlotsPage })))

export default {
  path: '/station-slots',
  label: 'Station slots',
  roles: ['ADMIN', 'STATION_OPERATOR', 'DISPATCHER'],
  routes: [
    { path: '/station-slots', Page: StationSlotsPage },
  ],
} satisfies OperationFeature
