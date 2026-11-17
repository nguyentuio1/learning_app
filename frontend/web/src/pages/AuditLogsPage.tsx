import { useEffect, useState } from 'react'
import axios from 'axios'
import { Button, Card, Empty, Input, Table, Typography } from 'antd'
import { useSearchParams } from 'react-router-dom'
import { auditLogApi } from '../services/auditLogApi'
import { userManagementApi } from '../services/userManagementApi'
import { userManagementError, type UserManagementError } from '../utils/userManagement'
import { UserFilterSelect } from '../components/users/UserFilterSelect'
import { UserIssue, UserPageHeading } from '../components/users/UserPageParts'
import type { AuditLogItem, AuditLogPage } from '../types/auditLog'
import type { ManagedUser } from '../types/userManagement'
import './users.css'

const PAGE_SIZE = 20
const timeFormat = new Intl.DateTimeFormat('en-GB', { timeZone: 'Asia/Ho_Chi_Minh', dateStyle: 'medium', timeStyle: 'medium' })

/** LA-177 / FR-33: read-only administrator search of the audit log, newest first. */
export function AuditLogsPage() {
  const [params, setParams] = useSearchParams()
  const userId = params.get('userId') || ''
  const action = params.get('action') || ''
  const fromDate = params.get('from') || ''
  const toDate = params.get('to') || ''
  const page = Number(params.get('page')) > 0 ? Number(params.get('page')) : 1
  const invalidRange = Boolean(fromDate && toDate && toDate < fromDate)
  const [attempt, setAttempt] = useState(0)
  // Results are tagged with the request they answer, so "loading" is derived instead of set inside the effect.
  const requestKey = JSON.stringify([userId, action, fromDate, toDate, page, attempt])
  const [response, setResponse] = useState<{ key: string; data?: AuditLogPage; issue?: UserManagementError } | null>(null)
  const current = response?.key === requestKey ? response : null
  const loading = !current
  const result = current?.data ?? (loading ? response?.data ?? null : null)
  const issue = current?.issue ?? null
  const [actions, setActions] = useState<string[]>([])
  const [users, setUsers] = useState<ManagedUser[]>([])

  useEffect(() => {
    const controller = new AbortController()
    auditLogApi.actions(controller.signal).then(setActions).catch(() => undefined)
    userManagementApi.list(controller.signal).then(setUsers).catch(() => undefined)
    return () => controller.abort()
  }, [])

  useEffect(() => {
    if (invalidRange) return
    const controller = new AbortController()
    auditLogApi.search({ userId, action, fromDate, toDate, page, pageSize: PAGE_SIZE }, controller.signal)
      .then(data => { if (!controller.signal.aborted) setResponse({ key: requestKey, data }) })
      .catch(error => {
        if (!controller.signal.aborted && !axios.isCancel(error)) setResponse({ key: requestKey, issue: userManagementError(error) })
      })
    return () => controller.abort()
  }, [userId, action, fromDate, toDate, page, requestKey, invalidRange])

  function update(changes: Record<string, string>) {
    setParams(previous => {
      const next = new URLSearchParams(previous)
      for (const [name, value] of Object.entries(changes)) {
        if (value) next.set(name, value)
        else next.delete(name)
      }
      if (!('page' in changes)) next.delete('page')
      return next
    })
  }

  const columns = [
    { title: 'Time', key: 'time', width: 190, render: (_: unknown, row: AuditLogItem) =>
      <time dateTime={row.timestamp}>{timeFormat.format(new Date(row.timestamp))}</time> },
    { title: 'User', key: 'user', render: (_: unknown, row: AuditLogItem) => row.userEmail
      ? <span>{row.userFullName}<br /><Typography.Text type="secondary">{row.userEmail}</Typography.Text></span>
      : <Typography.Text type="secondary">System</Typography.Text> },
    { title: 'Action', dataIndex: 'action', key: 'action' },
    { title: 'Entity', key: 'entity', render: (_: unknown, row: AuditLogItem) =>
      <span>{row.entityName}<br /><Typography.Text type="secondary" title={row.entityId}>{row.entityId.slice(0, 8)}</Typography.Text></span> },
    { title: 'Reason', dataIndex: 'reason', key: 'reason', render: (reason: string | null) => reason || '—' },
    { title: 'IP address', dataIndex: 'ipAddress', key: 'ip', render: (ip: string | null) => ip || '—' },
  ]

  return <main className="users-page">
    <UserPageHeading title="Audit logs" subtitle="Sensitive actions recorded by the system. Read-only, newest first." />
    <Card className="users-table-card">
      <div className="users-filters">
        <label>User<UserFilterSelect label="Filter user" value={userId}
          options={[{ value: '', label: 'All users' }, ...users.map(user => ({ value: user.id, label: `${user.fullName} (${user.email})` }))]}
          onChange={value => update({ userId: value })} /></label>
        <label>Action<UserFilterSelect label="Filter action" value={action}
          options={[{ value: '', label: 'All actions' }, ...actions.map(code => ({ value: code, label: code }))]}
          onChange={value => update({ action: value })} /></label>
        <label>From<Input type="date" aria-label="From date" value={fromDate} onChange={event => update({ from: event.target.value })} /></label>
        <label>To<Input type="date" aria-label="To date" value={toDate} onChange={event => update({ to: event.target.value })} /></label>
        <Button onClick={() => setParams({})}>Reset</Button>
      </div>
      {invalidRange ? <Typography.Paragraph type="danger" role="alert">The end date must not be before the start date.</Typography.Paragraph>
        : issue ? <UserIssue issue={issue} retry={() => setAttempt(value => value + 1)} />
        : <>{result && <Typography.Paragraph type="secondary" aria-live="polite">{result.totalCount} events</Typography.Paragraph>}
          <Table<AuditLogItem> rowKey="id" loading={loading} columns={columns} dataSource={result?.items ?? []} scroll={{ x: 900 }}
            pagination={{ current: page, pageSize: PAGE_SIZE, total: result?.totalCount ?? 0, showSizeChanger: false, hideOnSinglePage: true,
              onChange: next => update({ page: String(next) }) }}
            locale={{ emptyText: <Empty description="No audit events match these filters." image={Empty.PRESENTED_IMAGE_SIMPLE} /> }} /></>}
    </Card>
  </main>
}
