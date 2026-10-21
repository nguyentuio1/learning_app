import { act, render, screen, waitFor } from '@testing-library/react'
import type { ContextType } from 'react'
import { MemoryRouter } from 'react-router-dom'
import { expect, it, vi } from 'vitest'
import { AuthContext } from '../src/contexts/authContext'
import { AppRouter } from '../src/routes/AppRouter'
import type { Role } from '../src/types/auth'

const deferredPage = vi.hoisted(() => {
  let resolve!: () => void
  const ready = new Promise<void>(done => { resolve = done })
  return { ready, resolve, requested: vi.fn() }
})

vi.mock('../src/pages/LoginPage', () => ({
  LoginPage: () => <h1>Sign in</h1>,
}))
vi.mock('../src/components/layouts/AccountLayout', async () => {
  const { Outlet } = await import('react-router-dom')
  return { AccountLayout: () => <Outlet /> }
})
vi.mock('../src/pages/UsersPage', async () => {
  deferredPage.requested()
  await deferredPage.ready
  return { UsersPage: () => <h1>User management loaded</h1> }
})

function app(role: Role | null) {
  const value: NonNullable<ContextType<typeof AuthContext>> = {
    status: role ? 'authenticated' : 'anonymous',
    user: role ? {
      id: 'test-user', fullName: 'Test User', email: 'test@example.com',
      phoneNumber: '0901234567', avatarUrl: null, status: 'ACTIVE',
      role: { id: 5, code: role, name: role }, createdAt: '2026-10-20T00:00:00Z',
    } : null,
    accessToken: null, refreshToken: null, generation: 0, message: '',
    login: vi.fn(), logout: vi.fn(), updateProfile: vi.fn(),
  }
  return <AuthContext.Provider value={value}>
    <MemoryRouter initialEntries={['/admin/users']}><AppRouter /></MemoryRouter>
  </AuthContext.Provider>
}

it('loads a protected page only after authorization and announces a pending chunk', async () => {
  const anonymous = render(app(null))
  expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeVisible()
  expect(deferredPage.requested).not.toHaveBeenCalled()
  anonymous.unmount()

  const authenticated = render(app('DISPATCHER'))
  expect(await screen.findByText('Access denied')).toBeVisible()
  expect(deferredPage.requested).not.toHaveBeenCalled()

  authenticated.rerender(app('ADMIN'))
  await waitFor(() => expect(deferredPage.requested).toHaveBeenCalledTimes(1))
  expect(screen.getByRole('status')).toHaveTextContent('Loading page')
  expect(screen.queryByRole('heading', { name: 'User management loaded' })).not.toBeInTheDocument()

  await act(async () => deferredPage.resolve())
  expect(await screen.findByRole('heading', { name: 'User management loaded' })).toBeVisible()
  expect(screen.queryByText('Loading page…')).not.toBeInTheDocument()
})
