import { Route, Routes } from 'react-router-dom'
import { HomePage, LoginPage, NotFoundPage } from '../pages'
import { RequireAuth } from './RequireAuth'
import { managementRoles } from '../types/auth'

export function AppRouter() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route element={<RequireAuth roles={managementRoles} />}>
        <Route path="/" element={<HomePage />} />
      </Route>
      <Route path="*" element={<NotFoundPage />} />
    </Routes>
  )
}
