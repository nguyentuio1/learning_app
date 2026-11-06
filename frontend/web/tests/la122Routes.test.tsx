import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { screen } from '@testing-library/react'
import { Route, Routes } from 'react-router-dom'
import MockAdapter from 'axios-mock-adapter'
import { apiClient } from '../src/services/apiClient'
import { AppRouter } from '../src/routes/AppRouter'
import { LoginPage } from '../src/pages/LoginPage'
import { mount } from './operationsFixtures'
vi.mock('../src/components/auth/LoginIllustration', () => ({ LoginIllustration: () => null }))
let mock: MockAdapter
beforeEach(() => { mock = new MockAdapter(apiClient); mock.onGet('/api/landing-stations').reply(200, []); mock.onGet('/api/drones').reply(200, []) })
afterEach(() => mock.restore())
it('opens the implemented task route and shows its menu for an allowed role', async () => {
  mount(<AppRouter />, 'CUSTOMER', '/station-availability')
  expect(await screen.findByRole('heading', { name: 'Station availability', exact: true })).toBeVisible()
  expect(screen.getByRole('link', { name: 'Station availability', exact: true })).toBeVisible()
})


it('retains an authenticated task destination after visiting sign in', async () => {
  // RequireAuth supplies this state after an unauthenticated deep link.
  const { MemoryRouter } = await import('react-router-dom')
  const { AuthContext } = await import('../src/contexts/authContext')
  const { authValue } = await import('./operationsFixtures')
  const { render } = await import('@testing-library/react')
  render(<AuthContext.Provider value={authValue('CUSTOMER')}><MemoryRouter initialEntries={[{ pathname: '/login', state: { from: '/station-availability' } }]}>
    <Routes><Route path="/login" element={<LoginPage />} /><Route path="/station-availability" element={<h1>Task destination</h1>} /></Routes>
  </MemoryRouter></AuthContext.Provider>)
  expect(await screen.findByText('Task destination')).toBeVisible()
})
