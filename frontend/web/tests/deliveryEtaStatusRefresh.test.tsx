// LA-173: real page/hook/API refresh path; only HTTP and SignalR transports are mocked.
import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { act, render, screen, waitFor } from '@testing-library/react'
import { ConfigProvider } from 'antd'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import MockAdapter from 'axios-mock-adapter'
import { DeliveryEtaPage } from '../src/features/deliveryEta/DeliveryEtaPage'
import { apiClient } from '../src/services/apiClient'
import { session } from '../src/services/authSession'

const hub = vi.hoisted(() => ({ handlers: new Map<string, () => void>(), invoke: vi.fn().mockResolvedValue(undefined) }))
vi.mock('../src/hooks/useAuth', () => ({ useAuth: () => ({ user: { id: 'eta-status-reader', role: { code: 'CUSTOMER' } } }) }))
vi.mock('@microsoft/signalr', () => ({
  HubConnectionState: { Disconnected: 'Disconnected' }, LogLevel: { None: 6 },
  HubConnectionBuilder: class {
    withUrl() { return this }
    withAutomaticReconnect() { return this }
    configureLogging() { return this }
    build() {
      return {
        state: 'Disconnected', start: vi.fn().mockResolvedValue(undefined), stop: vi.fn().mockResolvedValue(undefined),
        invoke: hub.invoke,
        on: (name: string, handler: () => void) => { hub.handlers.set(name, handler) },
        off: (name: string) => { hub.handlers.delete(name) },
        onreconnecting: vi.fn(), onreconnected: vi.fn(), onclose: vi.fn(),
      }
    }
  },
}))

const orderId = '11111111-2222-4333-8444-555555555555'
const endpoint = `/api/delivery-orders/${orderId}/eta`
let mock: MockAdapter
beforeEach(() => {
  Object.defineProperty(document, 'hidden', { configurable: true, value: false })
  session.clear(); hub.handlers.clear(); hub.invoke.mockClear()
  mock = new MockAdapter(apiClient)
})
afterEach(() => { mock.restore(); Reflect.deleteProperty(document, 'hidden') })

it('refreshes an APPROVED screen on status notifications, corrects ETA and ends its countdown on arrival', async () => {
  const estimate = (status: string, minutes: number) => ({ orderId, status,
    estimatedDeliveryTime: new Date(Date.now() + minutes * 60_000).toISOString(), actualDeliveryTime: null, aiEtaPrediction: null })
  mock.onGet(endpoint).reply(200, estimate('APPROVED', 20))
  render(<ConfigProvider theme={{ token: { motion: false } }}><MemoryRouter initialEntries={['/delivery-eta/' + orderId]}>
    <Routes><Route path="/delivery-eta/:id" element={<DeliveryEtaPage />} /></Routes>
  </MemoryRouter></ConfigProvider>)
  await screen.findByRole('heading', { name: 'Estimated arrival' })
  await waitFor(() => expect(hub.invoke).toHaveBeenCalledWith('Subscribe', orderId))
  await screen.findByText('Status updates connected')
  await screen.findByRole('button', { name: 'Refresh estimate' })
  const requestsBefore = mock.history.get.filter(r => r.url === endpoint).length
  const notify = hub.handlers.get('StatusChanged')
  expect(notify).toBeTypeOf('function')

  mock.onGet(endpoint).reply(200, estimate('IN_TRANSIT', 18))
  act(() => { notify!() })
  expect(await screen.findByText('18 minutes remaining')).toBeVisible()
  expect(mock.history.get.filter(r => r.url === endpoint).length).toBeGreaterThan(requestsBefore)
  await screen.findByRole('button', { name: 'Refresh estimate' })

  mock.onGet(endpoint).reply(200, estimate('ARRIVED', 18))
  act(() => { notify!() })
  expect(await screen.findByRole('heading', { name: 'Arrival estimate ended' })).toBeVisible()
  expect(screen.getByText('The delivery has arrived and is awaiting confirmation.')).toBeVisible()
  expect(screen.queryByText(/minutes remaining/)).not.toBeInTheDocument()
})
