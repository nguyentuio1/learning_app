import { Button, Result, Spin } from 'antd'
import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { useAuth } from '../hooks/useAuth'

import type { Role } from '../types/auth'

export function RequireAuth({ roles }: { roles: readonly Role[] }) {
  const auth = useAuth()
  const location = useLocation()
  if (auth.status === 'loading') return <div className="page-shell" role="status"><Spin tip="Restoring session"><div style={{ padding: 32 }} /></Spin></div>
  if (!auth.user || auth.status !== 'authenticated') {
    return <Navigate to="/login" state={{ from: location.pathname }} replace />
  }
  if (!roles.some(role => role === auth.user?.role.code)) {
    if (auth.user.role.code === 'CUSTOMER' || auth.user.role.code === 'STATION_OPERATOR') {
      return <Result status="info" title="Please use the mobile app"
        subTitle="Use the SmartDroneDelivery mobile app to access Customer and Station Operator features."
        extra={<Button onClick={() => void auth.logout()}>Sign out</Button>} />
    }
    return <Result status="403" title="Access denied" subTitle="Your role cannot access this page."
      extra={<Button onClick={() => void auth.logout()}>Sign out</Button>} />
  }
  return <Outlet key={`${auth.generation}:${auth.user.id}`} />
}
