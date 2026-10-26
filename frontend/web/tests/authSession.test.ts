import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import axios from 'axios'
import MockAdapter from 'axios-mock-adapter'
import type { AuthResponse } from '../src/types/auth'

const signedIn: AuthResponse = {
  accessToken: 'old-token', refreshToken: 'old-refresh', expiresIn: 900,
  user: {
    id: '11111111-1111-1111-1111-111111111111', email: 'admin@example.com',
    fullName: 'Test Admin', phoneNumber: '0901234567', avatarUrl: null, status: 'ACTIVE',
    role: { id: 5, code: 'ADMIN', name: 'System Administrator' }, createdAt: '2026-10-20T00:00:00Z',
  },
}
const renewed: AuthResponse = { ...signedIn, accessToken: 'new-token', refreshToken: 'new-refresh' }
let mock: MockAdapter
let session: typeof import('../src/services/authSession').session
let apiClient: typeof import('../src/services/apiClient').apiClient

beforeEach(async () => {
  vi.resetModules()
  sessionStorage.clear()
  // New Axios instances inherit this test adapter, including the private auth transport.
  mock = new MockAdapter(axios)
  ;({ session } = await import('../src/services/authSession'))
  ;({ apiClient } = await import('../src/services/apiClient'))
  mock.onPost('/api/auth/login').reply(200, signedIn)
  await session.login('admin@example.com', 'Example123!')
})
afterEach(() => {
  session.clear()
  mock.restore()
})

it('keeps the session and does not refresh after an incorrect-current-password 401', async () => {
  mock.onPut('/api/users/me/password').reply(401, { detail: 'Current password is incorrect.' })
  const before = session.getSnapshot()
  const { profileApi } = await import('../src/services/profileApi')
  await expect(profileApi.changePassword({ currentPassword: 'Wrong123!', newPassword: 'NewPass123!' }))
    .rejects.toMatchObject({ response: { status: 401, data: { detail: 'Current password is incorrect.' } } })
  expect(session.getSnapshot()).toBe(before)
  expect(mock.history.put).toHaveLength(1)
  expect(mock.history.post.map(request => request.url)).toEqual(['/api/auth/login'])
})

it('shares one refresh request across simultaneous expired-token responses and retries both', async () => {
  let finishRefresh!: (response: [number, AuthResponse]) => void
  mock.onPost('/api/auth/refresh').reply(() => new Promise(resolve => { finishRefresh = resolve }))
  for (const path of ['/api/users', `/api/users/${signedIn.user.id}`]) {
    mock.onGet(path).reply(config => config.headers?.Authorization === 'Bearer new-token'
      ? [200, { ok: true }] : [401])
  }
  const requests = Promise.all([apiClient.get('/api/users'), apiClient.get(`/api/users/${signedIn.user.id}`)])
  await vi.waitFor(() => {
    expect(mock.history.get).toHaveLength(2)
    expect(mock.history.post.filter(request => request.url === '/api/auth/refresh')).toHaveLength(1)
  })
  finishRefresh([200, renewed])
  expect((await requests).map(response => response.status)).toEqual([200, 200])
  expect(mock.history.post.filter(request => request.url === '/api/auth/refresh')).toHaveLength(1)
  expect(mock.history.get).toHaveLength(4)
  expect(session.getSnapshot()).toMatchObject({ status: 'authenticated', accessToken: 'new-token', refreshToken: 'new-refresh' })
})

it('reuses a renewed token when a parallel request returns its old-token 401 late', async () => {
  let finishLate!: (response: [number]) => void
  mock.onGet('/api/users/late').replyOnce(() => new Promise(resolve => { finishLate = resolve }))
  mock.onGet('/api/users/late').reply(config => {
    expect(config.headers?.Authorization).toBe('Bearer new-token')
    return [200, []]
  })
  mock.onGet('/api/users').replyOnce(401)
  mock.onGet('/api/users').reply(200, [])
  mock.onPost('/api/auth/refresh').reply(200, renewed)
  const late = apiClient.get('/api/users/late')
  await vi.waitFor(() => expect(mock.history.get).toHaveLength(1))
  await apiClient.get('/api/users')
  finishLate([401])
  await expect(late).resolves.toMatchObject({ status: 200 })
  expect(mock.history.post.filter(request => request.url === '/api/auth/refresh')).toHaveLength(1)
})

it('does not refresh or clear an authenticated session when an admin API returns 403', async () => {
  mock.onGet('/api/users').reply(403)
  const before = session.getSnapshot()
  await expect(apiClient.get('/api/users')).rejects.toMatchObject({ response: { status: 403 } })
  expect(session.getSnapshot()).toBe(before)
  expect(mock.history.get).toHaveLength(1)
  expect(mock.history.post.map(request => request.url)).toEqual(['/api/auth/login'])
})
