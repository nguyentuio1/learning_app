import { Route, Routes } from 'react-router-dom'
import { HomePage, NotFoundPage } from '../pages'

export function AppRouter() {
  return (
    <Routes>
      <Route path="/" element={<HomePage />} />
      <Route path="*" element={<NotFoundPage />} />
    </Routes>
  )
}
