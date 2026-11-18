import { Button } from 'antd'
import { Navigate } from 'react-router-dom'
import { approvalApi } from '../features/approvals/approvalApi'
import { OperationsPage, ResourceState } from '../features/operations/OperationsUi'
import { workspaceDestination } from '../features/operations/navigation'
import { useResource } from '../features/operations/useResource'
import { schedulingApi } from '../features/scheduling/schedulingApi'
import { trackingApi } from '../features/tracking/trackingApi'
import { useAuth } from '../hooks/useAuth'
import './workspace.css'

const loadPending = async (signal: AbortSignal) => (await approvalApi.queue({ page: 1, pageSize: 1 }, signal)).totalCount
const loadUnassigned = async (signal: AbortSignal) => (await schedulingApi.queue({ page: 1, pageSize: 1, assignmentStatus: 'UNASSIGNED' }, signal)).totalCount
const loadActive = async (signal: AbortSignal) => (await trackingApi.active(signal)).length

function QueueCard({ title, href, action, empty, load, primary }: {
  title: string; href: string; action: string; empty: string
  load: (signal: AbortSignal) => Promise<number>; primary?: boolean
}) {
  const queue = useResource(load)
  return <section className="operations-panel workspace-card">
    <h2>{title}</h2>
    <ResourceState loading={queue.loading} error={queue.error} retry={queue.reload} />
    {queue.data !== undefined && <p>{queue.data === 0 ? empty : queue.data}</p>}
    <Button type={primary ? 'primary' : 'default'} href={href}>{action}</Button>
  </section>
}

export function HomePage() {
  const { user } = useAuth()
  const role = user?.role.code
  if (role === 'MANAGER') return <Navigate to={workspaceDestination(role)} replace />
  if (role === 'ADMIN') return <OperationsPage title="Administration workspace" subtitle="Manage access and oversee delivery operations.">
    <div className="workspace-grid">
      <section className="operations-panel workspace-card"><h2>Administration</h2><div className="operations-actions">
        <Button type="primary" href="/admin/users">Manage users</Button>
        <Button href="/admin/roles">View roles</Button>
        <Button href="/admin/audit-logs">View audit logs</Button>
      </div></section>
      <section className="operations-panel workspace-card"><h2>Delivery operations</h2><div className="operations-actions">
        <Button href="/dispatcher/approvals">Delivery approvals</Button>
        <Button href="/dispatcher/scheduling">Delivery scheduling</Button>
        <Button href="/delivery-tracking">Delivery tracking</Button>
      </div></section>
    </div>
  </OperationsPage>
  return <OperationsPage title="Dispatch workspace" subtitle="Review requests, schedule approved deliveries and follow active flights.">
    <div className="workspace-grid">
      <QueueCard title="Pending approvals" href="/dispatcher/approvals" action="Open approvals" empty="No requests are waiting for a decision." load={loadPending} primary />
      <QueueCard title="Approved, without a drone" href="/dispatcher/scheduling" action="Open scheduling" empty="No approved deliveries are waiting for a drone." load={loadUnassigned} />
      <QueueCard title="Active deliveries" href="/delivery-tracking" action="Open tracking" empty="No active deliveries to track." load={loadActive} />
    </div>
  </OperationsPage>
}
