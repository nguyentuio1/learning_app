// LA-161: Test ETA estimation web UI and integration
import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { ConfigProvider } from 'antd'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import MockAdapter from 'axios-mock-adapter'
import { DeliveryEtaPage } from '../src/features/deliveryEta/DeliveryEtaPage'
import { getDeliveryEta } from '../src/features/deliveryEta/etaApi'
import { apiClient } from '../src/services/apiClient'
import { session } from '../src/services/authSession'

let currentRole = 'CUSTOMER'

vi.mock('../src/hooks/useAuth', () => ({
  useAuth: () => ({
    user: {
      id: 'user-eta-1',
      email: 'eta@smartdrone.local',
      fullName: 'ETA Tester',
      role: { code: currentRole, name: currentRole },
    },
  }),
}))

vi.mock('@microsoft/signalr', () => ({
  HubConnectionState: { Disconnected: 'Disconnected' },
  LogLevel: { None: 6 },
  HubConnectionBuilder: class {
    withUrl() { return this }
    withAutomaticReconnect() { return this }
    configureLogging() { return this }
    build() {
      return {
        state: 'Disconnected',
        start: vi.fn().mockRejectedValue(new Error('No live hub in unit test')),
        stop: vi.fn().mockResolvedValue(undefined),
        invoke: vi.fn().mockResolvedValue(undefined),
        on: vi.fn(),
        off: vi.fn(),
        onreconnecting: vi.fn(),
        onreconnected: vi.fn(),
        onclose: vi.fn(),
      }
    }
  },
}))

const orderId = '11111111-2222-4333-8444-555555555555'

let mock: MockAdapter

beforeEach(() => {
  currentRole = 'CUSTOMER'
  session.clear()
  mock = new MockAdapter(apiClient)
})

afterEach(() => {
  mock.restore()
})

function renderEta(initialPath = '/delivery-eta') {
  return render(
    <ConfigProvider theme={{ token: { motion: false } }}>
      <MemoryRouter initialEntries={[initialPath]}>
        <Routes>
          <Route path="/delivery-eta" element={<DeliveryEtaPage />} />
          <Route path="/delivery-eta/:id" element={<DeliveryEtaPage />} />
        </Routes>
      </MemoryRouter>
    </ConfigProvider>,
  )
}

it('shows invalid order ID alert for malformed route id', () => {
  renderEta('/delivery-eta/not-a-uuid')
  expect(screen.getByText('Invalid order ID')).toBeInTheDocument()
})

it('lets a customer search and select an order from the picker', async () => {
  const user = userEvent.setup()
  mock.onGet('/api/delivery-orders').reply(200, {
    items: [{ id: orderId, trackingNumber: 'DD-202611-0161' }],
    page: 1,
    pageSize: 20,
    totalCount: 1,
    totalPages: 1,
  })
  mock.onGet(`/api/delivery-orders/${orderId}/eta`).reply(200, {
    orderId,
    status: 'IN_TRANSIT',
    estimatedDeliveryTime: new Date(Date.now() + 18 * 60_000).toISOString(),
    actualDeliveryTime: null,
    aiEtaPrediction: {
      calculatedDistanceKm: 12.4,
      predictedDurationMinutes: 18,
      weatherCondition: 'CLEAR',
      confidenceScore: 0.92,
      createdAt: new Date().toISOString(),
    },
  })

  renderEta('/delivery-eta')

  const select = await screen.findByRole('combobox', { name: 'Your delivery order' })
  await user.click(select)
  await user.click(await screen.findByText('DD-202611-0161'))

  expect(await screen.findByText('Estimated arrival')).toBeInTheDocument()
  expect(screen.getByText(/18 minutes remaining|17 minutes remaining/)).toBeInTheDocument()
  expect(screen.getByText('12.4 km')).toBeInTheDocument()
  expect(screen.getByText('18 min')).toBeInTheDocument()
  expect(screen.getByText('CLEAR')).toBeInTheDocument()
  expect(screen.getByText('92%')).toBeInTheDocument()
  expect(screen.getByRole('link', { name: 'Open order details' })).toHaveAttribute('href', `/orders/${orderId}`)
})

it('renders schedule fallback message when aiEtaPrediction is null', async () => {
  currentRole = 'DISPATCHER'
  mock.onGet(`/api/delivery-orders/${orderId}/eta`).reply(200, {
    orderId,
    status: 'APPROVED',
    estimatedDeliveryTime: new Date(Date.now() + 30 * 60_000).toISOString(),
    actualDeliveryTime: null,
    aiEtaPrediction: null,
  })

  renderEta(`/delivery-eta/${orderId}`)

  expect(await screen.findByText('No AI prediction has been recorded.')).toBeInTheDocument()
  expect(screen.getByText('No AI prediction is available. Any arrival time shown comes from the order schedule.')).toBeInTheDocument()
  expect(screen.getByRole('link', { name: 'View delivery tracking' })).toHaveAttribute('href', `/delivery-tracking/${orderId}`)
})

it('shows ended state and actual delivery time for delivered orders', async () => {
  mock.onGet(`/api/delivery-orders/${orderId}/eta`).reply(200, {
    orderId,
    status: 'DELIVERED',
    estimatedDeliveryTime: '2026-11-16T08:30:00Z',
    actualDeliveryTime: '2026-11-16T08:28:00Z',
    aiEtaPrediction: null,
  })

  renderEta(`/delivery-eta/${orderId}`)

  expect(await screen.findByText('Arrival estimate ended')).toBeInTheDocument()
  expect(screen.getByText('Delivery has been confirmed.')).toBeInTheDocument()
  expect(screen.getByText('Recorded delivery time')).toBeInTheDocument()
})

it('shows unavailable info alert when eta endpoint returns 404', async () => {
  mock.onGet(`/api/delivery-orders/${orderId}/eta`).reply(404)

  renderEta(`/delivery-eta/${orderId}`)

  expect(await screen.findByText('Estimate unavailable for this delivery')).toBeInTheDocument()
})

it('rejects response when returned orderId does not match requested id', async () => {
  mock.onGet(`/api/delivery-orders/${orderId}/eta`).reply(200, {
    orderId: '99999999-2222-4333-8444-555555555555',
    status: 'IN_TRANSIT',
    estimatedDeliveryTime: null,
    actualDeliveryTime: null,
    aiEtaPrediction: null,
  })

  await expect(getDeliveryEta(orderId, new AbortController().signal)).rejects.toThrow('Unexpected delivery estimate')
})
