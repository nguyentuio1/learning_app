import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { apiClient } from '../src/services/apiClient'
import { session } from '../src/services/authSession'
import { userManagementApi } from '../src/services/userManagementApi'
import { filterUsers, userManagementError } from '../src/utils/userManagement'
import type { ManagedUser } from '../src/types/userManagement'
const user: ManagedUser = { id: 'abc', email: 'a@example.com', fullName: 'An Nguyen', phoneNumber: '0901234567', role: 'Dispatcher', roleCode: 'DISPATCHER', status: 'ACTIVE', avatarUrl: null, createdAt: '2026-10-17T08:00:00Z' }
let mock: MockAdapter
beforeEach(() => { session.clear(); mock = new MockAdapter(apiClient) })
afterEach(() => mock.restore())
describe('LA-93 API contract', () => {
  it('uses the real collection and detail paths and flat role response', async () => {
    mock.onGet('/api/users').reply(200, [user])
    mock.onGet('/api/users/abc').reply(200, user)
    expect(await userManagementApi.list()).toEqual([user])
    expect((await userManagementApi.get('abc')).roleCode).toBe('DISPATCHER')
  })
  it('normalizes details, preserves passwords and does not overpost create fields', async () => {
    mock.onPost('/api/users').reply(201, user)
    await userManagementApi.create({ email: ' A@EXAMPLE.COM ', fullName: ' An ', phoneNumber: '0901234567', role: 'DISPATCHER', password: 'Secret123 ' })
    expect(JSON.parse(mock.history.post[0].data)).toEqual({ email: 'a@example.com', fullName: 'An', phoneNumber: '0901234567', role: 'DISPATCHER', password: 'Secret123 ' })
  })
  it('keeps details, role and deactivation separate', async () => {
    mock.onPut('/api/users/abc').reply(200, user)
    mock.onPut('/api/users/abc/role').reply(204)
    mock.onPut('/api/users/abc/deactivate').reply(204)
    await userManagementApi.update('abc', { ...user, fullName: 'Updated' })
    await userManagementApi.assignRole('abc', 'MANAGER')
    await userManagementApi.deactivate('abc')
    expect(JSON.parse(mock.history.put[0].data)).toEqual({ fullName: 'Updated', email: user.email, phoneNumber: user.phoneNumber })
    expect(JSON.parse(mock.history.put[1].data)).toEqual({ role: 'MANAGER' })
    expect(mock.history.put[2].data).toBeUndefined()
  })
  it('supports cancellation without dispatching a request', async () => {
    const controller = new AbortController(); controller.abort()
    await expect(userManagementApi.list(controller.signal)).rejects.toMatchObject({ code: 'ERR_CANCELED' })
    expect(mock.history.get).toHaveLength(0)
  })
  it.each([400, 401, 403, 404, 409, 500])('handles HTTP %i without inventing success', async status => {
    mock.onGet('/api/users').reply(status, { detail: 'Email is already registered.', errors: { email: ['Invalid'], unsafe: ['Ignore'] } })
    try { await userManagementApi.list(); throw new Error('Expected rejection') } catch (error) {
      const problem = userManagementError(error)
      expect(problem.status).toBe(status)
      expect(problem.message.length).toBeGreaterThan(0)
      expect(problem.fields).not.toHaveProperty('unsafe')
      if (status === 409) expect(problem.fields.email).toEqual(['Email is already registered.'])
    }
  })
  it('handles network failure', async () => {
    mock.onGet('/api/users').networkError()
    await userManagementApi.list().catch(error => expect(userManagementError(error).message).toMatch(/Unable to reach/))
  })
})
describe('account filtering', () => {
  it('combines query, role and status using codes, not labels', () => {
    expect(filterUsers([user], ' NGUYEN ', 'DISPATCHER', 'ACTIVE')).toEqual([user])
    expect(filterUsers([user], '090123', '', '')).toEqual([user])
    expect(filterUsers([user], '', 'ADMIN', '')).toEqual([])
    expect(filterUsers([user], '', '', 'INACTIVE')).toEqual([])
  })
})
