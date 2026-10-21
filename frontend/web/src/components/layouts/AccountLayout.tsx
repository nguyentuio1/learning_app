import { Avatar, Button, Typography } from 'antd'
import { Link, Outlet } from 'react-router-dom'
import { useAuth } from '../../hooks/useAuth'
import './account-layout.css'

export function AccountLayout() {
  const auth = useAuth()
  return <div className="account-layout">
    <aside className="account-sidebar">
      <Link to="/profile" className="account-brand"><svg viewBox="0 0 32 32" aria-hidden="true"><path d="m9 23 15-15"/><circle cx="9" cy="23" r="3"/><circle cx="24" cy="8" r="3"/></svg><span>SmartDrone<small>Delivery operations</small></span></Link>
      <nav aria-label="Account navigation"><span className="account-nav-label">WORKSPACE</span><Link to="/profile" className="account-current" aria-current="page">My profile</Link></nav>
      <div className="account-sidebar-bottom"><span>{auth.user?.role.name}</span><Button onClick={() => void auth.logout()}>Sign out</Button></div>
    </aside>
    <div className="account-workspace"><header className="account-header">
      <span>Account / My profile</span>
      <div className="account-user"><Typography.Text>{auth.user?.fullName}</Typography.Text><Avatar src={auth.user?.avatarUrl || undefined} alt="Your profile photo">{auth.user?.fullName.slice(0, 1)}</Avatar></div>
    </header><Outlet /></div>
  </div>
}
