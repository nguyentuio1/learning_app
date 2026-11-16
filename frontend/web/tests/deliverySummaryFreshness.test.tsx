import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { fireEvent, render, screen } from '@testing-library/react'
import { ConfigProvider } from 'antd'
import MockAdapter from 'axios-mock-adapter'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { DeliverySummaryPage } from '../src/features/deliverySummary/DeliverySummaryPage'
import { apiClient } from '../src/services/apiClient'

vi.mock('../src/hooks/useAuth', () => ({
  useAuth: () => ({ user: { id: 'customer-1', role: { code: 'CUSTOMER' } } }),
}))

const orderId = 'e8a3d1f4-5b62-4c79-8a0e-2f6b9c1d7e33'
const orderPath = '/api/delivery-orders/' + orderId
const summaryPath = orderPath + '/ai-summary'
const approvedLog = { fromStatus: 'PENDING', toStatus: 'APPROVED', createdAt: '2026-11-16T08:00:00Z' }
const arrivedLog = { fromStatus: 'IN_TRANSIT', toStatus: 'ARRIVED', createdAt: '2026-11-16T08:24:00Z' }
const order = {
  id: orderId,
  trackingNumber: 'DD-202611-0012',
  status: 'APPROVED',
  originStationName: 'Hub North',
  destinationStationName: 'Hub South',
  statusLogs: [approvedLog],
}
const summary = {
  orderId,
  outcome: 'GENERATED',
  text: 'The delivery arrived at Hub South and is awaiting pickup.',
  generatedAt: '2026-11-16T08:25:00Z',
}

let mock: MockAdapter

beforeEach(() => {
  mock = new MockAdapter(apiClient)
  mock.onPost(summaryPath).reply(200, summary)
})

afterEach(() => mock.restore())

function show() {
  render(
    <ConfigProvider theme={{ token: { motion: false } }}>
      <MemoryRouter initialEntries={['/delivery-summary/' + orderId]}>
        <Routes>
          <Route path="/delivery-summary/:id" element={<DeliverySummaryPage />} />
        </Routes>
      </MemoryRouter>
    </ConfigProvider>,
  )
}

it('refreshes the record when an order arrives before the user generates its summary', async () => {
  let currentOrder = order
  mock.onGet(orderPath).reply(() => [200, currentOrder])
  show()
  const generate = await screen.findByRole('button', { name: 'Generate summary' })
  expect(screen.getByText('Approved', { selector: 'dd' })).toBeVisible()

  currentOrder = { ...order, status: 'ARRIVED', statusLogs: [approvedLog, arrivedLog] }
  fireEvent.click(generate)

  expect(await screen.findByText(summary.text)).toBeVisible()
  expect(screen.getByText('Arrived', { selector: 'dd' })).toBeVisible()
  expect(screen.queryByText('Approved', { selector: 'dd' })).not.toBeInTheDocument()
  expect(mock.history.get.map(request => request.url)).toEqual([orderPath, orderPath])
  expect(mock.history.post).toHaveLength(1)
})

it('hides the summary when the record refresh is denied and clears it before retrying', async () => {
  mock.onGet(orderPath).replyOnce(200, order)
  mock.onGet(orderPath).replyOnce(403)
  mock.onGet(orderPath).reply(200, order)
  show()

  fireEvent.click(await screen.findByRole('button', { name: 'Generate summary' }))

  expect(await screen.findByText('You do not have permission to perform this action.')).toBeVisible()
  expect(screen.queryByText(summary.text)).not.toBeInTheDocument()
  expect(screen.queryByRole('heading', { name: 'Delivery record' })).not.toBeInTheDocument()

  fireEvent.click(screen.getByRole('button', { name: 'Try again' }))
  expect(await screen.findByText('Approved', { selector: 'dd' })).toBeVisible()
  expect(screen.queryByText(summary.text)).not.toBeInTheDocument()
  expect(mock.history.post).toHaveLength(1)
})
