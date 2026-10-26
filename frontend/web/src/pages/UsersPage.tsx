import { useState } from 'react'
import { Button, Card, Empty, Input, Table } from 'antd'
import { Link, useSearchParams } from 'react-router-dom'
import { useManagedUsers } from '../hooks/useManagedUsers'
import { filterUsers } from '../utils/userManagement'
import { statusLabels, userRoles, type ManagedUser } from '../types/userManagement'
import { UserIssue, UserPageHeading, UserStatus } from '../components/users/UserPageParts'
import { UserFilterSelect } from '../components/users/UserFilterSelect'
import './users.css'

export function UsersPage() {
  const { data, loading, issue, reload } = useManagedUsers()
  const [params, setParams] = useSearchParams()
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const role = params.get('role') || ''
  const status = params.get('status') || ''
  const filtered = filterUsers(data, search, role, status)
  function filter(name: string, value: string) {
    setPage(1)
    setParams(previous => { const next = new URLSearchParams(previous); if (value) next.set(name, value); else next.delete(name); return next })
  }
  const columns = [
    { title: 'Name', dataIndex: 'fullName', key: 'name' },
    { title: 'Email', dataIndex: 'email', key: 'email' },
    { title: 'Role', dataIndex: 'role', key: 'role' },
    { title: 'Status', key: 'status', render: (_: unknown, user: ManagedUser) => <UserStatus status={user.status} /> },
    { title: 'Action', key: 'action', width: 110, align: 'right' as const, render: (_: unknown, user: ManagedUser) => <Link className="users-row-action" aria-label={`View ${user.fullName}`} to={`/admin/users/${user.id}`}>View →</Link> },
  ]
  return <main className="users-page">
    <UserPageHeading title="Users" subtitle="Manage customer and staff accounts."
      action={<Link className="users-primary-link" to="/admin/users/new">Create staff account</Link>} />
    {issue ? <UserIssue issue={issue} retry={reload} /> : <Card className="users-table-card">
      <div className="users-filters">
        <label className="users-search">Search<Input aria-label="Search users" placeholder="Name, email or phone" value={search} onChange={event => { setSearch(event.target.value); setPage(1) }} allowClear /></label>
        <label>Role<UserFilterSelect label="Filter role" value={role} options={[{ value: '', label: 'All roles' }, ...userRoles]} onChange={value => filter('role', value)} /></label>
        <label>Status<UserFilterSelect label="Filter status" value={status} options={[{ value: '', label: 'All statuses' }, ...Object.entries(statusLabels).map(([value, label]) => ({ value, label }))]} onChange={value => filter('status', value)} /></label>
        <Button onClick={() => { setSearch(''); setParams({}); setPage(1) }}>Reset</Button>
      </div>
      <Table<ManagedUser> rowKey="id" loading={loading} columns={columns} dataSource={filtered} scroll={{ x: 760 }}
        pagination={{ current: page, pageSize: 10, showSizeChanger: false, hideOnSinglePage: true, onChange: setPage, showTotal: total => `${total} users` }}
        locale={{ emptyText: <Empty description="No matching users" image={Empty.PRESENTED_IMAGE_SIMPLE} /> }} />
    </Card>}
  </main>
}
