import { lazy } from 'react'
import type { OperationFeature } from '../operations/navigation'

const DronesPage = lazy(() => import('./DronesPage').then(module => ({ default: module.DronesPage })))
const DronePage = lazy(() => import('./DronePage').then(module => ({ default: module.DronePage })))

export default {
  path: '/drones',
  label: 'Drone fleet',
  roles: ['ADMIN', 'DISPATCHER'],
  routes: [
    { path: '/drones', Page: DronesPage },
    { path: '/drones/new', Page: DronePage },
    { path: '/drones/:id', Page: DronePage },
  ],
} satisfies OperationFeature
