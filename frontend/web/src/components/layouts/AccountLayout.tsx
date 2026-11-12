import { Avatar, Button, ConfigProvider } from 'antd'
import { Link, NavLink, Outlet, useLocation } from 'react-router-dom'
import { useAuth } from '../../hooks/useAuth'
import { useAccountMotion } from '../../hooks/useAccountMotion'
import { operationFeatures } from '../../features/operations/navigation'
import './account-layout.css'
import './account-motion.css'

function AccountIcon({ name }: { name: 'users' | 'roles' | 'profile' | 'logout' | 'orders' | 'addresses' | 'packages' | 'drone' | 'route' | 'activity' | 'grid' | 'schedule' | 'alert' | 'clipboard' }) {
  return <svg className="account-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
    {name === 'drone' && <><rect x="8" y="8" width="8" height="8" rx="3"/><path d="m8 8-3-3m11 3 3-3M8 16l-3 3m11-3 3 3M2 5h6M16 5h6M2 19h6m8 0h6"/></>}
    {name === 'route' && <><circle cx="5" cy="18" r="2.5"/><circle cx="19" cy="6" r="2.5"/><path d="M8 18h5a4 4 0 0 0 0-8h-2a4 4 0 0 1 0-8h4"/></>}
    {name === 'activity' && <path d="M2 12h5l3-8 4 16 3-8h5"/>}
    {name === 'grid' && <><rect x="3" y="3" width="7" height="7" rx="1.5"/><rect x="14" y="3" width="7" height="7" rx="1.5"/><rect x="3" y="14" width="7" height="7" rx="1.5"/><rect x="14" y="14" width="7" height="7" rx="1.5"/></>}
    {name === 'schedule' && <><path d="M9 21H5a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2v3M7 2v4M17 2v4M3 10h8"/><circle cx="17" cy="17" r="5"/><path d="M17 14v3l2 1"/></>}
    {name === 'alert' && <path d="m12 3 10 18H2L12 3zM12 9v5M12 17v.5"/>}
    {name === 'clipboard' && <path d="M8 4H5v17h14V4h-3M9 3h6v4H9zM8 12h8M8 16h5"/>}
    {name === 'users' && <><circle cx="9" cy="8" r="3"/><path d="M3 21v-3a6 6 0 0 1 12 0v3M16 5a3 3 0 0 1 0 6M18 15a5 5 0 0 1 3 4v2"/></>}
    {name === 'roles' && <><path d="m12 3 8 3v6c0 5-8 9-8 9s-8-4-8-9V6Z"/><path d="m8 12 3 3 5-6"/></>}
    {name === 'profile' && <><circle cx="12" cy="8" r="4"/><path d="M4 21v-2a8 8 0 0 1 16 0v2"/></>}
    {name === 'logout' && <><path d="M9 4H4v16h5M9 12h12m-4-4 4 4-4 4"/></>}
    {name === 'orders' && <><path d="m12 3 9 5v9l-9 5-9-5V8Zm0 10v9M3 8l9 5 9-5M7 5l9 5"/></>}
    {name === 'addresses' && <><path d="M20 10c0 5-8 11-8 11S4 15 4 10a8 8 0 1 1 16 0Z"/><circle cx="12" cy="10" r="2.5"/></>}
    {name === 'packages' && <><path d="m4 8 8-4 8 4v9l-8 4-8-4V8Z"/><path d="m4 8 8 4 8-4M12 12v9"/></>}
  </svg>
}

const operationIcons: Record<string, Parameters<typeof AccountIcon>[0]['name']> = {
  '/drones': 'drone',
  '/stations': 'route',
  '/station-availability': 'activity',
  '/station-slots': 'grid',
  '/dispatcher/scheduling': 'schedule',
  '/dispatcher/failures': 'alert',
  '/dispatcher/approvals': 'clipboard',
}

export function AccountLayout() {
  const auth = useAuth()
  const { pathname } = useLocation()
  const motionRef = useAccountMotion(pathname)
  const isAdmin = auth.user?.role.code === 'ADMIN'
  const managedOrderId = pathname.match(/^\/orders\/([^/]+)\/(?:edit|manage)$/)?.[1]
  const initials = auth.user?.fullName.trim().split(/\s+/).slice(0, 2).map(part => part[0]).join('').toUpperCase() || 'SD'
  const operation = operationFeatures.find(feature => pathname === feature.path || pathname.startsWith(feature.path + '/'))
  const section = operation ? operation.label : pathname.startsWith('/orders') ? 'Orders' : pathname.startsWith('/admin/roles') ? 'Roles' : pathname.startsWith('/admin/users') ? 'Users' : pathname.startsWith('/addresses') ? 'Addresses' : pathname.startsWith('/packages') ? 'Packages' : 'My profile'
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
        {auth.user?.role.code === 'CUSTOMER' && <NavLink to="/orders" className={({ isActive }) => isActive ? 'account-current' : 'account-nav-link'}><AccountIcon name="orders"/>My orders</NavLink>}
        {auth.user?.role.code === 'CUSTOMER' && managedOrderId && <NavLink to={`/orders/${managedOrderId}/manage`} className={({ isActive }) => isActive ? 'account-current' : 'account-nav-link'}><AccountIcon name="orders"/>Manage this order</NavLink>}
        {auth.user?.role.code === 'CUSTOMER' && <NavLink to="/orders/new" className={({ isActive }) => isActive ? 'account-current' : 'account-nav-link'}><AccountIcon name="orders"/>Create order</NavLink>}
        {isAdmin && <>
          <NavLink to="/admin/users" className={({ isActive }) => isActive ? 'account-current' : 'account-nav-link'}><AccountIcon name="users"/>Users</NavLink>
          <NavLink to="/admin/roles" className={({ isActive }) => isActive ? 'account-current' : 'account-nav-link'}><AccountIcon name="roles"/>Roles</NavLink>
        </>}
        {auth.user?.role.code === 'CUSTOMER' && <NavLink to="/addresses" className={({ isActive }) => isActive ? 'account-current' : 'account-nav-link'}><AccountIcon name="addresses"/>Addresses</NavLink>}
        {auth.user?.role.code === 'CUSTOMER' && <NavLink to="/packages" className={({ isActive }) => isActive ? 'account-current' : 'account-nav-link'}><AccountIcon name="packages"/>Packages</NavLink>}
        {operationFeatures.filter(feature => feature.roles.some(role => role === auth.user?.role.code)).map(feature => (
          <NavLink key={feature.path} to={feature.path} className={({ isActive }) => isActive ? 'account-current' : 'account-nav-link'}>
            <AccountIcon name={operationIcons[feature.path] ?? 'grid'}/>{feature.label}
          </NavLink>
        ))}
      </nav>
      <div className="account-sidebar-bottom">
        <div className="account-workspace-role">{auth.user?.role.name}</div>
        <NavLink to="/profile" className={({ isActive }) => isActive ? 'account-current' : 'account-nav-link'}><AccountIcon name="profile"/>My profile</NavLink>
        <Button type="text" className="account-sign-out" icon={<AccountIcon name="logout"/>} onClick={() => void auth.logout()}>Sign out</Button>
      </div>
    </aside>
    <div className="account-workspace"><header className="account-header">
      <div className="account-breadcrumb"><span>{pathname.startsWith('/admin/') ? 'Administration' : (pathname.startsWith('/addresses') || pathname.startsWith('/packages')) ? 'Delivery details' : 'Account'}</span><span aria-hidden="true">/</span><strong>{section}</strong></div>
      <div className="account-user"><span className="account-role-badge"><span className="account-role-symbol"><AccountIcon name="roles"/></span>{auth.user?.role.name}</span><Link className="account-avatar-link" to="/profile" aria-label={`My profile: ${auth.user?.fullName}`}><Avatar size={38} src={auth.user?.avatarUrl || undefined} alt="Your profile photo">{initials}</Avatar></Link></div>
    </header><Outlet /></div>
  </div></ConfigProvider>
}
