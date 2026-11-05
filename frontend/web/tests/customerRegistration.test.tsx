import { beforeEach, expect, it, vi } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useState, type ContextType } from 'react'
import { ConfigProvider } from 'antd'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { AuthContext } from '../src/contexts/authContext'
import { LoginPage } from '../src/pages/LoginPage'
import { RegisterPage } from '../src/pages/RegisterPage'

vi.mock('../src/components/auth/LoginIllustration', () => ({ LoginIllustration: () => null }))

const register = vi.fn()
const login = vi.fn()
function authValue(): NonNullable<ContextType<typeof AuthContext>> {
  return {
    status: 'anonymous',
    user: null,
    accessToken: null,
    refreshToken: null,
    generation: 0,
    message: '',
    login,
    register,
    logout: vi.fn(),
    updateProfile: vi.fn(),
  }
}

function renderRegistration(path = '/register') {
  return render(
    <ConfigProvider theme={{ token: { motion: false } }}>
      <AuthContext.Provider value={authValue()}>
        <MemoryRouter initialEntries={[path]}>
          <Routes>
            <Route path="/register" element={<RegisterPage />} />
            <Route path="/login" element={<LoginPage />} />
            <Route path="/orders" element={<h1>Customer orders</h1>} />
          </Routes>
        </MemoryRouter>
      </AuthContext.Provider>
    </ConfigProvider>,
  )
}

function AuthenticatedRegistrationHarness() {
  const [authenticated, setAuthenticated] = useState(false)
  const value = authValue()
  value.status = authenticated ? 'authenticated' : 'anonymous'
  value.user = authenticated ? {
    id: '22222222-2222-2222-2222-222222222222',
    email: 'new@example.com',
    fullName: 'New Customer',
    phoneNumber: '0901234567',
    avatarUrl: null,
    status: 'ACTIVE',
    role: { id: 1, code: 'CUSTOMER', name: 'Customer' },
    createdAt: '2026-11-06T00:00:00Z',
  } : null
  value.register = async request => {
    await register(request)
    setAuthenticated(true)
  }
  return <ConfigProvider theme={{ token: { motion: false } }}>
    <AuthContext.Provider value={value}>
      <MemoryRouter initialEntries={['/register']}>
        <Routes>
          <Route path="/register" element={<RegisterPage />} />
          <Route path="/orders" element={<h1>Customer orders</h1>} />
        </Routes>
      </MemoryRouter>
    </AuthContext.Provider>
  </ConfigProvider>
}

beforeEach(() => {
  register.mockReset().mockResolvedValue(undefined)
  login.mockReset().mockResolvedValue(undefined)
})

it('offers registration from login and log in from registration', async () => {
  const user = userEvent.setup()
  renderRegistration('/login')

  await user.click(screen.getByRole('link', { name: 'Register' }))
  expect(await screen.findByRole('heading', { name: 'Create customer account' })).toBeVisible()
  await user.click(screen.getByRole('link', { name: 'Log in' }))
  expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeVisible()
})

it('validates required fields inline and submits valid customer details', async () => {
  const user = userEvent.setup()
  renderRegistration()

  await user.click(screen.getByRole('button', { name: 'Register' }))
  expect(await screen.findByText('Enter your full name.')).toBeVisible()
  expect(screen.getByText('Enter your email.')).toBeVisible()
  expect(screen.getByText('Enter your phone number.')).toBeVisible()
  expect(screen.getByText('Enter your password.')).toBeVisible()
  expect(register).not.toHaveBeenCalled()

  await user.type(screen.getByLabelText('Full name'), 'New Customer')
  await user.type(screen.getByLabelText('Email'), 'new@example.com')
  await user.type(screen.getByLabelText('Phone number'), '0901234567')
  await user.type(screen.getByLabelText('Password'), 'Passw0rd12')
  await user.click(screen.getByRole('button', { name: 'Register' }))

  await waitFor(() => expect(register).toHaveBeenCalledWith({
    fullName: 'New Customer',
    email: 'new@example.com',
    phoneNumber: '0901234567',
    password: 'Passw0rd12',
  }))
})

it('shows duplicate-email conflict detail in an alert', async () => {
  register.mockRejectedValue(Object.assign(new Error('Conflict'), {
    isAxiosError: true,
    response: { status: 409, data: { detail: 'Email is already registered.' } },
  }))
  const user = userEvent.setup()
  renderRegistration()
  await user.type(screen.getByLabelText('Full name'), 'New Customer')
  await user.type(screen.getByLabelText('Email'), 'used@example.com')
  await user.type(screen.getByLabelText('Phone number'), '0901234567')
  await user.type(screen.getByLabelText('Password'), 'Passw0rd12')
  await user.click(screen.getByRole('button', { name: 'Register' }))

  expect(await screen.findByText('Email is already registered.')).toBeVisible()
})

it('navigates to orders after registration establishes the authenticated session', async () => {
  const user = userEvent.setup()
  render(<AuthenticatedRegistrationHarness />)
  await user.type(screen.getByLabelText('Full name'), 'New Customer')
  await user.type(screen.getByLabelText('Email'), 'new@example.com')
  await user.type(screen.getByLabelText('Phone number'), '0901234567')
  await user.type(screen.getByLabelText('Password'), 'Passw0rd12')
  await user.click(screen.getByRole('button', { name: 'Register' }))

  expect(await screen.findByRole('heading', { name: 'Customer orders' })).toBeVisible()
})
