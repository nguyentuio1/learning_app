import { lazy } from 'react'
import type { OperationFeature } from '../operations/navigation'

const StationAvailabilityPage = lazy(() => import('./StationAvailabilityPage').then(module => ({ default: module.StationAvailabilityPage })))

export default {
  path: '/station-availability',
  label: 'Station availability',
  roles: ['ADMIN', 'STATION_OPERATOR', 'DISPATCHER', 'MANAGER', 'CUSTOMER'],
  routes: [
    { path: '/station-availability', Page: StationAvailabilityPage },
  ],
} satisfies OperationFeature
