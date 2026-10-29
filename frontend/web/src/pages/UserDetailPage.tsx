import { useEffect, useRef, useState } from 'react'
import axios from 'axios'
import { Avatar, Alert, Button, Card, Form, Modal, Select, Skeleton, Typography } from 'antd'
import { useLocation, useParams } from 'react-router-dom'
import { useAuth } from '../hooks/useAuth'
import { useRequestController } from '../hooks/useRequestController'
import { userManagementApi } from '../services/userManagementApi'
import { userManagementError, type UserManagementError } from '../utils/userManagement'
import { userRoles, type ManagedUser, type UserDetailsValues } from '../types/userManagement'
import type { Role } from '../types/auth'
import { UserDetailsFields } from '../components/users/UserDetailsFields'
import { BackToUsers, UserIssue, UserPageHeading, UserStatus } from '../components/users/UserPageParts'
import './users.css'

export function UserDetailPage() {
  const { id = '' } = useParams()
  // Changing a route must discard the previous account's forms and pending operations.
  return <UserDetail key={id} id={id} />
}

function UserDetail({ id }: { id: string }) {
  const auth = useAuth()
  const location = useLocation()
  const [form] = Form.useForm<UserDetailsValues>()
  const [user, setUser] = useState<ManagedUser | null>(null)
  const [role, setRole] = useState<Role | undefined>()
  const [loading, setLoading] = useState(true)
  const [loadIssue, setLoadIssue] = useState<UserManagementError | null>(null)
  const [issue, setIssue] = useState<UserManagementError | null>(null)
  const [busy, setBusy] = useState(false)
  const [success, setSuccess] = useState(location.state?.created ? 'Account created successfully.' : '')
  const [attempt, setAttempt] = useState(0)
  const [confirm, setConfirm] = useState<'role' | 'deactivate' | null>(null)
  const locked = useRef(false)
  const isOwnAccount = auth.user?.id === id
  const start = useRequestController()
  useEffect(() => {
    const controller = new AbortController()
    userManagementApi.get(id, controller.signal).then(data => {
      if (controller.signal.aborted) return
      setUser(data); setRole(data.roleCode); form.setFieldsValue(data)
    }).catch(error => {
      if (!controller.signal.aborted && !axios.isCancel(error)) setLoadIssue(userManagementError(error))
    }).finally(() => { if (!controller.signal.aborted) setLoading(false) })
    return () => controller.abort()
  }, [id, attempt, form])

  function retry() { setUser(null); setLoading(true); setLoadIssue(null); setAttempt(value => value + 1) }
  async function save(operation: 'details' | 'role' | 'deactivate', values?: UserDetailsValues) {
    if (!user || locked.current || (operation === 'role' && !role)) return
    if (isOwnAccount && operation !== 'details') return
    locked.current = true; setBusy(true); setIssue(null); setSuccess('')
    const controller = start()
    try {
      let next = user
      if (operation === 'details' && values) next = await userManagementApi.update(id, values, controller.signal)
      if (operation === 'role' && role) {
        await userManagementApi.assignRole(id, role, controller.signal)
        next = { ...user, roleCode: role, role: userRoles.find(item => item.value === role)!.label }
      }
      if (operation === 'deactivate') {
        await userManagementApi.deactivate(id, controller.signal)
        next = { ...user, status: 'INACTIVE' }
      }
      if (controller.signal.aborted) return
      setUser(next); setConfirm(null)
      if (operation === 'details') {
        form.setFieldsValue(next)
        if (auth.user?.id === id) auth.updateProfile({ ...auth.user, fullName: next.fullName, phoneNumber: next.phoneNumber })
      }
      setSuccess(operation === 'details' ? 'Account details saved.' : operation === 'role' ? 'Role updated. It applies when the user receives their next access token.' : 'Account deactivated. Its records and delivery history are retained.')
    } catch (error) {
      if (!controller.signal.aborted && !axios.isCancel(error)) {
        const problem = userManagementError(error)
        setIssue(problem)
        if (operation === 'details') {
          const names: (keyof UserDetailsValues)[] = ['fullName', 'email', 'phoneNumber']
          form.setFields(names.map(name => ({ name, errors: problem.fields[name] || [] })))
        }
      }
    } finally { locked.current = false; if (!controller.signal.aborted) setBusy(false) }
  }
  const accessBlocked = issue?.status === 401 || issue?.status === 403 || issue?.status === 404
  return <main className="users-page">
    <UserPageHeading title="User details" subtitle="Account information, role assignment and access." action={<BackToUsers />} />
    {loading ? <Card aria-label="Loading user" aria-busy="true"><Skeleton active paragraph={{ rows: 6 }} /></Card>
      : loadIssue ? <UserIssue issue={loadIssue} retry={retry} />
      : user && <>
        {success && <Alert className="users-feedback" type="success" showIcon title={success} />}
        {issue && !confirm && <div className="users-feedback"><UserIssue issue={issue} /></div>}
        <div className="users-grid">
          <Card title="Account details">
            <div className="users-detail-summary"><Avatar size={52} src={user.avatarUrl || undefined}>{user.fullName.slice(0, 1)}</Avatar>
              <div><h2>{user.fullName}</h2><UserStatus status={user.status} /><small>Joined {new Date(user.createdAt).toLocaleDateString()}</small></div></div>
            <Form form={form} name="update-user" layout="vertical" disabled={busy || !!accessBlocked} onFinish={values => void save('details', values)}>
              <UserDetailsFields />
              <Button htmlType="submit" type="primary" loading={busy && !confirm}>Save details</Button>
            </Form>
          </Card>
          <div className="users-stack">
            <Card title="Role assignment">
              <label htmlFor="assigned-role">Assigned role</label>
              <Select id="assigned-role" aria-label="Assigned role" style={{ width: '100%', margin: '10px 0' }} value={role}
                onChange={setRole} options={userRoles} disabled={busy || !!accessBlocked || isOwnAccount} />
              <Typography.Paragraph type="secondary">{userRoles.find(item => item.value === role)?.description}</Typography.Paragraph>
              {isOwnAccount && <Typography.Paragraph type="secondary">You cannot change your own administrator role. Ask another administrator to manage your access.</Typography.Paragraph>}
              <Button type="primary" disabled={busy || !!accessBlocked || isOwnAccount || role === user.roleCode} onClick={() => { setIssue(null); setConfirm('role') }}>Save role</Button>
            </Card>
            <Card title="Account access">
              <Typography.Paragraph>Deactivation blocks sign-in and retains this account's records and delivery history.</Typography.Paragraph>
              {isOwnAccount && <Typography.Paragraph type="secondary">You cannot deactivate your own account.</Typography.Paragraph>}
              <Button danger disabled={busy || !!accessBlocked || isOwnAccount || user.status === 'INACTIVE'} onClick={() => { setIssue(null); setConfirm('deactivate') }}>
                {user.status === 'INACTIVE' ? 'Account inactive' : 'Deactivate account'}
              </Button>
            </Card>
          </div>
        </div>
        <Modal open={!!confirm} title={confirm === 'role' ? 'Change this role?' : 'Deactivate this account?'}
          onCancel={() => { if (!busy) { setConfirm(null); setIssue(null) } }} closable={!busy} keyboard={!busy} mask={{ closable: !busy }}
          confirmLoading={busy} okButtonProps={{ danger: confirm === 'deactivate', disabled: !!accessBlocked }}
          cancelButtonProps={{ disabled: busy }} okText={confirm === 'role' ? 'Confirm role change' : 'Confirm deactivation'}
          onOk={() => { if (confirm) void save(confirm) }}>
          <Typography.Paragraph>{user.fullName} · {user.email}</Typography.Paragraph>
          <Typography.Paragraph>{confirm === 'role' ? `${user.role} → ${userRoles.find(item => item.value === role)?.label}` : 'This account will be marked inactive. This does not delete the user.'}</Typography.Paragraph>
          {issue && <UserIssue issue={issue} />}
        </Modal>
      </>}
  </main>
}
