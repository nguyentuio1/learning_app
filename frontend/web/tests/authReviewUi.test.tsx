import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { act, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { ContextType } from 'react'
import { ConfigProvider } from 'antd'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import MockAdapter from 'axios-mock-adapter'
import { AuthContext } from '../src/contexts/authContext'
import { LoginPage } from '../src/pages/LoginPage'
import { PasswordSettings } from '../src/components/profile/ProfileForms'
import { RequireAuth } from '../src/routes/RequireAuth'
import { managementRoles, type Role } from '../src/types/auth'
import { apiClient } from '../src/services/apiClient'

vi.mock('../src/components/auth/LoginIllustration', () => ({ LoginIllustration: () => null }))

const login = vi.fn()
const logout = vi.fn()
const onAccessError = vi.fn()
let mock: MockAdapter
beforeEach(() => {
  login.mockReset().mockResolvedValue(undefined)
  logout.mockReset().mockResolvedValue(undefined)
  onAccessError.mockReset()
  mock = new MockAdapter(apiClient)
})
afterEach(() => mock.restore())

function authValue(role: Role | null): NonNullable<ContextType<typeof AuthContext>> {
  return {
    status: role ? 'authenticated' : 'anonymous',
    user: role ? {
      id: '11111111-1111-1111-1111-111111111111', email: 'user@example.com',
      fullName: 'Test User', phoneNumber: '0901234567', avatarUrl: null, status: 'ACTIVE',
      role: { id: 5, code: role, name: role }, createdAt: '2026-10-20T00:00:00Z',
    } : null,
    accessToken: role ? 'token' : null, refreshToken: role ? 'refresh' : null,
    generation: 0, message: '', login, logout, updateProfile: vi.fn(),
  }
}
function loginApp(role: Role | null, from?: string) {
  return <ConfigProvider theme={{ token: { motion: false } }}>
    <AuthContext.Provider value={authValue(role)}>
      <MemoryRouter initialEntries={[{ pathname: '/login', state: { from } }]}><Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route element={<RequireAuth roles={managementRoles} />}>
          <Route path="/" element={<h1>Management home</h1>} />
        </Route>
        <Route path="/profile" element={<h1>Profile destination</h1>} />
        <Route path="/admin/users" element={<h1>Admin destination</h1>} />
      </Routes></MemoryRouter>
    </AuthContext.Provider>
  </ConfigProvider>
}

it.each(['CUSTOMER', 'STATION_OPERATOR'] as const)('shows mobile guidance after %s signs in, including a saved profile destination', async role => {
  render(loginApp(role, '/profile'))
  expect(await screen.findByText('Please use the mobile app')).toBeVisible()
  expect(screen.queryByText('Profile destination')).not.toBeInTheDocument()
})
it.each(['DISPATCHER', 'MANAGER'] as const)('keeps the profile destination for %s', async role => {
  render(loginApp(role))
  expect(await screen.findByRole('heading', { name: 'Profile destination' })).toBeVisible()
})
it('retains the admin destination and rejects external redirect targets', async () => {
  render(loginApp('ADMIN', 'https://example.com/elsewhere'))
  expect(await screen.findByRole('heading', { name: 'Admin destination' })).toBeVisible()
})
it('blocks repeated sign-in while pending and redirects after the login transition', async () => {
  let finishLogin!: () => void
  login.mockImplementation(() => new Promise<void>(resolve => { finishLogin = resolve }))
  const view = render(loginApp(null))
  const user = userEvent.setup()
  await user.type(screen.getByLabelText('Email'), 'user@example.com')
  await user.type(screen.getByLabelText('Password'), 'Example123!')
  await user.dblClick(screen.getByRole('button', { name: 'Sign in' }))
  await waitFor(() => expect(login).toHaveBeenCalledTimes(1))
  expect(screen.getByLabelText('Email')).toBeDisabled()
  await act(async () => { view.rerender(loginApp('ADMIN')); finishLogin() })
  expect(await screen.findByRole('heading', { name: 'Admin destination' })).toBeVisible()
})
it('allows another sign-in attempt after login fails', async () => {
  login.mockRejectedValue(new Error('Unable to sign in.'))
  render(loginApp(null))
  const user = userEvent.setup()
  await user.type(screen.getByLabelText('Email'), 'user@example.com')
  await user.type(screen.getByLabelText('Password'), 'Example123!')
  await user.click(screen.getByRole('button', { name: 'Sign in' }))
  expect(await screen.findByText('Unable to sign in.')).toBeVisible()
  expect(screen.getByLabelText('Email')).toBeEnabled()
  expect(screen.getByLabelText('Password')).toHaveValue('')
  await user.type(screen.getByLabelText('Password'), 'Example456!')
  await user.click(screen.getByRole('button', { name: 'Sign in' }))
  await waitFor(() => expect(login).toHaveBeenCalledTimes(2))
})

async function submitPassword() {
  render(<ConfigProvider theme={{ token: { motion: false } }}>
    <AuthContext.Provider value={authValue('ADMIN')}>
      <PasswordSettings onAccessError={onAccessError} />
    </AuthContext.Provider>
  </ConfigProvider>)
  const user = userEvent.setup()
  await user.type(screen.getByLabelText('Current password'), 'Current123!')
  await user.type(screen.getByLabelText('New password'), 'Changed123!')
  await user.type(screen.getByLabelText('Confirm new password'), 'Changed123!')
  await user.click(screen.getByRole('button', { name: 'Change password' }))
}
it('signs out only after the password API confirms success', async () => {
  let finishChange!: (response: [number]) => void
  mock.onPut('/api/users/me/password').reply(() => new Promise(resolve => { finishChange = resolve }))
  await submitPassword()
  await waitFor(() => expect(mock.history.put).toHaveLength(1))
  expect(logout).not.toHaveBeenCalled()
  await act(async () => finishChange([204]))
  await waitFor(() => expect(logout).toHaveBeenCalledTimes(1))
  expect(onAccessError).not.toHaveBeenCalled()
})
it('shows the wrong-current-password 401 without signing out or treating it as lost access', async () => {
  mock.onPut('/api/users/me/password').reply(401, { detail: 'Current password is incorrect.' })
  await submitPassword()
  await waitFor(() => expect(screen.getAllByText('Current password is incorrect.').length).toBeGreaterThan(0))
  expect(logout).not.toHaveBeenCalled()
  expect(onAccessError).not.toHaveBeenCalled()
  expect(screen.getByLabelText('Current password')).toHaveValue('Current123!')
})
