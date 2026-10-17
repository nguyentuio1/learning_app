import { Alert, Button, Tag, Typography } from 'antd'
import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { statusLabels } from '../../types/userManagement'
import type { UserManagementError } from '../../utils/userManagement'

export function UserPageHeading({ title, subtitle, action }: { title: string; subtitle: string; action?: ReactNode }) {
  return <header className="users-heading"><div><span className="users-eyebrow">SYSTEM ADMINISTRATION</span>
    <Typography.Title level={1}>{title}</Typography.Title><Typography.Paragraph type="secondary">{subtitle}</Typography.Paragraph>
  </div><div className="users-actions">{action}</div></header>
}
export function UserStatus({ status }: { status: string }) {
  return <Tag className={`user-status user-status-${status.toLowerCase()}`} style={{
    backgroundColor: status === 'ACTIVE' ? '#dcefd5' : '#ecece6',
    color: status === 'ACTIVE' ? '#25673e' : '#5d635a', border: 0,
  }}>{statusLabels[status] || status}</Tag>
}
export function UserIssue({ issue, retry }: { issue: UserManagementError; retry?: () => void }) {
  return <Alert showIcon type={issue.status === 403 ? 'warning' : 'error'} title={issue.message}
    action={retry ? <Button onClick={retry}>Try again</Button> : undefined} />
}
export function BackToUsers() { return <Link to="/admin/users">← Users</Link> }
