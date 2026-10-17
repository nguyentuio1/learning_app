import { lazy, Suspense } from 'react'
import { Route, Routes } from 'react-router-dom'
import { HomePage } from '../pages/HomePage'
import { RequireAuth } from './RequireAuth'
import { allRoles, managementRoles } from '../types/auth'

const LoginPage = lazy(() => import('../pages/LoginPage').then(module => ({ default: module.LoginPage })))
const NotFoundPage = lazy(() => import('../pages/NotFoundPage').then(module => ({ default: module.NotFoundPage })))
const ProfilePage = lazy(() => import('../pages/ProfilePage').then(module => ({ default: module.ProfilePage })))
const AccountLayout = lazy(() => import('../components/layouts/AccountLayout').then(module => ({ default: module.AccountLayout })))
const UsersPage = lazy(() => import('../pages/UsersPage').then(module => ({ default: module.UsersPage })))
const CreateUserPage = lazy(() => import('../pages/CreateUserPage').then(module => ({ default: module.CreateUserPage })))
const UserDetailPage = lazy(() => import('../pages/UserDetailPage').then(module => ({ default: module.UserDetailPage })))
const RolesPage = lazy(() => import('../pages/RolesPage').then(module => ({ default: module.RolesPage })))

export function AppRouter() {
  return (
    <Suspense fallback={<div className="page-shell" role="status" aria-live="polite">Loading page…</div>}>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route element={<RequireAuth roles={['ADMIN']} />}>
          <Route element={<AccountLayout />}>
            <Route path="/admin/users" element={<UsersPage />} />
            <Route path="/admin/users/new" element={<CreateUserPage />} />
            <Route path="/admin/users/:id" element={<UserDetailPage />} />
            <Route path="/admin/roles" element={<RolesPage />} />
          </Route>
        </Route>
        <Route element={<RequireAuth roles={allRoles} />}>
          <Route element={<AccountLayout />}>
            <Route path="/profile" element={<ProfilePage />} />
          </Route>
        </Route>
        <Route element={<RequireAuth roles={managementRoles} />}>
          <Route path="/" element={<HomePage />} />
        </Route>
        <Route path="*" element={<NotFoundPage />} />
      </Routes>
    </Suspense>
  )
}
