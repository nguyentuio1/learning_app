import { beforeEach, afterEach, expect, it, vi } from 'vitest'
import MockAdapter from 'axios-mock-adapter'

const auth = vi.hoisted(() => ({
  snapshot: { accessToken: 'old-token', refreshToken: 'refresh', generation: 1 },
  refresh: vi.fn(),
  clear: vi.fn(),
}))
vi.mock('../src/services/authSession', () => ({
  apiBaseURL: '/', session: { getSnapshot: () => auth.snapshot, refresh: auth.refresh, clear: auth.clear },
}))
import { apiClient } from '../src/services/apiClient'
import { userManagementApi } from '../src/services/userManagementApi'
let mock: MockAdapter
beforeEach(() => {
  auth.snapshot = { accessToken: 'old-token', refreshToken: 'refresh', generation: 1 }
  auth.refresh.mockReset(); auth.clear.mockReset()
  mock = new MockAdapter(apiClient)
})
afterEach(() => mock.restore())
it('attaches the session bearer token to user-management requests', async () => {
  mock.onGet('/api/users').reply(config => {
    expect(config.headers?.Authorization).toBe('Bearer old-token')
    return [200, []]
  })
  await userManagementApi.list()
})
it('renews an expired token and retries the failed request once', async () => {
  mock.onGet('/api/users').replyOnce(401)
  auth.refresh.mockImplementation(async () => { auth.snapshot.accessToken = 'new-token'; return 'new-token' })
  mock.onGet('/api/users').reply(config => {
    expect(config.headers?.Authorization).toBe('Bearer new-token')
    return [200, []]
  })
  await expect(userManagementApi.list()).resolves.toEqual([])
  expect(auth.refresh).toHaveBeenCalledTimes(1)
  expect(mock.history.get).toHaveLength(2)
})
it('does not retry a forbidden operation', async () => {
  mock.onPut('/api/users/id/role').reply(403)
  await expect(userManagementApi.assignRole('id', 'MANAGER')).rejects.toMatchObject({ response: { status: 403 } })
  expect(auth.refresh).not.toHaveBeenCalled()
  expect(mock.history.put).toHaveLength(1)
})
it('clears the session after a retried request is still unauthorized', async () => {
  mock.onGet('/api/users').reply(401)
  auth.refresh.mockResolvedValue('old-token')
  await expect(userManagementApi.list()).rejects.toMatchObject({ response: { status: 401 } })
  expect(auth.refresh).toHaveBeenCalledTimes(1)
  expect(auth.clear).toHaveBeenCalledTimes(1)
  expect(mock.history.get).toHaveLength(2)
})
it('discards a response from a previous session', async () => {
  mock.onGet('/api/users').reply(() => {
    auth.snapshot.generation++
    return [200, [{ id: 'private-account' }]]
  })
  await expect(userManagementApi.list()).rejects.toMatchObject({ code: 'ERR_CANCELED' })
})
