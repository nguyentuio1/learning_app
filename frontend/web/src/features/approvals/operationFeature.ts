import { lazy } from 'react'
import type { OperationFeature } from '../operations/navigation'

const ApprovalQueuePage = lazy(() => import('./ApprovalQueuePage').then(module => ({ default: module.ApprovalQueuePage })))
export default {
  path: '/dispatcher/approvals', label: 'Delivery approvals', roles: ['ADMIN', 'DISPATCHER'],
  routes: [{ path: '/dispatcher/approvals', Page: ApprovalQueuePage }],
} satisfies OperationFeature
