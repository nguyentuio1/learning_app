import { lazy } from 'react'
import type { OperationFeature } from '../operations/navigation'

const Page = lazy(() => import('./CustomerAssistantPage').then(module => ({ default: module.CustomerAssistantPage })))

export default {
  path: '/delivery-assistant',
  label: 'Delivery assistant',
  roles: ['CUSTOMER'],
  routes: [ { path: '/delivery-assistant', Page }, { path: '/delivery-assistant/:id', Page } ],
} satisfies OperationFeature
