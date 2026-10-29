import { Avatar, Button, ConfigProvider } from 'antd'
import { Link, NavLink, Outlet, useLocation } from 'react-router-dom'
import { useAuth } from '../../hooks/useAuth'
import { useAccountMotion } from '../../hooks/useAccountMotion'
import './account-layout.css'
import './account-motion.css'

function AccountIcon({ name }: { name: 'users' | 'roles' | 'profile' | 'logout' }) {
  return <svg className="account-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
    {name === 'users' && <><circle cx="9" cy="8" r="3"/><path d="M3 21v-3a6 6 0 0 1 12 0v3M16 5a3 3 0 0 1 0 6M18 15a5 5 0 0 1 3 4v2"/></>}
    {name === 'roles' && <><path d="m12 3 8 3v6c0 5-8 9-8 9s-8-4-8-9V6Z"/><path d="m8 12 3 3 5-6"/></>}
    {name === 'profile' && <><circle cx="12" cy="8" r="4"/><path d="M4 21v-2a8 8 0 0 1 16 0v2"/></>}
    {name === 'logout' && <><path d="M9 4H4v16h5M9 12h12m-4-4 4 4-4 4"/></>}
  </svg>
}

export function AccountLayout() {
  const auth = useAuth()
  const { pathname } = useLocation()
  const motionRef = useAccountMotion(pathname)
  const isAdmin = auth.user?.role.code === 'ADMIN'
  const initials = auth.user?.fullName.trim().split(/\s+/).slice(0, 2).map(part => part[0]).join('').toUpperCase() || 'SD'
  const section = pathname.startsWith('/admin/roles') ? 'Roles' : pathname.startsWith('/admin/users') ? 'Users' : 'My profile'
  return <ConfigProvider theme={{ token: {
    colorLink: '#4a704b', colorLinkHover: '#1e4f3c', colorLinkActive: '#163f2f',
    colorPrimaryHover: '#163f2f', colorPrimaryActive: '#163f2f',
    controlItemBgHover: '#f1f6eb', controlItemBgActiveHover: '#e6eedc',
    fontSize: 14, fontWeightStrong: 650, colorTextPlaceholder: '#65776b',
  } }}><div className="account-layout" ref={motionRef}>
    <aside className="account-sidebar">
      <span className="account-nav-marker" aria-hidden="true" />
      <Link to="/profile" className="account-brand"><svg viewBox="0 0 32 32" aria-hidden="true"><path d="m8 24 16-16"/><circle cx="8" cy="24" r="3"/><circle cx="24" cy="8" r="3"/></svg><span>SmartDrone<small>Delivery operations</small></span></Link>
      <nav aria-label="Account navigation"><span className="account-nav-label">WORKSPACE</span>
        {isAdmin && <>
          <NavLink to="/admin/users" className={({ isActive }) => isActive ? 'account-current' : 'account-nav-link'}><AccountIcon name="users"/>Users</NavLink>
          <NavLink to="/admin/roles" className={({ isActive }) => isActive ? 'account-current' : 'account-nav-link'}><AccountIcon name="roles"/>Roles</NavLink>
        </>}
      </nav>
      <div className="account-sidebar-bottom">
        <div className="account-workspace-role">{auth.user?.role.name}</div>
        <NavLink to="/profile" className={({ isActive }) => isActive ? 'account-current' : 'account-nav-link'}><AccountIcon name="profile"/>My profile</NavLink>
        <Button type="text" className="account-sign-out" icon={<AccountIcon name="logout"/>} onClick={() => void auth.logout()}>Sign out</Button>
      </div>
    </aside>
    <div className="account-workspace"><header className="account-header">
      <div className="account-breadcrumb"><span>{pathname.startsWith('/admin/') ? 'Administration' : 'Account'}</span><span aria-hidden="true">/</span><strong>{section}</strong></div>
      <div className="account-user"><span className="account-role-badge"><span className="account-role-symbol"><AccountIcon name="roles"/></span>{auth.user?.role.name}</span><Link className="account-avatar-link" to="/profile" aria-label={`My profile: ${auth.user?.fullName}`}><Avatar size={38} src={auth.user?.avatarUrl || undefined} alt="Your profile photo">{initials}</Avatar></Link></div>
    </header><Outlet /></div>
  </div></ConfigProvider>
}
