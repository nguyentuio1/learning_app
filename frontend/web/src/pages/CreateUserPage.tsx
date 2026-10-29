import { useRef, useState } from 'react'
import axios from 'axios'
import { Button, Card, Form, Input, Select, Typography } from 'antd'
import { useNavigate } from 'react-router-dom'
import { userManagementApi } from '../services/userManagementApi'
import { staffRoles, type CreateUserValues } from '../types/userManagement'
import { userManagementError, type UserManagementError } from '../utils/userManagement'
import { useRequestController } from '../hooks/useRequestController'
import { UserDetailsFields } from '../components/users/UserDetailsFields'
import { BackToUsers, UserIssue, UserPageHeading } from '../components/users/UserPageParts'
import './users.css'

export function CreateUserPage() {
  const navigate = useNavigate()
  const [form] = Form.useForm<CreateUserValues>()
  const role = Form.useWatch('role', form) || 'DISPATCHER'
  const [busy, setBusy] = useState(false)
  const [issue, setIssue] = useState<UserManagementError | null>(null)
  const locked = useRef(false)
  const start = useRequestController()
  async function submit(values: CreateUserValues) {
    if (locked.current) return
    locked.current = true; setBusy(true); setIssue(null)
    const controller = start()
    try {
      const created = await userManagementApi.create(values, controller.signal)
      if (!controller.signal.aborted) { form.resetFields(); navigate(`/admin/users/${created.id}`, { replace: true, state: { created: true } }) }
    } catch (error) {
      if (!controller.signal.aborted && !axios.isCancel(error)) {
        const problem = userManagementError(error)
        setIssue(problem)
        const names: (keyof CreateUserValues)[] = ['fullName', 'email', 'phoneNumber', 'password', 'role']
        form.setFields(names.map(name => ({ name, errors: problem.fields[name] || [] })))
      }
    } finally { locked.current = false; if (!controller.signal.aborted) setBusy(false) }
  }
  return <main className="users-page">
    <UserPageHeading title="Create staff account" subtitle="Set up an account and assign its initial access." action={<BackToUsers />} />
    <div className="users-grid"><Card title="Account details">
      <Form form={form} name="create-user" layout="vertical" initialValues={{ role: 'DISPATCHER' }} onFinish={submit} disabled={busy}>
        <UserDetailsFields />
        <Form.Item name="role" label="Role" rules={[{ required: true }]}><Select options={staffRoles} /></Form.Item>
        <Form.Item name="password" label="Initial password" extra="8–100 characters, including a letter and a digit."
          rules={[{ required: true }, { min: 8, max: 100 }, { pattern: /^(?=.*[A-Za-z])(?=.*\d).+$/, message: 'Include at least one letter and one digit.' }]}>
          <Input.Password autoComplete="new-password" maxLength={100} />
        </Form.Item>
        {issue && <div className="users-feedback"><UserIssue issue={issue} /></div>}
        <div className="users-actions"><Button type="primary" htmlType="submit" loading={busy}>Create account</Button><Button disabled={busy} onClick={() => navigate('/admin/users')}>Cancel</Button></div>
      </Form>
    </Card><Card title="Account access" className="users-access-card"><span className="users-role-symbol" aria-hidden="true">↗</span>
      <Typography.Title level={3}>{staffRoles.find(item => item.value === role)?.label}</Typography.Title>
      <Typography.Paragraph>{staffRoles.find(item => item.value === role)?.description}</Typography.Paragraph>
      <Typography.Paragraph type="secondary">New accounts are active. Customers register through the customer application.</Typography.Paragraph>
    </Card></div>
  </main>
}
