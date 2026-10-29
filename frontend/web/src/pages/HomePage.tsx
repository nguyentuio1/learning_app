import { Navigate } from 'react-router-dom'
import { useAuth } from '../hooks/useAuth'

export function HomePage() {
  const { user } = useAuth()
  return <Navigate to={user?.role.code === 'ADMIN' ? '/admin/users' : '/profile'} replace />
}
