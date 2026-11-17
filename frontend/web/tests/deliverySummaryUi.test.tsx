// LA-164: Test delivery summarization web UI and integration
import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { ConfigProvider } from 'antd'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import MockAdapter from 'axios-mock-adapter'
import { DeliverySummaryPage } from '../src/features/deliverySummary/DeliverySummaryPage'
import { generateDeliverySummary } from '../src/features/deliverySummary/summaryApi'
import { apiClient } from '../src/services/apiClient'
import { session } from '../src/services/authSession'

let currentRole = 'CUSTOMER'

vi.mock('../src/hooks/useAuth', () => ({
  useAuth: () => ({
    user: {
      id: 'user-sum-1',
      email: 'summary@smartdrone.local',
      fullName: 'Summary Tester',
      role: { code: currentRole, name: currentRole },
    },
  }),
}))

const orderId = 'e8a3d1f4-5b62-4c79-8a0e-2f6b9c1d7e33'
const orderPath = `/api/delivery-orders/${orderId}`
const summaryPath = `${orderPath}/ai-summary`
const trackingSnapshotPath = `/api/delivery-orders/${orderId}/tracking`

let mock: MockAdapter

beforeEach(() => {
  currentRole = 'CUSTOMER'
  session.clear()
  mock = new MockAdapter(apiClient)
})

afterEach(() => {
  mock.restore()
})

function renderSummary(initialPath = '/delivery-summary') {
  return render(
    <ConfigProvider theme={{ token: { motion: false } }}>
      <MemoryRouter initialEntries={[initialPath]}>
        <Routes>
          <Route path="/delivery-summary" element={<DeliverySummaryPage />} />
          <Route path="/delivery-summary/:id" element={<DeliverySummaryPage />} />
        </Routes>
      </MemoryRouter>
    </ConfigProvider>,
  )
}

it('shows invalid order ID alert for malformed route id', () => {
  renderSummary('/delivery-summary/not-a-uuid')
  expect(screen.getByText('Invalid order ID')).toBeInTheDocument()
})

it('lets a customer select an order, view status history, and generate a summary', async () => {
  const user = userEvent.setup()
  mock.onGet('/api/delivery-orders').reply(200, {
    items: [{ id: orderId, trackingNumber: 'DD-202611-0164' }],
    page: 1,
    pageSize: 20,
    totalCount: 1,
    totalPages: 1,
  })
  mock.onGet(orderPath).reply(200, {
    id: orderId,
    trackingNumber: 'DD-202611-0164',
    status: 'ARRIVED',
    originStationName: 'Station Alpha',
    destinationStationName: 'Station Beta',
    statusLogs: [
      { fromStatus: 'PENDING', toStatus: 'APPROVED', createdAt: '2026-11-16T08:00:00Z' },
      { fromStatus: 'IN_TRANSIT', toStatus: 'ARRIVED', createdAt: '2026-11-16T08:24:00Z' },
    ],
  })
  mock.onPost(summaryPath).reply(200, {
    orderId,
    outcome: 'GENERATED',
    text: 'The order was approved and arrived at the destination station awaiting pickup.',
    generatedAt: '2026-11-16T08:25:00Z',
  })

  renderSummary('/delivery-summary')

  const select = await screen.findByRole('combobox', { name: 'Your delivery order' })
  await user.click(select)
  await user.click(await screen.findByText('DD-202611-0164'))

  expect(await screen.findByRole('heading', { name: 'DD-202611-0164' })).toBeInTheDocument()
  expect(screen.getByText('Station Alpha')).toBeInTheDocument()
  expect(screen.getByText('Station Beta')).toBeInTheDocument()
  expect(screen.getByRole('link', { name: 'Open order details' })).toHaveAttribute('href', `/orders/${orderId}`)

  await user.click(screen.getByRole('button', { name: 'Generate summary' }))

  expect(await screen.findByText('AI-generated summary')).toBeInTheDocument()
  expect(
    screen.getByText('The order was approved and arrived at the destination station awaiting pickup.'),
  ).toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Generate again' })).toBeInTheDocument()
})

it('renders NO_HISTORY outcome and empty customer timeline message when no logs exist', async () => {
  const user = userEvent.setup()
  mock.onGet(orderPath).reply(200, {
    id: orderId,
    trackingNumber: 'DD-202611-0165',
    status: 'PENDING',
    originStationName: 'Station Alpha',
    destinationStationName: 'Station Beta',
    statusLogs: [],
  })
  mock.onPost(summaryPath).reply(200, {
    orderId,
    outcome: 'NO_HISTORY',
    text: null,
    generatedAt: null,
  })

  renderSummary(`/delivery-summary/${orderId}`)

  expect(await screen.findByText('No status history has been recorded yet.')).toBeInTheDocument()
  await user.click(screen.getByRole('button', { name: 'Generate summary' }))

  expect(await screen.findByText('There is no recorded history to summarize.')).toBeInTheDocument()
})

it('loads dispatcher record from tracking snapshot and links to delivery tracking', async () => {
  currentRole = 'DISPATCHER'
  mock.onGet(trackingSnapshotPath).reply(200, {
    orderId,
    trackingNumber: 'DD-202611-0166',
    status: 'IN_TRANSIT',
    originStation: { id: 's1', name: 'North Hub', latitude: 10.7, longitude: 106.6 },
    destinationStation: { id: 's2', name: 'South Hub', latitude: 10.8, longitude: 106.7 },
    drone: null,
    latestPosition: null,
    eta: null,
  })

  renderSummary(`/delivery-summary/${orderId}`)

  expect(await screen.findByRole('heading', { name: 'DD-202611-0166' })).toBeInTheDocument()
  expect(screen.getByText('North Hub')).toBeInTheDocument()
  expect(screen.getByText('South Hub')).toBeInTheDocument()
  expect(screen.getByRole('link', { name: 'View delivery tracking' })).toHaveAttribute(
    'href',
    `/delivery-tracking/${orderId}`,
  )
})

it('handles 409 source conflict, 429 rate-limit cooldown, 503 unavailable, 504 timeout, and 404 access loss', async () => {
  const user = userEvent.setup()
  mock
    .onGet(orderPath)
    .replyOnce(200, {
      id: orderId,
      trackingNumber: 'DD-202611-0167',
      status: 'APPROVED',
      originStationName: 'Station Alpha',
      destinationStationName: 'Station Beta',
      statusLogs: [{ fromStatus: 'PENDING', toStatus: 'APPROVED', createdAt: '2026-11-16T08:00:00Z' }],
    })
    .onGet(orderPath)
    .reply(200, {
      id: orderId,
      trackingNumber: 'DD-202611-0167',
      status: 'IN_TRANSIT',
      originStationName: 'Station Alpha',
      destinationStationName: 'Station Beta',
      statusLogs: [{ fromStatus: 'APPROVED', toStatus: 'IN_TRANSIT', createdAt: '2026-11-16T08:10:00Z' }],
    })

  mock
    .onPost(summaryPath)
    .replyOnce(409)
    .onPost(summaryPath)
    .replyOnce(503)
    .onPost(summaryPath)
    .replyOnce(504)
    .onPost(summaryPath)
    .replyOnce(429, {}, { 'retry-after': '1' })
    .onPost(summaryPath)
    .replyOnce(404)

  renderSummary(`/delivery-summary/${orderId}`)
  const generateBtn = await screen.findByRole('button', { name: 'Generate summary' })

  // 409 Conflict reloads record and warns user
  await user.click(generateBtn)
  expect(
    await screen.findByText(
      'The delivery changed while the summary was being generated. Review the refreshed record before trying again.',
    ),
  ).toBeInTheDocument()
  expect(await screen.findByText('In Transit', { selector: 'dd' })).toBeInTheDocument()

  // 503 Service Unavailable
  await user.click(screen.getByRole('button', { name: 'Generate summary' }))
  expect(await screen.findByText('The summary service is temporarily unavailable.')).toBeInTheDocument()

  // 504 Gateway Timeout
  await user.click(screen.getByRole('button', { name: 'Generate summary' }))
  expect(await screen.findByText('Summary generation took too long. You can try again.')).toBeInTheDocument()

  // 429 Too Many Requests disables button during cooldown
  await user.click(screen.getByRole('button', { name: 'Generate summary' }))
  expect(
    await screen.findByText('A summary request is already in progress. Please wait before trying again.'),
  ).toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Generate summary' })).toBeDisabled()

  // Wait for 1s cooldown to expire, then trigger 404 access loss
  await new Promise(resolve => setTimeout(resolve, 1100))
  await user.click(screen.getByRole('button', { name: 'Generate summary' }))
  expect(await screen.findByText('This delivery summary is unavailable to your account.')).toBeInTheDocument()
})

it('rejects response when returned orderId does not match requested id', async () => {
  mock.onPost(summaryPath).reply(200, {
    orderId: '99999999-2222-4333-8444-555555555555',
    outcome: 'NO_HISTORY',
    text: null,
    generatedAt: null,
  })

  await expect(generateDeliverySummary(orderId, new AbortController().signal)).rejects.toThrow(
    'Unexpected delivery summary',
  )
})
