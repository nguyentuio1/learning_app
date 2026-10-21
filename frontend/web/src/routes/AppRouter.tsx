import { Route, Routes } from 'react-router-dom'
import { HomePage, LoginPage, NotFoundPage, ProfilePage } from '../pages'
import { RequireAuth } from './RequireAuth'
import { allRoles, managementRoles } from '../types/auth'
import { AccountLayout } from '../components/layouts/AccountLayout'

export function AppRouter() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
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
