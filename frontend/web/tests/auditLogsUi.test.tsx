import { beforeEach, afterEach, expect, it, vi } from 'vitest'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { ConfigProvider } from 'antd'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import MockAdapter from 'axios-mock-adapter'
import { AuthContext } from '../src/contexts/authContext'
import { RequireAuth } from '../src/routes/RequireAuth'
import { AuditLogsPage } from '../src/pages/AuditLogsPage'
import { apiClient } from '../src/services/apiClient'
import { session } from '../src/services/authSession'
import { auditLogQuery } from '../src/services/auditLogApi'
import type { AuditLogPage } from '../src/types/auditLog'

const userId = '22222222-2222-2222-2222-222222222222'
const page: AuditLogPage = {
  page: 1, pageSize: 20, totalCount: 2, totalPages: 1,
  items: [
    { id: 9, timestamp: '2026-11-17T03:04:05Z', userId, userEmail: 'cus@example.com', userFullName: 'Cus Tomer',
      action: 'LOGIN_FAILED', entityName: 'User', entityId: userId, ipAddress: '10.0.0.5', userAgent: 'UA', reason: 'INVALID_PASSWORD' },
    { id: 8, timestamp: '2026-11-17T02:00:00Z', userId: null, userEmail: null, userFullName: null,
      action: 'ORDER_PICKUP_TIMEOUT', entityName: 'DeliveryOrder', entityId: 'abcdef12-0000-0000-0000-000000000000', ipAddress: null, userAgent: null, reason: null },
  ],
}
let mock: MockAdapter
beforeEach(() => {
  session.clear()
  mock = new MockAdapter(apiClient)
  mock.onGet('/api/audit-logs/actions').reply(200, ['LOGIN', 'LOGIN_FAILED'])
  mock.onGet('/api/users').reply(200, [{ id: userId, fullName: 'Cus Tomer', email: 'cus@example.com', phoneNumber: '0900000000',
    role: 'Customer', roleCode: 'CUSTOMER', status: 'ACTIVE', avatarUrl: null, createdAt: '2026-10-17T08:00:00Z' }])
})
afterEach(() => mock.restore())

function show(path = '/admin/audit-logs', role = 'ADMIN') {
  const auth = { status: 'authenticated' as const,
    user: { id: 'admin-id', fullName: 'Admin', email: 'admin@example.com', phoneNumber: '', avatarUrl: null, role: { id: 5, code: role, name: role } },
    accessToken: 'test', refreshToken: 'r', generation: 0, message: '',
    login: vi.fn(), register: vi.fn(), logout: vi.fn(), updateProfile: vi.fn() }
  return render(<ConfigProvider theme={{ token: { motion: false } }}><AuthContext.Provider value={auth}>
    <MemoryRouter initialEntries={[path]}><Routes>
      <Route path="/" element={<h1>Home</h1>} />
      <Route element={<RequireAuth roles={['ADMIN']} />}>
        <Route path="/admin/audit-logs" element={<AuditLogsPage />} />
      </Route>
    </Routes></MemoryRouter>
  </AuthContext.Provider></ConfigProvider>)
}

it('turns calendar days into an inclusive UTC+07:00 range', () => {
  expect(auditLogQuery({ fromDate: '2026-11-10', toDate: '2026-11-11', action: 'LOGIN', page: 2, pageSize: 20 })).toEqual({
    page: '2', pageSize: '20', action: 'LOGIN', from: '2026-11-10T00:00:00+07:00', to: '2026-11-11T23:59:59.999+07:00',
  })
  expect(auditLogQuery({ page: 1, pageSize: 20 })).toEqual({ page: '1', pageSize: '20' })
})

it('lists events newest first with the actor, reason and Vietnam time', async () => {
  mock.onGet('/api/audit-logs').reply(200, page)
  show()
  const table = await screen.findByRole('table')
  const rows = within(table).getAllByRole('row').slice(1)
  expect(within(rows[0]).getByText('LOGIN_FAILED')).toBeVisible()
  expect(within(rows[0]).getByText('cus@example.com')).toBeVisible()
  expect(within(rows[0]).getByText('INVALID_PASSWORD')).toBeVisible()
  expect(within(rows[0]).getByText(/10:04:05/)).toBeVisible()
  expect(within(rows[1]).getByText('System')).toBeVisible()
  expect(screen.getByText('2 events')).toBeInTheDocument()
})

it('sends the filters from the address to the API and resets to page one', async () => {
  mock.onGet('/api/audit-logs').reply(200, page)
  show(`/admin/audit-logs?userId=${userId}&action=LOGIN_FAILED&from=2026-11-10&page=3`)
  await screen.findByRole('table')
  const first = mock.history.get.filter(request => request.url === '/api/audit-logs')[0]
  expect(first.params).toEqual({ page: '3', pageSize: '20', userId, action: 'LOGIN_FAILED', from: '2026-11-10T00:00:00+07:00' })

  fireEvent.change(screen.getByLabelText('To date'), { target: { value: '2026-11-12' } })
  await waitFor(() => expect(mock.history.get.filter(request => request.url === '/api/audit-logs')).toHaveLength(2))
  const second = mock.history.get.filter(request => request.url === '/api/audit-logs')[1]
  expect(second.params).toMatchObject({ page: '1', to: '2026-11-12T23:59:59.999+07:00' })
})

it('does not query an inverted date range', async () => {
  mock.onGet('/api/audit-logs').reply(200, page)
  show('/admin/audit-logs?from=2026-11-12&to=2026-11-10')
  expect(await screen.findByRole('alert')).toHaveTextContent('The end date must not be before the start date.')
  expect(mock.history.get.filter(request => request.url === '/api/audit-logs')).toHaveLength(0)
})

it('shows an empty state and a retryable error', async () => {
  mock.onGet('/api/audit-logs').replyOnce(500).onGet('/api/audit-logs').reply(200, { ...page, items: [], totalCount: 0, totalPages: 0 })
  show()
  fireEvent.click(await screen.findByRole('button', { name: 'Try again' }))
  expect(await screen.findByText('No audit events match these filters.')).toBeVisible()
})

it('keeps non-administrators out before fetching', async () => {
  show('/admin/audit-logs', 'DISPATCHER')
  await waitFor(() => expect(mock.history.get.filter(request => request.url === '/api/audit-logs')).toHaveLength(0))
  expect(screen.queryByRole('table')).not.toBeInTheDocument()
})
