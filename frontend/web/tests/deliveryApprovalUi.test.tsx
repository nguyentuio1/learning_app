// LA-133: Test delivery request approval UI and integration
import { afterEach, beforeEach, expect, it } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { ConfigProvider } from 'antd'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import MockAdapter from 'axios-mock-adapter'
import { ApprovalQueuePage } from '../src/features/approvals/ApprovalQueuePage'
import { apiClient } from '../src/services/apiClient'
import { session } from '../src/services/authSession'

const pendingOrder = {
  orderId: 'e8a3d1f4-5b62-4c79-8a0e-2f6b9c1d7e33',
  trackingNumber: 'DD-202611-0012',
  customerName: 'Tran Thi Mai',
  customerPhone: '0901234567',
  packageName: 'Medical Supplies',
  weightGram: 450,
  lengthCm: 25,
  widthCm: 18,
  heightCm: 10,
  isFragile: true,
  originStation: { id: 'station-1', stationCode: 'HCM-01', name: 'District 1 Hub' },
  destinationStation: { id: 'station-2', stationCode: 'HCM-02', name: 'Thu Duc Hub' },
  createdAt: new Date(Date.now() - 30 * 60000).toISOString(),
}

const mockStations = [
  { id: 'station-1', stationCode: 'HCM-01', name: 'District 1 Hub' },
  { id: 'station-2', stationCode: 'HCM-02', name: 'Thu Duc Hub' },
]

let mock: MockAdapter

beforeEach(() => {
  session.clear()
  mock = new MockAdapter(apiClient)
  mock.onGet('/api/landing-stations').reply(200, mockStations)
  mock.onGet('/api/dispatch/pending-orders').reply(200, {
    items: [pendingOrder],
    page: 1,
    pageSize: 20,
    totalCount: 1,
    totalPages: 1,
  })
})

afterEach(() => mock.restore())

function show() {
  render(
    <ConfigProvider theme={{ token: { motion: false } }}>
      <MemoryRouter initialEntries={['/dispatch/approvals']}>
        <Routes>
          <Route path="/dispatch/approvals" element={<ApprovalQueuePage />} />
        </Routes>
      </MemoryRouter>
    </ConfigProvider>,
  )
}

it('renders the pending orders queue with order details and route', async () => {
  show()
  expect(await screen.findByText('DD-202611-0012')).toBeInTheDocument()
  expect(screen.getByText('Tran Thi Mai')).toBeInTheDocument()
  expect(screen.getByText('0901234567')).toBeInTheDocument()
  expect(screen.getByText('Medical Supplies')).toBeInTheDocument()
  expect(screen.getByText(/450 g · 25 × 18 × 10 cm · Fragile/)).toBeInTheDocument()
  expect(screen.getByText(/District 1 Hub → Thu Duc Hub/)).toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Approve' })).toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Reject' })).toBeInTheDocument()
})

it('validates date boundaries and forbids start date after end date', async () => {
  show()
  await screen.findByText('DD-202611-0012')

  const fromInput = screen.getByLabelText('Created from')
  const throughInput = screen.getByLabelText('Created through')

  fireEvent.change(fromInput, { target: { value: '2026-11-20' } })
  fireEvent.change(throughInput, { target: { value: '2026-11-10' } })

  await userEvent.click(screen.getByRole('button', { name: 'Apply filters' }))
  expect(await screen.findByText('End date must not be before start date.')).toBeInTheDocument()

  // Verify no new pending-orders request was sent with invalid date
  expect(mock.history.get.filter(r => r.url === '/api/dispatch/pending-orders')).toHaveLength(1)
})

it('applies filters and resets to default values', async () => {
  show()
  await screen.findByText('DD-202611-0012')

  const pendingOrderCalls = () => mock.history.get.filter(r => r.url === '/api/dispatch/pending-orders')
  expect(pendingOrderCalls()).toHaveLength(1)

  const fromInput = screen.getByLabelText('Created from')
  const throughInput = screen.getByLabelText('Created through')

  fireEvent.change(fromInput, { target: { value: '2026-11-01' } })
  fireEvent.change(throughInput, { target: { value: '2026-11-15' } })

  await userEvent.click(screen.getByRole('button', { name: 'Apply filters' }))

  await waitFor(() => {
    const calls = pendingOrderCalls()
    expect(calls).toHaveLength(2)
    expect(calls[1].params).toEqual({
      page: 1,
      pageSize: 20,
      originStationId: undefined,
      destinationStationId: undefined,
      fromDate: new Date('2026-11-01T00:00:00.000').toISOString(),
      toDate: new Date('2026-11-15T23:59:59.999').toISOString(),
    })
  })

  await userEvent.click(screen.getByRole('button', { name: 'Reset' }))
  await waitFor(() => {
    const calls = pendingOrderCalls()
    expect(calls).toHaveLength(3)
    expect(calls[2].params).toEqual({
      page: 1,
      pageSize: 20,
      originStationId: undefined,
      destinationStationId: undefined,
      fromDate: undefined,
      toDate: undefined,
    })
  })
})

it('executes approval flow successfully and refreshes the queue', async () => {
  mock.onPost(`/api/delivery-orders/${pendingOrder.orderId}/approve`).reply(200, {
    orderId: pendingOrder.orderId,
    trackingNumber: pendingOrder.trackingNumber,
    status: 'APPROVED',
    updatedAt: new Date().toISOString(),
  })

  show()
  await userEvent.click(await screen.findByRole('button', { name: 'Approve' }))

  expect(await screen.findByText('Approve delivery request?')).toBeInTheDocument()
  expect(screen.getByText(/The customer will be notified/)).toBeInTheDocument()

  await userEvent.click(screen.getByRole('button', { name: 'Confirm approval' }))

  await waitFor(() => {
    expect(mock.history.post).toHaveLength(1)
    expect(mock.history.post[0].url).toBe(`/api/delivery-orders/${pendingOrder.orderId}/approve`)
  })

  expect(await screen.findByText(/DD-202611-0012: APPROVED\. The queue has been refreshed\./)).toBeInTheDocument()
})

it('requires a reason before rejecting and executes rejection flow', async () => {
  mock.onPost(`/api/delivery-orders/${pendingOrder.orderId}/reject`).reply(200, {
    orderId: pendingOrder.orderId,
    trackingNumber: pendingOrder.trackingNumber,
    status: 'CANCELLED',
    updatedAt: new Date().toISOString(),
  })

  show()
  await userEvent.click(await screen.findByRole('button', { name: 'Reject' }))

  expect(await screen.findByText('Reject delivery request?')).toBeInTheDocument()
  expect(screen.getByText(/This cancels the pending request and notifies the customer\./)).toBeInTheDocument()

  // Try confirming without entering reason
  await userEvent.click(screen.getByRole('button', { name: 'Confirm rejection' }))
  expect(await screen.findByText('Enter a reason of 1 to 500 characters.')).toBeInTheDocument()
  expect(mock.history.post).toHaveLength(0)

  // Enter valid reason
  fireEvent.change(screen.getByLabelText('Reason for rejection'), {
    target: { value: ' Weather conditions unsafe for drone flight. ' },
  })

  await userEvent.click(screen.getByRole('button', { name: 'Confirm rejection' }))

  await waitFor(() => {
    expect(mock.history.post).toHaveLength(1)
    expect(mock.history.post[0].url).toBe(`/api/delivery-orders/${pendingOrder.orderId}/reject`)
    expect(JSON.parse(mock.history.post[0].data)).toEqual({
      reason: 'Weather conditions unsafe for drone flight.',
    })
  })

  expect(await screen.findByText(/DD-202611-0012: CANCELLED\. The queue has been refreshed\./)).toBeInTheDocument()
})

it('handles 409 conflict when order is already reviewed or cancelled', async () => {
  mock.onPost(`/api/delivery-orders/${pendingOrder.orderId}/approve`).reply(409, {
    detail: 'Only pending orders can be approved or rejected.',
  })

  show()
  await userEvent.click(await screen.findByRole('button', { name: 'Approve' }))
  await userEvent.click(screen.getByRole('button', { name: 'Confirm approval' }))

  expect(await screen.findByText('Only pending orders can be approved or rejected.')).toBeInTheDocument()
  // Queue should be re-queried to show latest state
  await waitFor(() => {
    expect(mock.history.get.filter(r => r.url === '/api/dispatch/pending-orders').length).toBeGreaterThanOrEqual(2)
  })
})

it('handles queue loading failure and offers retry', async () => {
  mock.resetHandlers()
  mock.onGet('/api/landing-stations').reply(200, mockStations)
  mock.onGet('/api/dispatch/pending-orders').replyOnce(500)
  mock.onGet('/api/dispatch/pending-orders').reply(200, {
    items: [pendingOrder],
    page: 1,
    pageSize: 20,
    totalCount: 1,
    totalPages: 1,
  })
  show()

  expect(await screen.findByText('Unable to reach the service. Please try again.')).toBeInTheDocument()
  const retryBtn = screen.getByRole('button', { name: 'Try again' })
  expect(retryBtn).toBeInTheDocument()

  await userEvent.click(retryBtn)
  expect(await screen.findByText('DD-202611-0012')).toBeInTheDocument()
})
