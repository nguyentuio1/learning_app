// LA-149: Test delivery map web UI and integration
import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { ConfigProvider } from 'antd'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import MockAdapter from 'axios-mock-adapter'
import { DeliveryMapPage } from '../src/features/deliveryMap/DeliveryMapPage'
import { DeliveryMap } from '../src/features/deliveryMap/DeliveryMap'
import { apiClient } from '../src/services/apiClient'
import { session } from '../src/services/authSession'

vi.mock('../src/hooks/useAuth', () => ({
  useAuth: () => ({
    user: {
      id: 'dispatcher-1',
      email: 'dispatcher@smartdrone.local',
      fullName: 'Dispatcher',
      role: 'DISPATCHER',
    },
  }),
}))

const hubHandlers = new Map<string, () => void>()

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
        start: vi.fn().mockResolvedValue(undefined),
        stop: vi.fn().mockResolvedValue(undefined),
        invoke: vi.fn().mockResolvedValue(undefined),
        on: vi.fn((event: string, handler: () => void) => {
          hubHandlers.set(event, handler)
        }),
        off: vi.fn((event: string) => {
          hubHandlers.delete(event)
        }),
        onreconnecting: vi.fn(),
        onreconnected: vi.fn(),
        onclose: vi.fn(),
      }
    }
  },
}))

const orderId = '11111111-2222-4333-8444-555555555555'

const activeOrder = {
  orderId,
  trackingNumber: 'DD-202611-0147',
  orderStatus: 'IN_TRANSIT',
  missionId: 'aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee',
  flightStatus: 'IN_AIR',
  droneSerialNumber: 'DRN-MAP-01',
  originStationId: 'station-a',
  destinationStationId: 'station-b',
  scheduledDepartureTime: '2026-11-15T08:00:00Z',
  estimatedDeliveryTime: '2026-11-15T08:30:00Z',
  lastTelemetryAt: new Date().toISOString(),
  telemetryStale: false,
}

const makeSnapshot = (overrides: Record<string, unknown> = {}) => ({
  orderId,
  trackingNumber: 'DD-202611-0147',
  status: 'IN_TRANSIT',
  originStation: { id: 'station-a', name: 'Hub Central', latitude: 10.7769, longitude: 106.7009 },
  destinationStation: { id: 'station-b', name: 'Hub East', latitude: 10.8231, longitude: 106.6297 },
  estimatedDeliveryTime: '2026-11-15T08:30:00Z',
  lastPosition: {
    latitude: 10.795123,
    longitude: 106.665432,
    altitudeMeters: 65,
    speedMps: 14,
    batteryPercentage: 82,
    timestamp: new Date().toISOString(),
  },
  telemetryStale: false,
  ...overrides,
})

let mock: MockAdapter

beforeEach(() => {
  hubHandlers.clear()
  session.clear()
  mock = new MockAdapter(apiClient)
})

afterEach(() => {
  mock.restore()
  session.clear()
})

function renderMap(initialPath = '/delivery-map') {
  return render(
    <ConfigProvider>
      <MemoryRouter initialEntries={[initialPath]}>
        <Routes>
          <Route path="/delivery-map" element={<DeliveryMapPage />} />
          <Route path="/delivery-map/:id" element={<DeliveryMapPage />} />
        </Routes>
      </MemoryRouter>
    </ConfigProvider>,
  )
}

it('shows empty state when there are no active deliveries', async () => {
  mock.onGet('/api/dispatch/active-deliveries').reply(200, [])
  renderMap('/delivery-map')

  expect(await screen.findByText('There are no active deliveries right now.')).toBeInTheDocument()
})

it('shows invalid order ID alert for non-UUID route parameter', async () => {
  mock.onGet('/api/dispatch/active-deliveries').reply(200, [activeOrder])
  renderMap('/delivery-map/not-a-uuid')

  expect(await screen.findByText('Invalid order ID')).toBeInTheDocument()
  expect(screen.getByText('Choose a delivery from the list.')).toBeInTheDocument()
})

it('renders station markers, coordinates, and live drone marker for an in-transit delivery', async () => {
  mock.onGet('/api/dispatch/active-deliveries').reply(200, [activeOrder])
  mock.onGet(`/api/delivery-orders/${orderId}/tracking`).reply(200, makeSnapshot())

  renderMap(`/delivery-map/${orderId}`)

  expect(await screen.findByText('DD-202611-0147')).toBeInTheDocument()
  expect(screen.getByText('Hub Central')).toBeInTheDocument()
  expect(screen.getByText('Hub East')).toBeInTheDocument()
  expect(screen.getByText('10.795123, 106.665432')).toBeInTheDocument()
  expect(screen.getByLabelText('Delivery geographical map')).toBeInTheDocument()
  expect(screen.getByLabelText('Reported drone marker')).toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Center drone' })).toBeEnabled()
})

it('displays stale position warning when telemetryStale is true', async () => {
  mock.onGet('/api/dispatch/active-deliveries').reply(200, [activeOrder])
  mock.onGet(`/api/delivery-orders/${orderId}/tracking`).reply(200, makeSnapshot({ telemetryStale: true }))

  renderMap(`/delivery-map/${orderId}`)

  expect(await screen.findByText('Position is out of date')).toBeInTheDocument()
  expect(screen.getByText(/Last-known drone position/)).toBeInTheDocument()
})

it('hides drone marker and disables Center drone when order is not IN_TRANSIT', async () => {
  mock.onGet('/api/dispatch/active-deliveries').reply(200, [])
  mock.onGet(`/api/delivery-orders/${orderId}/tracking`).reply(200, makeSnapshot({ status: 'DELIVERED' }))

  renderMap(`/delivery-map/${orderId}`)

  expect(await screen.findByText('No live position: this delivery is not in transit.')).toBeInTheDocument()
  expect(screen.queryByLabelText('Reported drone marker')).not.toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Center drone' })).toBeDisabled()
})

it('handles unsupported station coordinates gracefully', () => {
  render(
    <ConfigProvider>
      <DeliveryMap
        origin={{ id: 'a', name: 'Pole', latitude: 89.5, longitude: 0 }}
        destination={{ id: 'b', name: 'Hub', latitude: 10.8, longitude: 106.6 }}
        position={null}
        stale={false}
      />
    </ConfigProvider>,
  )

  expect(screen.getByText('Station coordinates cannot be displayed on this basemap.')).toBeInTheDocument()
})

it('supports zoom, keyboard pan, and basemap retry when a tile fails to load', async () => {
  const { container } = render(
    <ConfigProvider>
      <DeliveryMap
        origin={{ id: 'a', name: 'Hub A', latitude: 10.7769, longitude: 106.7009 }}
        destination={{ id: 'b', name: 'Hub B', latitude: 10.8231, longitude: 106.6297 }}
        position={{
          latitude: 10.8,
          longitude: 106.66,
          altitudeMeters: 50,
          speedMps: 10,
          batteryPercentage: 90,
          timestamp: new Date().toISOString(),
        }}
        stale={false}
      />
    </ConfigProvider>,
  )

  const svg = screen.getByRole('img')
  const marker = screen.getByLabelText('Reported drone marker')
  const initialTransform = marker.getAttribute('transform')
  expect(initialTransform).toBeTruthy()

  fireEvent.keyDown(svg, { key: 'ArrowLeft' })
  const afterPanTransform = marker.getAttribute('transform')
  expect(afterPanTransform).not.toBe(initialTransform)

  fireEvent.click(screen.getByRole('button', { name: 'Zoom in' }))
  const afterZoomInTransform = marker.getAttribute('transform')
  expect(afterZoomInTransform).not.toBe(afterPanTransform)

  fireEvent.click(screen.getByRole('button', { name: 'Zoom out' }))
  const afterZoomOutTransform = marker.getAttribute('transform')
  expect(afterZoomOutTransform).not.toBe(afterZoomInTransform)

  fireEvent.click(screen.getByRole('button', { name: 'Fit stations' }))
  expect(marker.getAttribute('transform')).toBe(initialTransform)

  fireEvent.click(screen.getByRole('button', { name: 'Center drone' }))
  expect(marker.getAttribute('transform')).not.toBe(initialTransform)

  const tile = container.querySelector('image')
  expect(tile).not.toBeNull()
  fireEvent.error(tile!)

  await waitFor(() => {
    expect(screen.getByText(/Basemap tiles are unavailable\./)).toBeInTheDocument()
  })

  fireEvent.click(screen.getByRole('button', { name: 'Retry basemap' }))
  await waitFor(() => {
    expect(screen.queryByText(/Basemap tiles are unavailable\./)).not.toBeInTheDocument()
  })
})

it('updates drone marker coordinates and transform when SignalR telemetry arrives', async () => {
  let currentSnapshot = makeSnapshot()
  mock.onGet('/api/dispatch/active-deliveries').reply(200, [activeOrder])
  mock.onGet(`/api/delivery-orders/${orderId}/tracking`).reply(() => [200, currentSnapshot])

  renderMap(`/delivery-map/${orderId}`)

  expect(await screen.findByText('10.795123, 106.665432')).toBeInTheDocument()
  const initialTransform = screen.getByLabelText('Reported drone marker').getAttribute('transform')

  await waitFor(() => {
    expect(hubHandlers.has('PositionUpdated')).toBe(true)
  })

  currentSnapshot = makeSnapshot({
    lastPosition: {
      latitude: 10.812345,
      longitude: 106.645678,
      altitudeMeters: 70,
      speedMps: 15,
      batteryPercentage: 80,
      timestamp: new Date().toISOString(),
    },
  })

  await act(async () => {
    hubHandlers.get('PositionUpdated')?.()
  })

  expect(await screen.findByText('10.812345, 106.645678')).toBeInTheDocument()
  await waitFor(() => {
    expect(screen.getByLabelText('Reported drone marker').getAttribute('transform')).not.toBe(initialTransform)
  })
})
