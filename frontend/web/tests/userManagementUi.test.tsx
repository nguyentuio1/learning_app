import { beforeEach, afterEach, expect, it, vi } from 'vitest'
import { act, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { ConfigProvider } from 'antd'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import MockAdapter from 'axios-mock-adapter'
import { AuthContext } from '../src/contexts/authContext'
import { RequireAuth } from '../src/routes/RequireAuth'
import { UsersPage } from '../src/pages/UsersPage'
import { CreateUserPage } from '../src/pages/CreateUserPage'
import { UserDetailPage } from '../src/pages/UserDetailPage'
import { RolesPage } from '../src/pages/RolesPage'
import { apiClient } from '../src/services/apiClient'
import { session } from '../src/services/authSession'
import type { ManagedUser } from '../src/types/userManagement'

const id = '11111111-1111-1111-1111-111111111111'
const account: ManagedUser = { id, fullName: 'An Nguyen', email: 'an@example.com', phoneNumber: '0901234567', role: 'Dispatcher', roleCode: 'DISPATCHER', status: 'ACTIVE', avatarUrl: null, createdAt: '2026-10-17T08:00:00Z' }
let mock: MockAdapter
const logout = vi.fn().mockResolvedValue(undefined)
beforeEach(() => { session.clear(); mock = new MockAdapter(apiClient); logout.mockClear() })
afterEach(() => mock.restore())
function show(path = '/admin/users', role: string | null = 'ADMIN') {
  const auth = { status: role ? 'authenticated' as const : 'anonymous' as const,
    user: role ? { ...account, id: 'admin-id', role: { id: 5, code: role, name: role } } : null,
    accessToken: 'test', refreshToken: 'test-refresh', generation: 0, message: '',
    login: vi.fn(), logout, updateProfile: vi.fn() }
  return render(<ConfigProvider theme={{ token: { motion: false } }}><AuthContext.Provider value={auth}>
    <MemoryRouter initialEntries={[path]}><Routes>
      <Route path="/login" element={<h1>Login required</h1>} />
      <Route element={<RequireAuth roles={['ADMIN']} />}>
        <Route path="/admin/users" element={<UsersPage />} />
        <Route path="/admin/users/new" element={<CreateUserPage />} />
        <Route path="/admin/users/:id" element={<UserDetailPage />} />
        <Route path="/admin/roles" element={<RolesPage />} />
      </Route>
    </Routes></MemoryRouter>
  </AuthContext.Provider></ConfigProvider>)
}
it('redirects anonymous users before fetching data', async () => {
  show('/admin/users', null)
  expect(await screen.findByText('Login required')).toBeVisible()
  expect(mock.history.get).toHaveLength(0)
})
it.each(['CUSTOMER', 'DISPATCHER', 'STATION_OPERATOR', 'MANAGER'])('denies %s before fetching data', async role => {
  show('/admin/users', role)
  expect(await screen.findByText('Access denied')).toBeVisible()
  expect(mock.history.get).toHaveLength(0)
})
it('loads real API rows, filters and resets them', async () => {
  mock.onGet('/api/users').reply(200, [account])
  show()
  expect(await screen.findByRole('link', { name: 'View An Nguyen' })).toBeVisible()
  expect(screen.getByRole('columnheader', { name: 'Name' })).toBeVisible()
  expect(screen.getByRole('columnheader', { name: 'Email' })).toBeVisible()
  expect(screen.getByRole('cell', { name: 'an@example.com' })).toBeVisible()
  expect(screen.queryByLabelText('Account statistics')).not.toBeInTheDocument()
  const user = userEvent.setup()
  await user.type(screen.getByRole('textbox', { name: 'Search users' }), 'absent')
  expect(await screen.findByText('No matching users')).toBeVisible()
  await user.click(screen.getByRole('button', { name: 'Reset' }))
  expect(await screen.findByRole('link', { name: 'View An Nguyen' })).toBeVisible()
})
it('shows load failure and retries', async () => {
  mock.onGet('/api/users').replyOnce(500)
  mock.onGet('/api/users').reply(200, [account])
  show()
  await userEvent.click(await screen.findByRole('button', { name: 'Try again' }))
  expect(await screen.findByRole('link', { name: 'View An Nguyen' })).toBeVisible()
})
it('keeps the demo-style role and status selectors connected to real filters', async () => {
  mock.onGet('/api/users').reply(200, [account])
  show()
  await screen.findByRole('link', { name: 'View An Nguyen' })
  const user = userEvent.setup()
  await user.click(screen.getByRole('combobox', { name: 'Filter role' }))
  await user.click(await screen.findByRole('option', { name: 'Logistics Manager' }))
  expect(await screen.findByText('No matching users')).toBeVisible()
  await user.click(screen.getByRole('button', { name: 'Reset' }))
  expect(await screen.findByRole('link', { name: 'View An Nguyen' })).toBeVisible()
  await user.click(screen.getByRole('combobox', { name: 'Filter status' }))
  await user.click(await screen.findByRole('option', { name: 'Inactive' }))
  expect(await screen.findByText('No matching users')).toBeVisible()
  await user.click(screen.getByRole('button', { name: 'Reset' }))
  expect(await screen.findByRole('link', { name: 'View An Nguyen' })).toBeVisible()
  expect(within(screen.getByRole('cell', { name: 'Active' })).getByText('Active')).toHaveStyle({ backgroundColor: '#dcefd5', color: '#25673e' })
})
it('shows empty data without fabricated accounts', async () => {
  mock.onGet('/api/users').reply(200, [])
  show()
  expect(await screen.findByText('No matching users')).toBeVisible()
})
it('uses roles from project definitions and real counts', async () => {
  mock.onGet('/api/users').reply(200, [account])
  show('/admin/roles')
  expect(await screen.findByRole('heading', { name: 'System Administrator' })).toBeVisible()
  expect(await screen.findByText('1 users · View accounts →')).toHaveAttribute('href', '/admin/users?role=DISPATCHER')
})
async function fillCreate() {
  const user = userEvent.setup()
  await user.type(screen.getByLabelText('Full name'), 'An Nguyen')
  await user.type(screen.getByLabelText('Email'), 'an@example.com')
  await user.type(screen.getByLabelText('Phone number'), '0901234567')
  await user.type(screen.getByLabelText('Initial password'), 'Example123!')
  return user
}
it('validates a missing form before calling create', async () => {
  show('/admin/users/new')
  await userEvent.click(screen.getByRole('button', { name: 'Create account' }))
  expect(await screen.findByText('Enter a full name.')).toBeVisible()
  expect(mock.history.post).toHaveLength(0)
})
it('creates an account and opens the persisted detail', async () => {
  mock.onPost('/api/users').reply(201, account)
  mock.onGet(`/api/users/${id}`).reply(200, account)
  show('/admin/users/new')
  const user = await fillCreate()
  await user.click(screen.getByRole('button', { name: 'Create account' }))
  expect(await screen.findByText('Account created successfully.')).toBeVisible()
  expect(mock.history.post).toHaveLength(1)
  expect(JSON.parse(mock.history.post[0].data)).toMatchObject({ role: 'DISPATCHER', fullName: 'An Nguyen' })
})
it('retains form inputs and displays duplicate errors from backend', async () => {
  mock.onPost('/api/users').reply(409, { detail: 'Email is already registered.' })
  show('/admin/users/new')
  const user = await fillCreate()
  await user.click(screen.getByRole('button', { name: 'Create account' }))
  await waitFor(() => expect(screen.getAllByText('Email is already registered.').length).toBeGreaterThan(0))
  expect(screen.getByLabelText('Full name')).toHaveValue('An Nguyen')
})
it('loads, edits and saves details without changing roles or passwords', async () => {
  mock.onGet(`/api/users/${id}`).reply(200, account)
  mock.onPut(`/api/users/${id}`).reply(200, { ...account, fullName: 'Updated Name' })
  show(`/admin/users/${id}`)
  const name = await screen.findByLabelText('Full name')
  const user = userEvent.setup()
  await user.clear(name); await user.type(name, 'Updated Name')
  await user.click(screen.getByRole('button', { name: 'Save details' }))
  expect(await screen.findByText('Account details saved.')).toBeVisible()
  expect(JSON.parse(mock.history.put[0].data)).toEqual({ fullName: 'Updated Name', email: account.email, phoneNumber: account.phoneNumber })
})
it('requires confirmation and reflects successful deactivation only after API responds', async () => {
  mock.onGet(`/api/users/${id}`).reply(200, account)
  mock.onPut(`/api/users/${id}/deactivate`).reply(204)
  show(`/admin/users/${id}`)
  await userEvent.click(await screen.findByRole('button', { name: 'Deactivate account' }))
  expect(mock.history.put).toHaveLength(0)
  const dialog = screen.getByRole('dialog')
  await userEvent.click(within(dialog).getByRole('button', { name: 'Confirm deactivation' }))
  expect(await screen.findByRole('button', { name: 'Account inactive' })).toBeDisabled()
  expect(mock.history.put).toHaveLength(1)
})
it('does not mutate on cancel or mark success when deactivation fails', async () => {
  mock.onGet(`/api/users/${id}`).reply(200, account)
  mock.onPut(`/api/users/${id}/deactivate`).reply(500)
  show(`/admin/users/${id}`)
  const button = await screen.findByRole('button', { name: 'Deactivate account' })
  await userEvent.click(button)
  await userEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Cancel' }))
  expect(mock.history.put).toHaveLength(0)
  await userEvent.click(button)
  await userEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Confirm deactivation' }))
  expect(await screen.findByText('The service is temporarily unavailable. Please try again.')).toBeVisible()
  expect(screen.queryByText('Account inactive')).not.toBeInTheDocument()
})
it('assigns a role using its code after confirmation', async () => {
  mock.onGet(`/api/users/${id}`).reply(200, account)
  mock.onPut(`/api/users/${id}/role`).reply(204)
  show(`/admin/users/${id}`)
  const select = await screen.findByRole('combobox', { name: 'Assigned role' })
  const user = userEvent.setup()
  await user.click(select)
  await user.click(await screen.findByText('Logistics Manager'))
  await user.click(screen.getByRole('button', { name: 'Save role' }))
  expect(mock.history.put).toHaveLength(0)
  await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Confirm role change' }))
  expect(await screen.findByText(/Role updated\./)).toBeVisible()
  expect(JSON.parse(mock.history.put[0].data)).toEqual({ role: 'MANAGER' })
})
it('shows not found rather than an editable blank account', async () => {
  mock.onGet(`/api/users/${id}`).reply(404)
  show(`/admin/users/${id}`)
  expect(await screen.findByText('This user could not be found.')).toBeVisible()
  expect(screen.queryByRole('button', { name: 'Save details' })).not.toBeInTheDocument()
})

it('shows loading and aborts the request when leaving the page', async () => {
  let resolve!: (response: [number, ManagedUser]) => void
  mock.onGet(`/api/users/${id}`).reply(() => new Promise(done => { resolve = done }))
  const view = show(`/admin/users/${id}`)
  expect(screen.getByLabelText('Loading user')).toHaveAttribute('aria-busy', 'true')
  await waitFor(() => expect(mock.history.get).toHaveLength(1))
  view.unmount()
  expect(mock.history.get[0].signal?.aborted).toBe(true)
  await act(async () => { resolve([200, account]) })
})

it('blocks repeated submissions while saving', async () => {
  let resolve!: (response: [number, ManagedUser]) => void
  mock.onPost('/api/users').reply(() => new Promise(done => { resolve = done }))
  mock.onGet(`/api/users/${id}`).reply(200, account)
  show('/admin/users/new')
  const user = await fillCreate()
  await user.dblClick(screen.getByRole('button', { name: 'Create account' }))
  await waitFor(() => expect(mock.history.post).toHaveLength(1))
  expect(screen.getByLabelText('Full name')).toBeDisabled()
  await act(async () => { resolve([201, account]) })
  expect(await screen.findByText('Account created successfully.')).toBeVisible()
})

it('keeps details intact after a save error', async () => {
  mock.onGet(`/api/users/${id}`).reply(200, account)
  mock.onPut(`/api/users/${id}`).reply(400, { detail: 'Review the phone number.', errors: { phoneNumber: ['Phone format is invalid.'] } })
  show(`/admin/users/${id}`)
  await screen.findByLabelText('Full name')
  await userEvent.click(screen.getByRole('button', { name: 'Save details' }))
  expect(await screen.findByText('Phone format is invalid.')).toBeVisible()
  expect(screen.getByLabelText('Full name')).toHaveValue(account.fullName)
  expect(screen.queryByText('Account details saved.')).not.toBeInTheDocument()
})

it('signs out after deactivating the current administrator', async () => {
  mock.onGet('/api/users/admin-id').reply(200, { ...account, id: 'admin-id', roleCode: 'ADMIN', role: 'System Administrator' })
  mock.onPut('/api/users/admin-id/deactivate').reply(204)
  show('/admin/users/admin-id')
  await userEvent.click(await screen.findByRole('button', { name: 'Deactivate account' }))
  await waitFor(() => expect(screen.getByText(/You are changing your own account/)).toBeVisible())
  await userEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Confirm deactivation' }))
  await waitFor(() => expect(logout).toHaveBeenCalledTimes(1))
})
