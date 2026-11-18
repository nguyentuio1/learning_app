import { useEffect, useState } from 'react'
import { Avatar, Badge, Button, ConfigProvider } from 'antd'
import { Link, NavLink, Outlet, useLocation } from 'react-router-dom'
import { useAuth } from '../../hooks/useAuth'
import { useAccountMotion } from '../../hooks/useAccountMotion'
import { operationFeatures, operationNavigation, workspaceDestination } from '../../features/operations/navigation'
import { customerNotificationApi } from '../../services/customerNotificationApi'
import brandLogo from '../../assets/smartdrone-logo-02.png'
import { AccountMobileNavigation } from './AccountMobileNavigation'
import './account-layout.css'
import './account-motion.css'

type AccountIconName =
  | 'users' | 'roles' | 'audit' | 'profile' | 'logout' | 'notifications' | 'assistant'
  | 'orders' | 'create-order' | 'edit-order' | 'addresses' | 'packages' | 'summary'
  | 'drone' | 'map' | 'route' | 'landing-pad' | 'activity' | 'grid'
  | 'schedule' | 'clock' | 'alert' | 'approval' | 'chart' | 'home'

function AccountIcon({ name }: { name: AccountIconName }) {
  return <svg className="account-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
    {name === 'drone' && <><rect x="8" y="8" width="8" height="8" rx="3"/><path d="m8 8-3-3m11 3 3-3M8 16l-3 3m11-3 3 3M2 5h6M16 5h6M2 19h6m8 0h6"/></>}
    {name === 'route' && <><circle cx="5" cy="18" r="2.5"/><circle cx="19" cy="6" r="2.5"/><path d="M8 18h5a4 4 0 0 0 0-8h-2a4 4 0 0 1 0-8h4"/></>}
    {name === 'activity' && <path d="M2 12h5l3-8 4 16 3-8h5"/>}
    {name === 'grid' && <><rect x="3" y="3" width="7" height="7" rx="1.5"/><rect x="14" y="3" width="7" height="7" rx="1.5"/><rect x="3" y="14" width="7" height="7" rx="1.5"/><rect x="14" y="14" width="7" height="7" rx="1.5"/></>}
    {name === 'schedule' && <><path d="M9 21H5a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2v3M7 2v4M17 2v4M3 10h8"/><circle cx="17" cy="17" r="5"/><path d="M17 14v3l2 1"/></>}
    {name === 'alert' && <path d="m12 3 10 18H2L12 3zM12 9v5M12 17v.5"/>}
    {name === 'approval' && <path d="M8 4H5v17h14V4h-3M9 3h6v4H9zM8.5 13.5l2.3 2.3 4.7-4.7"/>}
    {name === 'users' && <><circle cx="9" cy="8" r="3"/><path d="M3 21v-3a6 6 0 0 1 12 0v3M16 5a3 3 0 0 1 0 6M18 15a5 5 0 0 1 3 4v2"/></>}
    {name === 'roles' && <><path d="m12 3 8 3v6c0 5-8 9-8 9s-8-4-8-9V6Z"/><path d="m8 12 3 3 5-6"/></>}
    {name === 'audit' && <><path d="M6 3h9l4 4v14H6z"/><path d="M14 3v5h5M9 12h7M9 16h7"/></>}
    {name === 'profile' && <><circle cx="12" cy="8" r="4"/><path d="M4 21v-2a8 8 0 0 1 16 0v2"/></>}
    {name === 'logout' && <><path d="M9 4H4v16h5M9 12h12m-4-4 4 4-4 4"/></>}
    {name === 'orders' && <><rect x="4" y="4" width="16" height="16" rx="2"/><path d="M8 8h.1M8 12h.1M8 16h.1M11 8h5m-5 4h5m-5 4h5"/></>}
    {name === 'addresses' && <><path d="M20 10c0 5-8 11-8 11S4 15 4 10a8 8 0 1 1 16 0Z"/><circle cx="12" cy="10" r="2.5"/></>}
    {name === 'packages' && <><path d="m4 8 8-4 8 4v9l-8 4-8-4V8Z"/><path d="m4 8 8 4 8-4M12 12v9"/></>}
    {name === 'map' && <path d="m3 6 6-3 6 3 6-3v15l-6 3-6-3-6 3V6Zm6-3v15m6-12v15"/>}
    {name === 'landing-pad' && <><rect x="3" y="3" width="18" height="18" rx="4"/><path d="M9 8v8m6-8v8m-6-4h6"/></>}
    {name === 'clock' && <><circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/></>}
    {name === 'chart' && <path d="M4 3v18h17M8 16v-5m5 5V7m5 9V4"/>}
    {name === 'home' && <path d="m3 10 9-7 9 7v10h-6v-6H9v6H3z"/>}
    {name === 'summary' && <path d="M6 3h8l4 4v14H6V3Zm8 0v5h4m-9 4h6m-6 4h4"/>}
    {name === 'create-order' && <path d="M6 3h8l4 4v14H6V3Zm8 0v5h4m-6 3v6m-3-3h6"/>}
    {name === 'edit-order' && <path d="m4 20 4.5-1 11-11-3.5-3.5-11 11L4 20Zm10-13 3.5 3.5M4 20h16"/>}
    {name === 'assistant' && <><path d="M4 5h16a1.5 1.5 0 0 1 1.5 1.5v9A1.5 1.5 0 0 1 20 17H9l-5 4v-4a1.5 1.5 0 0 1-1.5-1.5v-9A1.5 1.5 0 0 1 4 5Z"/><path d="M7 9h10m-10 4h6"/></>}
    {name === 'notifications' && <><path d="M6 8a6 6 0 0 1 12 0c0 7 3 9 3 9H3s3-2 3-9"/><path d="M10.3 21a1.94 1.94 0 0 0 3.4 0"/></>}
  </svg>
}

const operationIcons: Record<string, AccountIconName> = {
  '/delivery-map': 'map',
  '/delivery-tracking': 'route',
  '/delivery-eta': 'clock',
  '/delivery-summary': 'summary',
  '/delivery-assistant': 'assistant',
  '/dispatcher/approvals': 'approval',
  '/dispatcher/scheduling': 'schedule',
  '/dispatcher/failures': 'alert',
  '/drones': 'drone',
  '/stations': 'landing-pad',
  '/station-availability': 'activity',
  '/station-slots': 'grid',
  '/manager/analytics': 'chart',
}

export function AccountLayout() {
  const auth = useAuth()
  const { pathname } = useLocation()
  const motionRef = useAccountMotion(pathname)
  const role = auth.user?.role.code
  const isAdmin = role === 'ADMIN'
  const isCustomer = role === 'CUSTOMER'
  const hasWorkspace = isAdmin || role === 'DISPATCHER'
  const [unreadNotifications, setUnreadNotifications] = useState(0)

  useEffect(() => {
    if (!isCustomer) return
    let active = true
    const fetchCount = () => {
      customerNotificationApi.unreadCount()
        .then(count => { if (active) setUnreadNotifications(count) })
        .catch(() => {})
    }
    const handleVisibility = () => {
      if (document.visibilityState === 'visible') {
        fetchCount()
      }
    }
    fetchCount()
    const interval = setInterval(fetchCount, 60000)
    window.addEventListener('focus', fetchCount)
    document.addEventListener('visibilitychange', handleVisibility)
    return () => {
      active = false
      clearInterval(interval)
      window.removeEventListener('focus', fetchCount)
      document.removeEventListener('visibilitychange', handleVisibility)
    }
  }, [isCustomer, pathname])

  const managedOrderId = pathname.match(/^\/orders\/([^/]+)\/(?:edit|manage)$/)?.[1]
  const initials = auth.user?.fullName.trim().split(/\s+/).slice(0, 2).map(part => part[0]).join('').toUpperCase() || 'SD'
  const operation = operationFeatures.find(feature => pathname === feature.path || pathname.startsWith(feature.path + '/'))
  const section = operation ? operation.label : pathname === '/' ? 'Workspace' : pathname.startsWith('/orders') ? 'Orders' : pathname.startsWith('/notifications') ? 'Notifications' : pathname.startsWith('/admin/roles') ? 'Roles' : pathname.startsWith('/admin/audit-logs') ? 'Audit logs' : pathname.startsWith('/admin/users') ? 'Users' : pathname.startsWith('/addresses') ? 'Addresses' : pathname.startsWith('/packages') ? 'Packages' : 'My profile'
  const sidebarContent = <>
      <Link to={hasWorkspace || role === 'MANAGER' ? workspaceDestination(role) : '/profile'} className="account-brand"><img className="account-brand-logo" src={brandLogo} alt="" width="40" height="40" /><span>SmartDrone<small>Delivery operations</small></span></Link>
      <nav aria-label="Account navigation">
        <span className="account-nav-marker" aria-hidden="true" />
        {hasWorkspace && <NavLink to="/" end className={({ isActive }) => isActive ? 'account-current' : 'account-nav-link'}><AccountIcon name="home"/>Workspace</NavLink>}
        {isCustomer && <div className="account-nav-group"><span className="account-nav-label">My deliveries</span>
          <NavLink to="/orders" end={pathname === '/orders/new' || !!managedOrderId} className={({ isActive }) => isActive ? 'account-current' : 'account-nav-link'}><AccountIcon name="orders"/>My orders</NavLink>
          {managedOrderId && <NavLink to={`/orders/${managedOrderId}/manage`} className={({ isActive }) => isActive ? 'account-current' : 'account-nav-link'}><AccountIcon name="edit-order"/>Manage this order</NavLink>}
          <NavLink to="/orders/new" className={({ isActive }) => isActive ? 'account-current' : 'account-nav-link'}><AccountIcon name="create-order"/>Create order</NavLink>
          <NavLink to="/notifications" className={({ isActive }) => isActive ? 'account-current' : 'account-nav-link'}><AccountIcon name="notifications"/>Notifications{unreadNotifications > 0 && <Badge count={unreadNotifications} size="small" className="account-nav-unread" />}</NavLink>
          <NavLink to="/addresses" className={({ isActive }) => isActive ? 'account-current' : 'account-nav-link'}><AccountIcon name="addresses"/>Addresses</NavLink>
          <NavLink to="/packages" className={({ isActive }) => isActive ? 'account-current' : 'account-nav-link'}><AccountIcon name="packages"/>Packages</NavLink>
        </div>}
        {isAdmin && <div className="account-nav-group"><span className="account-nav-label">Administration</span>
          <NavLink to="/admin/users" className={({ isActive }) => isActive ? 'account-current' : 'account-nav-link'}><AccountIcon name="users"/>Users</NavLink>
          <NavLink to="/admin/roles" className={({ isActive }) => isActive ? 'account-current' : 'account-nav-link'}><AccountIcon name="roles"/>Roles</NavLink>
          <NavLink to="/admin/audit-logs" className={({ isActive }) => isActive ? 'account-current' : 'account-nav-link'}><AccountIcon name="audit"/>Audit logs</NavLink>
        </div>}
        {operationNavigation(role).map(group => (
          <div key={group.label} className="account-nav-group">
            <span className="account-nav-label">{group.label}</span>
            {group.features.map(feature => <NavLink key={feature.path} to={feature.path} className={({ isActive }) => isActive ? 'account-current' : 'account-nav-link'}>
              <AccountIcon name={operationIcons[feature.path] ?? 'grid'}/>{feature.label}
            </NavLink>)}
          </div>
        ))}
      </nav>
      <div className="account-sidebar-bottom">
        <div className="account-workspace-role">{auth.user?.role.name}</div>
        <NavLink to="/profile" className={({ isActive }) => isActive ? 'account-current' : 'account-nav-link'}><AccountIcon name="profile"/>My profile</NavLink>
        <Button type="text" className="account-sign-out" icon={<AccountIcon name="logout"/>} onClick={() => void auth.logout()}>Sign out</Button>
      </div>
  </>
  return <ConfigProvider theme={{ token: {
    colorLink: '#4a704b', colorLinkHover: '#1e4f3c', colorLinkActive: '#163f2f',
    colorPrimaryHover: '#163f2f', colorPrimaryActive: '#163f2f',
    controlItemBgHover: '#f1f6eb', controlItemBgActiveHover: '#e6eedc',
    fontSize: 14, fontWeightStrong: 650, colorTextPlaceholder: '#65776b',
  } }}><div className="account-layout" ref={motionRef}>
    <aside className="account-sidebar">
      {sidebarContent}
    </aside>
    <div className="account-workspace"><header className="account-header">
      <AccountMobileNavigation key={pathname}>{sidebarContent}</AccountMobileNavigation>
      <div className="account-breadcrumb"><span>{pathname.startsWith('/admin/') ? 'Administration' : (pathname.startsWith('/addresses') || pathname.startsWith('/packages')) ? 'Delivery details' : 'Account'}</span><span aria-hidden="true">/</span><strong>{section}</strong></div>
      <div className="account-user">
        {isCustomer && (
          <Link to="/notifications" className="account-bell-link" aria-label={`Notifications: ${unreadNotifications} unread`} style={{ marginRight: 16, display: 'inline-flex', alignItems: 'center' }}>
            <Badge count={unreadNotifications} size="small" offset={[-2, 4]}>
              <Button shape="circle" icon={<AccountIcon name="notifications" />} aria-label="Notifications" />
            </Badge>
          </Link>
        )}
        <span className="account-role-badge"><span className="account-role-symbol"><AccountIcon name="roles"/></span>{auth.user?.role.name}</span>
        <Link className="account-avatar-link" to="/profile" aria-label={`My profile: ${auth.user?.fullName}`}><Avatar size={38} src={auth.user?.avatarUrl || undefined} alt="Your profile photo">{initials}</Avatar></Link>
      </div>
    </header><Outlet /></div>
  </div></ConfigProvider>
}
