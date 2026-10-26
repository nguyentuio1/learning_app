import { afterAll, afterEach, beforeAll, beforeEach, expect, it, vi } from 'vitest'
import { cleanup, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { ConfigProvider } from 'antd'
import { MemoryRouter } from 'react-router-dom'
import axios from 'axios'
import MockAdapter from 'axios-mock-adapter'
import type { AuthResponse, Role } from '../src/types/auth'

// Artwork is tested separately; keep the real routes, provider, session and API interceptors.
vi.mock('../src/components/auth/LoginIllustration', () => ({ LoginIllustration: () => null }))

let session: typeof import('../src/services/authSession').session
let AuthProvider: typeof import('../src/contexts/AuthProvider').AuthProvider
let AppRouter: typeof import('../src/routes/AppRouter').AppRouter
let mock: MockAdapter
const userId = '11111111-1111-1111-1111-111111111111'
function credentials(role: Role = 'ADMIN'): AuthResponse {
  return {
    accessToken: 'test-access', refreshToken: 'test-refresh', expiresIn: 900,
    user: {
      id: userId, fullName: 'Test Administrator', email: 'admin@example.com',
      phoneNumber: '0901234567', status: 'ACTIVE', avatarUrl: null,
      createdAt: '2026-10-20T00:00:00Z', role: { id: 5, code: role, name: role },
    },
  }
}
beforeAll(async () => {
  sessionStorage.clear()
  // Install before importing the application, so its private auth transport is intercepted too.
  mock = new MockAdapter(axios)
  ;({ session } = await import('../src/services/authSession'))
  ;({ AuthProvider } = await import('../src/contexts/AuthProvider'))
  ;({ AppRouter } = await import('../src/routes/AppRouter'))
  await session.initialize()
})
beforeEach(() => {
  mock.reset()
  session.clear()
  mock.onPost('/api/auth/login').reply(200, credentials())
  mock.onPost('/api/auth/logout').reply(204)
  mock.onGet('/api/users/me').reply(200, credentials().user)
  mock.onGet('/api/users').reply(200, [{
    ...credentials().user, role: 'System Administrator', roleCode: 'ADMIN',
  }])
})
afterEach(() => { cleanup(); session.clear() })
afterAll(() => mock.restore())

function open(path: string) {
  return render(<ConfigProvider theme={{ token: { motion: false } }}>
    <MemoryRouter initialEntries={[path]}><AuthProvider><AppRouter /></AuthProvider></MemoryRouter>
  </ConfigProvider>)
}
async function signIn(role: Role = 'ADMIN') {
  mock.onPost('/api/auth/login').reply(200, credentials(role))
  await session.login('admin@example.com', 'Example123!')
}

it.each(['/admin/users', '/admin/users/new', `/admin/users/${userId}`, '/admin/roles', '/profile'])(
  'redirects anonymous direct navigation to %s without loading private data', async path => {
    open(path)
    expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeVisible()
    expect(mock.history.get).toHaveLength(0)
  },
)
it('signs in through the real provider, loads users with a bearer token, and navigates to roles', async () => {
  open('/admin/users')
  const user = userEvent.setup()
  await user.type(await screen.findByLabelText('Email'), 'admin@example.com')
  await user.type(screen.getByLabelText('Password'), 'Example123!')
  await user.click(screen.getByRole('button', { name: 'Sign in' }))
  expect(await screen.findByRole('link', { name: 'View Test Administrator' })).toBeVisible()
  expect(mock.history.get.find(request => request.url === '/api/users')?.headers?.Authorization).toBe('Bearer test-access')
  expect(sessionStorage.getItem('smartdrone.refreshToken')).toBe('test-refresh')
  expect(sessionStorage.getItem('smartdrone.accessToken')).toBeNull()
  await user.click(screen.getByRole('link', { name: 'Roles' }))
  expect(await screen.findByRole('heading', { name: 'Roles', exact: true })).toBeVisible()
  await user.click(screen.getByRole('button', { name: 'Sign out' }))
  expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeVisible()
  expect(session.getSnapshot()).toMatchObject({ status: 'anonymous', accessToken: null, refreshToken: null })
  expect(sessionStorage.getItem('smartdrone.refreshToken')).toBeNull()
})
it.each(['DISPATCHER', 'MANAGER'] as const)('blocks %s on the actual admin route without fetching users', async role => {
  await signIn(role)
  open('/admin/users')
  expect(await screen.findByText('Access denied')).toBeVisible()
  expect(mock.history.get).toHaveLength(0)
})
it.each(['CUSTOMER', 'STATION_OPERATOR'] as const)('shows mobile guidance to %s through the actual login route', async role => {
  await signIn(role)
  open('/login')
  expect(await screen.findByText('Please use the mobile app')).toBeVisible()
  expect(mock.history.get).toHaveLength(0)
})
it('returns to login when renewal fails while loading a protected page', async () => {
  await signIn()
  mock.onGet('/api/users').reply(401)
  mock.onPost('/api/auth/refresh').reply(401)
  open('/admin/users')
  expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeVisible()
  expect(session.getSnapshot().status).toBe('anonymous')
  expect(sessionStorage.getItem('smartdrone.refreshToken')).toBeNull()
  expect(mock.history.post.filter(request => request.url === '/api/auth/refresh')).toHaveLength(1)
})
it('keeps the session but shows an access error when an admin API rejects the account with 403', async () => {
  await signIn()
  mock.onGet('/api/users').reply(403)
  open('/admin/users')
  expect(await screen.findByText('Administrator access is required, or your account is no longer active.')).toBeVisible()
  expect(session.getSnapshot().status).toBe('authenticated')
  expect(mock.history.post.filter(request => request.url === '/api/auth/refresh')).toHaveLength(0)
})

async function changePassword() {
  await signIn()
  open('/profile')
  const user = userEvent.setup()
  await user.type(await screen.findByLabelText('Current password'), 'Example123!')
  await user.type(screen.getByLabelText('New password'), 'Updated456!')
  await user.type(screen.getByLabelText('Confirm new password'), 'Updated456!')
  await user.click(screen.getByRole('button', { name: 'Change password' }))
}
it('clears the real session and redirects after a successful password change', async () => {
  mock.onPut('/api/users/me/password').reply(204)
  await changePassword()
  expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeVisible()
  expect(session.getSnapshot()).toMatchObject({ status: 'anonymous', accessToken: null, refreshToken: null })
  expect(sessionStorage.getItem('smartdrone.refreshToken')).toBeNull()
  expect(mock.history.post.filter(request => request.url === '/api/auth/logout')).toHaveLength(1)
})
it('still clears local credentials if logout cannot confirm revocation after changing the password', async () => {
  mock.onPut('/api/users/me/password').reply(204)
  mock.onPost('/api/auth/logout').networkError()
  await changePassword()
  expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeVisible()
  expect(sessionStorage.getItem('smartdrone.refreshToken')).toBeNull()
  expect(session.getSnapshot().accessToken).toBeNull()
})
it('stays on the actual profile after an incorrect-password 401, without refresh or logout', async () => {
  mock.onPut('/api/users/me/password').reply(401, { detail: 'Current password is incorrect.' })
  await changePassword()
  await waitFor(() => expect(screen.getAllByText('Current password is incorrect.').length).toBeGreaterThan(0))
  expect(screen.getByRole('heading', { name: 'My profile' })).toBeVisible()
  expect(session.getSnapshot().status).toBe('authenticated')
  expect(mock.history.post.map(request => request.url)).toEqual(['/api/auth/login'])
})
