import { Card, Typography } from 'antd'
import { Link } from 'react-router-dom'
import { userRoles } from '../types/userManagement'
import { useManagedUsers } from '../hooks/useManagedUsers'
import { UserIssue, UserPageHeading } from '../components/users/UserPageParts'
import './users.css'

export function RolesPage() {
  const { data, loading, issue, reload } = useManagedUsers()
  return <main className="users-page"><UserPageHeading title="Roles" subtitle="Five defined roles. Assign access from a user account." />
    {issue && <div className="users-feedback"><UserIssue issue={issue} retry={reload} /></div>}
    <div className="users-role-grid">{userRoles.map(role => <Card key={role.value} className="users-role-card"
      title={<Typography.Title level={3}>{role.label}</Typography.Title>} extra={<code>{role.value}</code>}>
      <Typography.Paragraph>{role.description}</Typography.Paragraph>
      <Link to={`/admin/users?role=${role.value}`}>{loading || issue ? 'View accounts' : `${data.filter(user => user.roleCode === role.value).length} users · View accounts`} →</Link>
    </Card>)}</div>
    <Typography.Paragraph type="secondary">Roles are defined by the project. Custom roles and permission editing are not included.</Typography.Paragraph>
  </main>
}
