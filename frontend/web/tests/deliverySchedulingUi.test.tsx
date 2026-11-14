// LA-138: Test delivery scheduling and drone assignment UI and integration
import { afterEach, beforeEach, expect, it } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { ConfigProvider } from 'antd'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import MockAdapter from 'axios-mock-adapter'
import dayjs from 'dayjs'
import { SchedulingPage } from '../src/features/scheduling/SchedulingPage'
import { apiClient } from '../src/services/apiClient'
import { session } from '../src/services/authSession'

const orderId = 'a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d'

const approvedOrder = {
  orderId,
  trackingNumber: 'DD-202611-0088',
  status: 'APPROVED',
  assignmentStatus: 'UNASSIGNED',
  customerName: 'Le Van An',
  customerPhone: '0987654321',
  packageName: 'Electronics',
  weightGram: 1200,
  lengthCm: 20,
  widthCm: 15,
  heightCm: 10,
  isFragile: false,
  originStation: { id: 'station-1', stationCode: 'S-01', name: 'Hub North' },
  destinationStation: { id: 'station-2', stationCode: 'S-02', name: 'Hub South' },
  createdAt: '2026-11-13T01:00:00Z',
  scheduledDepartureTime: null,
  estimatedDeliveryTime: null,
  droneSerialNumber: null,
}

const detailedOrder = {
  ...approvedOrder,
  package: {
    packageName: 'Electronics',
    weightGram: 1200,
    lengthCm: 20,
    widthCm: 15,
    heightCm: 10,
    isFragile: false,
  },
  currentMission: null,
  statusLogs: [
    { fromStatus: 'PENDING', toStatus: 'APPROVED', createdAt: '2026-11-13T01:10:00Z', remarks: 'Approved by Dispatcher' },
  ],
  updatedAt: '2026-11-13T01:10:00Z',
}

const candidateDrones = [
  {
    droneId: 'drone-eligible',
    serialNumber: 'DRN-101',
    modelName: 'Falcon-X',
    maxPayloadGram: 2500,
    maxFlightRangeKm: 30,
    currentStationId: 'station-1',
    status: 'IDLE',
    isEligible: true,
    reasonsNotEligible: [],
  },
  {
    droneId: 'drone-ineligible-payload',
    serialNumber: 'DRN-102',
    modelName: 'Sparrow-Light',
    maxPayloadGram: 800,
    maxFlightRangeKm: 20,
    currentStationId: 'station-1',
    status: 'IDLE',
    isEligible: false,
    reasonsNotEligible: ['PAYLOAD_EXCEEDED'],
  },
  {
    droneId: 'drone-ineligible-status',
    serialNumber: 'DRN-103',
    modelName: 'Falcon-Heavy',
    maxPayloadGram: 5000,
    maxFlightRangeKm: 50,
    currentStationId: 'station-1',
    status: 'MAINTENANCE',
    isEligible: false,
    reasonsNotEligible: ['NOT_IDLE'],
  },
]

const mockStations = [
  { id: 'station-1', stationCode: 'S-01', name: 'Hub North' },
  { id: 'station-2', stationCode: 'S-02', name: 'Hub South' },
]

let mock: MockAdapter

beforeEach(() => {
  session.clear()
  mock = new MockAdapter(apiClient)
  mock.onGet('/api/landing-stations').reply(200, mockStations)
  mock.onGet('/api/dispatch/approved-orders').reply(200, {
    items: [approvedOrder],
    page: 1,
    pageSize: 20,
    totalCount: 1,
    totalPages: 1,
  })
  mock.onGet(`/api/dispatch/orders/${orderId}`).reply(200, detailedOrder)
  mock.onGet(`/api/delivery-orders/${orderId}/available-drones`).reply(200, candidateDrones)
})

afterEach(() => mock.restore())

function show(path = '/dispatcher/scheduling') {
  render(
    <ConfigProvider theme={{ token: { motion: false } }}>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route path="/dispatcher/scheduling" element={<SchedulingPage />} />
          <Route path="/dispatcher/scheduling/:id" element={<SchedulingPage />} />
        </Routes>
      </MemoryRouter>
    </ConfigProvider>,
  )
}

it('renders approved orders queue with routing, package and assignment status', async () => {
  show()
  expect(await screen.findByText('DD-202611-0088')).toBeInTheDocument()
  expect(screen.getByText('Electronics')).toBeInTheDocument()
  expect(screen.getByText('1200 g')).toBeInTheDocument()
  expect(screen.getByText(/Hub North → Hub South/)).toBeInTheDocument()
  expect(screen.getByText('UNASSIGNED')).toBeInTheDocument()
  expect(screen.getByText('Not scheduled')).toBeInTheDocument()
})

it('validates date filters and prevents from date after through date', async () => {
  show()
  await screen.findByText('DD-202611-0088')

  const fromInput = screen.getByLabelText('Created from')
  const throughInput = screen.getByLabelText('Created through')

  fireEvent.change(fromInput, { target: { value: '2026-11-20' } })
  fireEvent.change(throughInput, { target: { value: '2026-11-10' } })

  await userEvent.click(screen.getByRole('button', { name: 'Apply filters' }))
  expect(await screen.findByText('End date must not be before start date.')).toBeInTheDocument()

  // Reset clears filters
  await userEvent.click(screen.getByRole('button', { name: 'Reset' }))
  await waitFor(() => {
    expect(screen.queryByText('End date must not be before start date.')).not.toBeInTheDocument()
  })
})

it('displays invalid order ID error when id format is invalid', async () => {
  show('/dispatcher/scheduling/invalid-uuid-123')
  expect(await screen.findByText('Invalid order ID.')).toBeInTheDocument()
})

it('validates minimum 15 minute departure buffer before saving departure', async () => {
  show(`/dispatcher/scheduling/${orderId}`)
  expect(await screen.findByText('01 · Schedule departure')).toBeInTheDocument()
  expect(screen.getByText(/Save a departure first to check suitable drones/)).toBeInTheDocument()

  // Submit without selecting departure
  await userEvent.click(screen.getByRole('button', { name: 'Save departure' }))
  expect(await screen.findByText('Choose a departure time.')).toBeInTheDocument()
  expect(mock.history.put).toHaveLength(0)

  // Submit a departure less than 15 minutes from now
  const tooSoon = dayjs().add(10, 'minute').format('YYYY-MM-DD HH:mm')
  const datePickerInput = screen.getByPlaceholderText(/Select date/i)
  fireEvent.mouseDown(datePickerInput)
  fireEvent.change(datePickerInput, { target: { value: tooSoon } })
  fireEvent.keyDown(datePickerInput, { key: 'Enter', code: 'Enter' })

  await userEvent.click(screen.getByRole('button', { name: 'Save departure' }))
  expect(await screen.findByText('Choose a departure at least 15 minutes from now.')).toBeInTheDocument()
  expect(mock.history.put).toHaveLength(0)
})

it('schedules departure successfully and reveals candidate drone picker', async () => {
  const departureTime = dayjs().add(30, 'minute').startOf('minute')
  const expectedDepartureIso = departureTime.toDate().toISOString()
  const futureArrival = departureTime.add(20, 'minute').toISOString()

  let scheduled = false
  mock.onGet(`/api/dispatch/orders/${orderId}`).reply(() => [
    200,
    scheduled
      ? {
          ...detailedOrder,
          scheduledDepartureTime: expectedDepartureIso,
          estimatedDeliveryTime: futureArrival,
        }
      : detailedOrder,
  ])

  mock.onPut(`/api/delivery-orders/${orderId}/schedule`).reply(() => {
    scheduled = true
    return [
      200,
      {
        orderId,
        scheduledDepartureTime: expectedDepartureIso,
        estimatedDeliveryTime: futureArrival,
        estimatedFlightMinutes: 20,
      },
    ]
  })

  show(`/dispatcher/scheduling/${orderId}`)
  expect(await screen.findByText('01 · Schedule departure')).toBeInTheDocument()
  expect(screen.getByText(/Save a departure first to check suitable drones/)).toBeInTheDocument()
  expect(screen.queryByText('DRN-101 · Falcon-X')).not.toBeInTheDocument()
  expect(mock.history.get.filter(r => r.url === `/api/delivery-orders/${orderId}/available-drones`)).toHaveLength(0)

  const datePickerInput = screen.getByPlaceholderText(/Select date/i)
  fireEvent.mouseDown(datePickerInput)
  fireEvent.change(datePickerInput, {
    target: { value: departureTime.format('YYYY-MM-DD HH:mm') },
  })
  fireEvent.keyDown(datePickerInput, { key: 'Enter', code: 'Enter' })

  await userEvent.click(screen.getByRole('button', { name: 'Save departure' }))

  await waitFor(() => {
    const putCalls = mock.history.put.filter(r => r.url === `/api/delivery-orders/${orderId}/schedule`)
    expect(putCalls).toHaveLength(1)
    expect(JSON.parse(putCalls[0].data)).toEqual({
      scheduledDepartureTime: expectedDepartureIso,
    })
  })

  // Candidates section is now loaded after saving and refreshing the order
  expect(await screen.findByText(/Departure saved\. Estimated flight: 20 minutes\./)).toBeInTheDocument()
  expect(await screen.findByText('DRN-101 · Falcon-X')).toBeInTheDocument()
  expect(screen.queryByText(/Save a departure first to check suitable drones/)).not.toBeInTheDocument()
  expect(mock.history.get.filter(r => r.url === `/api/delivery-orders/${orderId}/available-drones`)).toHaveLength(1)
})

it('filters drone candidates, disallows ineligible drones, and assigns eligible drone', async () => {
  const futureDeparture = dayjs().add(30, 'minute').toISOString()
  const scheduledOrder = {
    ...detailedOrder,
    scheduledDepartureTime: futureDeparture,
    estimatedDeliveryTime: dayjs().add(50, 'minute').toISOString(),
  }
  mock.onGet(`/api/dispatch/orders/${orderId}`).reply(200, scheduledOrder)

  mock.onPost(`/api/delivery-orders/${orderId}/flight-mission`).reply(200, {
    id: 'mission-uuid-111',
    orderId,
    droneId: 'drone-eligible',
    flightStatus: 'PLANNED',
    destinationSlotId: 'slot-uuid-999',
    scheduledDepartureTime: futureDeparture,
  })

  show(`/dispatcher/scheduling/${orderId}`)
  expect(await screen.findByText('DRN-101 · Falcon-X')).toBeInTheDocument()
  expect(screen.getByText('DRN-102 · Sparrow-Light')).toBeInTheDocument()
  expect(screen.getByText('Package exceeds payload capacity')).toBeInTheDocument()
  expect(screen.getByText('Drone is not idle')).toBeInTheDocument()

  // Eligible drone radio can be selected
  const eligibleRadio = screen.getByRole('radio', { name: /DRN-101/ })
  expect(eligibleRadio).toBeEnabled()

  // Ineligible radio is disabled
  const ineligibleRadio = screen.getByRole('radio', { name: /DRN-102/ })
  expect(ineligibleRadio).toBeDisabled()

  await userEvent.click(eligibleRadio)

  const assignBtn = screen.getByRole('button', { name: 'Assign selected drone' })
  expect(assignBtn).toBeEnabled()
  await userEvent.click(assignBtn)

  // Confirmation modal
  expect(await screen.findByText('Plan this flight mission?')).toBeInTheDocument()
  expect(screen.getByText(/This reserves an available destination slot/)).toBeInTheDocument()

  const confirmOkBtn = screen.getByRole('button', { name: 'OK' })
  await userEvent.click(confirmOkBtn)

  await waitFor(() => {
    expect(mock.history.post.filter(r => r.url === `/api/delivery-orders/${orderId}/flight-mission`)).toHaveLength(1)
    expect(JSON.parse(mock.history.post[0].data)).toEqual({ droneId: 'drone-eligible' })
  })

  expect(await screen.findByText(/Drone assigned\. The current mission and delivery queue have been refreshed\./)).toBeInTheDocument()
})

it('displays notice and disables scheduling when order is no longer in APPROVED status', async () => {
  mock.onGet(`/api/dispatch/orders/${orderId}`).reply(200, {
    ...detailedOrder,
    status: 'CANCELLED',
  })

  show(`/dispatcher/scheduling/${orderId}`)
  expect(await screen.findByText('This delivery is no longer approved. Scheduling and assignment are unavailable.')).toBeInTheDocument()
  expect(screen.queryByRole('button', { name: 'Save departure' })).not.toBeInTheDocument()
})
