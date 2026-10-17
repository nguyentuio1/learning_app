import { Route, Routes } from 'react-router-dom'
import { HomePage, LoginPage, NotFoundPage, ProfilePage } from '../pages'
import { RequireAuth } from './RequireAuth'
import { allRoles, managementRoles } from '../types/auth'
import { AccountLayout } from '../components/layouts/AccountLayout'
import { UsersPage } from '../pages/UsersPage'
import { CreateUserPage } from '../pages/CreateUserPage'
import { UserDetailPage } from '../pages/UserDetailPage'
import { RolesPage } from '../pages/RolesPage'

export function AppRouter() {
  return (
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
  )
}
