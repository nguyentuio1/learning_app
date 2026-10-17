import process from 'node:process'
import { afterAll, beforeAll, expect, it, vi } from 'vitest'
import { cleanup, render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { ConfigProvider } from 'antd'
import { MemoryRouter } from 'react-router-dom'
import axios from 'axios'
import type { ManagedUser } from '../src/types/userManagement'

// jsdom has no SVG path geometry; omit only the decorative login animation.
vi.mock('../src/components/auth/LoginIllustration', () => ({ LoginIllustration: () => null }))

// No mocked HTTP responses: real components, routes, session, interceptors and API.
// Node's HTTP adapter avoids jsdom CORS limitations; this is not a browser E2E test.
let session: typeof import('../src/services/authSession').session
let api: typeof import('../src/services/userManagementApi').userManagementApi
let AuthProvider: typeof import('../src/contexts/AuthProvider').AuthProvider
let AppRouter: typeof import('../src/routes/AppRouter').AppRouter
const previousAdapter = axios.defaults.adapter
const target = process.env.LA94_API_URL!
const adminEmail = process.env.LA94_ADMIN_EMAIL!
const adminPassword = process.env.LA94_ADMIN_PASSWORD!
const suffix = `${Date.now()}`
const staffEmail = `la94-${suffix}@example.com`
const staffPhone = `08${suffix.slice(-8)}`
const password = 'Integration123!'
let created: ManagedUser | undefined

beforeAll(async () => {
  axios.defaults.adapter = 'http'
  vi.stubEnv('DEV', false)
  vi.stubEnv('VITE_API_BASE_URL', target)
  sessionStorage.clear()
  ;({ session } = await import('../src/services/authSession'))
  ;({ userManagementApi: api } = await import('../src/services/userManagementApi'))
  ;({ AuthProvider } = await import('../src/contexts/AuthProvider'))
  ;({ AppRouter } = await import('../src/routes/AppRouter'))
  await session.initialize()
})

afterAll(async () => {
  cleanup()
  // Keep audit/history, but make any test account inactive even if the test failed.
  try {
    await session.login(adminEmail, adminPassword)
    const user = created ?? (await api.list()).find(item => item.email === staffEmail)
    if (user?.status !== 'INACTIVE' && user) await api.deactivate(user.id)
  } finally {
    await session.logout()
    axios.defaults.adapter = previousAdapter
    vi.unstubAllEnvs()
  }
})

function open(path: string) {
  return render(<ConfigProvider theme={{ token: { motion: false } }}>
    <MemoryRouter initialEntries={[path]}><AuthProvider><AppRouter /></AuthProvider></MemoryRouter>
  </ConfigProvider>)
}

it('completes the admin UI workflow against LA-92 and reloads persisted results', async () => {
  const user = userEvent.setup()
  open('/admin/users')
  await user.type(await screen.findByLabelText('Email'), adminEmail)
  await user.type(screen.getByLabelText('Password'), adminPassword)
  await user.click(screen.getByRole('button', { name: 'Sign in' }))
  expect(await screen.findByRole('heading', { name: 'Users', exact: true })).toBeVisible()
  await user.click(screen.getByRole('link', { name: 'Create staff account' }))
  await user.type(await screen.findByLabelText('Full name'), 'LA94 Integration Staff')
  await user.type(screen.getByLabelText('Email'), staffEmail)
  await user.type(screen.getByLabelText('Phone number'), staffPhone)
  await user.type(screen.getByLabelText('Initial password'), password)
  await user.click(screen.getByRole('button', { name: 'Create account' }))
  expect(await screen.findByText('Account created successfully.')).toBeVisible()
  created = (await api.list()).find(item => item.email === staffEmail)
  expect(created).toMatchObject({ roleCode: 'DISPATCHER', status: 'ACTIVE', phoneNumber: staffPhone })

  const name = await screen.findByLabelText('Full name')
  await user.clear(name)
  await user.type(name, 'LA94 Updated Staff')
  await user.click(screen.getByRole('button', { name: 'Save details' }))
  expect(await screen.findByText('Account details saved.')).toBeVisible()
  expect((await api.get(created!.id)).fullName).toBe('LA94 Updated Staff')

  // Render the real duplicate-email 409 on the field without claiming success.
  const email = screen.getByLabelText('Email')
  await user.clear(email)
  await user.type(email, adminEmail)
  await user.click(screen.getByRole('button', { name: 'Save details' }))
  expect((await screen.findAllByText('Email is already registered.')).length).toBeGreaterThan(0)
  expect((await api.get(created!.id)).email).toBe(staffEmail)
  await user.clear(email)
  await user.type(email, staffEmail)
  const phone = screen.getByLabelText('Phone number')
  await user.clear(phone)
  await user.type(phone, session.getSnapshot().user!.phoneNumber)
  await user.click(screen.getByRole('button', { name: 'Save details' }))
  expect((await screen.findAllByText('Phone number is already registered.')).length).toBeGreaterThan(0)
  expect((await api.get(created!.id)).phoneNumber).toBe(staffPhone)
  await user.clear(phone)
  await user.type(phone, staffPhone)

  await user.click(screen.getByRole('combobox', { name: 'Assigned role' }))
  await user.click(await screen.findByText('Logistics Manager'))
  await user.click(screen.getByRole('button', { name: 'Save role' }))
  await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Confirm role change' }))
  expect(await screen.findByText(/Role updated\./)).toBeVisible()
  expect((await api.get(created!.id)).roleCode).toBe('MANAGER')

  // A new mount reads the API again, rather than relying on optimistic UI state.
  cleanup()
  open(`/admin/users/${created!.id}`)
  expect(await screen.findByLabelText('Full name')).toHaveValue('LA94 Updated Staff')
  expect(screen.getByText('Logistics Manager')).toBeVisible()
  const staffSession = (await axios.post(`${target}/api/auth/login`, { email: staffEmail, password })).data
  await expect(axios.get(`${target}/api/users`, {
    headers: { Authorization: `Bearer ${staffSession.accessToken}` },
  })).rejects.toMatchObject({ response: { status: 403 } })
  await user.click(screen.getByRole('button', { name: 'Deactivate account' }))
  await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Confirm deactivation' }))
  expect(await screen.findByRole('button', { name: 'Account inactive' })).toBeDisabled()
  expect((await api.get(created!.id)).status).toBe('INACTIVE')
  await expect(axios.post(`${target}/api/auth/login`, { email: staffEmail, password }))
    .rejects.toMatchObject({ response: { status: 403 } })
  await expect(axios.post(`${target}/api/auth/refresh`, { refreshToken: staffSession.refreshToken }))
    .rejects.toMatchObject({ response: { status: 401 } })

  cleanup()
  open(`/admin/users/${session.getSnapshot().user!.id}`)
  expect(await screen.findByRole('button', { name: 'Deactivate account' })).toBeDisabled()
  expect(screen.getByRole('combobox', { name: 'Assigned role' })).toBeDisabled()
  expect(screen.getByRole('button', { name: 'Save details' })).toBeEnabled()
})
