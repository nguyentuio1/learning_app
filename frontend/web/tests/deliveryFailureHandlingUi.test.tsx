// LA-142: Test failed delivery handling UI and integration
import { afterEach, beforeEach, expect, it } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { ConfigProvider } from 'antd'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import MockAdapter from 'axios-mock-adapter'
import { FailureHandlingPage } from '../src/features/failures/FailureHandlingPage'
import { apiClient } from '../src/services/apiClient'
import { session } from '../src/services/authSession'

const failedOrderId = 'b2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e'

const failedOrderInQueue = {
  orderId: failedOrderId,
  trackingNumber: 'DD-202611-0099',
  status: 'FAILED',
  packageName: 'Medical Vaccine',
  originStation: { id: 'station-1', stationCode: 'S-01', name: 'Hub North' },
  destinationStation: { id: 'station-2', stationCode: 'S-02', name: 'Hub South' },
  updatedAt: '2026-11-13T04:00:00Z',
  failureLogId: 101,
  failureType: 'EMERGENCY_LANDING' as const,
  failureReason: 'Severe rotor vibration detected',
  failedAt: '2026-11-13T03:55:00Z',
  resolutionStatus: 'UNRESOLVED' as const,
  lastResolutionAction: null,
  resolvedAt: null,
}

const detailedFailureOrder = {
  ...failedOrderInQueue,
  package: {
    packageName: 'Medical Vaccine',
    weightGram: 600,
    lengthCm: 15,
    widthCm: 12,
    heightCm: 8,
    isFragile: true,
  },
  scheduledDepartureTime: '2026-11-13T03:30:00Z',
  estimatedDeliveryTime: '2026-11-13T04:00:00Z',
  actualDepartureTime: '2026-11-13T03:32:00Z',
  actualDeliveryTime: null,
  assignmentStatus: 'ASSIGNED' as const,
  currentMission: {
    id: 'mission-failed-1',
    flightStatus: 'EMERGENCY_LANDING',
    drone: { id: 'drone-1', serialNumber: 'DRN-201', modelName: 'Falcon-Med', status: 'MAINTENANCE' },
    startSlotId: 'slot-1',
    destinationSlotId: 'slot-2',
    startTime: '2026-11-13T03:32:00Z',
    endTime: null,
  },
  statusLogs: [
    { fromStatus: 'IN_TRANSIT', toStatus: 'FAILED', createdAt: '2026-11-13T03:55:00Z', remarks: 'Emergency landing executed' },
  ],
  createdAt: '2026-11-13T02:00:00Z',
  latestFailure: {
    failureLogId: 101,
    failureType: 'EMERGENCY_LANDING' as const,
    failureReason: 'Severe rotor vibration detected',
    failedAt: '2026-11-13T03:55:00Z',
    resolutionStatus: 'UNRESOLVED' as const,
    lastResolutionAction: null,
    resolvedAt: null,
  },
  returnToOriginStatus: 'NOT_REQUESTED' as const,
  parcelLocation: { stationId: 'station-1', slotId: null },
  failureHistory: [
    {
      failureLogId: 101,
      failureType: 'EMERGENCY_LANDING' as const,
      failureReason: 'Severe rotor vibration detected',
      failedAt: '2026-11-13T03:55:00Z',
      resolutionStatus: 'UNRESOLVED' as const,
      lastResolutionAction: null,
      resolvedAt: null,
    },
  ],
  resolutionHistory: [],
  resolutionHistoryComplete: true,
}

const inFlightOrderId = 'c3d4e5f6-a7b8-4c9d-8e0f-1a2b3c4d5e6f'

const activeInFlightOrders = [
  {
    orderId: inFlightOrderId,
    trackingNumber: 'DD-202611-0077',
    orderStatus: 'IN_TRANSIT',
    missionId: 'mission-flight-77',
    flightStatus: 'IN_AIR',
    droneSerialNumber: 'DRN-301',
    originStationId: 'station-1',
    destinationStationId: 'station-2',
    scheduledDepartureTime: '2026-11-13T05:00:00Z',
    estimatedDeliveryTime: '2026-11-13T05:30:00Z',
    lastTelemetryAt: '2026-11-13T05:10:00Z',
    telemetryStale: false,
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
  mock.onGet('/api/dispatch/failed-orders').reply(200, {
    items: [failedOrderInQueue],
    page: 1,
    pageSize: 20,
    totalCount: 1,
    totalPages: 1,
  })
  mock.onGet(`/api/dispatch/failed-orders/${failedOrderId}`).reply(200, detailedFailureOrder)
  mock.onGet('/api/dispatch/active-deliveries').reply(200, activeInFlightOrders)
})

afterEach(() => mock.restore())

function show(path = '/dispatcher/failures') {
  render(
    <ConfigProvider theme={{ token: { motion: false } }}>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route path="/dispatcher/failures" element={<FailureHandlingPage />} />
          <Route path="/dispatcher/failures/:id" element={<FailureHandlingPage />} />
        </Routes>
      </MemoryRouter>
    </ConfigProvider>,
  )
}

it('renders failed orders queue with failure episode details and resolution status', async () => {
  show()
  expect(await screen.findByText('DD-202611-0099')).toBeInTheDocument()
  expect(screen.getByText('Medical Vaccine')).toBeInTheDocument()
  expect(screen.getByText('Severe rotor vibration detected')).toBeInTheDocument()
  expect(screen.getByText(/Hub North → Hub South/)).toBeInTheDocument()
  expect(screen.getByText('UNRESOLVED')).toBeInTheDocument()
})

it('validates date boundaries on queue filter', async () => {
  show()
  await screen.findByText('DD-202611-0099')

  const fromInput = screen.getByLabelText('Failed from')
  const throughInput = screen.getByLabelText('Failed through')

  fireEvent.change(fromInput, { target: { value: '2026-11-25' } })
  fireEvent.change(throughInput, { target: { value: '2026-11-05' } })

  fireEvent.click(screen.getByRole('button', { name: 'Apply filters' }))
  expect(await screen.findByText('End date must not be before start date.')).toBeInTheDocument()
})

it('opens incident report form, validates fields, and reports an in-flight incident', async () => {
  mock.onPost(`/api/delivery-orders/${inFlightOrderId}/fail`).reply(200, {
    orderId: inFlightOrderId,
    trackingNumber: 'DD-202611-0077',
    status: 'FAILED',
    updatedAt: new Date().toISOString(),
  })
  mock.onGet(`/api/dispatch/failed-orders/${inFlightOrderId}`).reply(200, {
    ...detailedFailureOrder,
    orderId: inFlightOrderId,
    trackingNumber: 'DD-202611-0077',
  })

  show()
  fireEvent.click(await screen.findByRole('button', { name: 'Report an incident' }))

  expect(await screen.findByText('Report an in-flight incident')).toBeInTheDocument()

  // Wait until flights data is loaded and submit button becomes enabled
  const submitBtn = await screen.findByRole('button', { name: 'Review incident' })
  await waitFor(() => expect(submitBtn).toBeEnabled())

  // Click submit without selecting delivery
  fireEvent.click(submitBtn)
  expect(await screen.findByText('Choose a delivery.')).toBeInTheDocument()

  // Select in-transit delivery
  const deliverySelect = screen.getByLabelText('In-transit delivery')
  fireEvent.mouseDown(deliverySelect)
  const option = await screen.findByText('DD-202611-0077 · DRN-301', { selector: '.ant-select-item-option-content' })
  fireEvent.click(option)

  // Select failure type
  const typeSelect = screen.getByLabelText('Failure type')
  fireEvent.mouseDown(typeSelect)
  const typeOption = await screen.findByText('Hardware fault', { selector: '.ant-select-item-option-content' })
  fireEvent.click(typeOption)

  // Enter reason
  fireEvent.change(screen.getByLabelText('Incident reason'), {
    target: { value: ' Motor #3 telemetry lost and propeller disengaged ' },
  })

  fireEvent.click(screen.getByRole('button', { name: 'Review incident' }))

  // Modal confirmation
  expect(await screen.findByText('Record this failure?')).toBeInTheDocument()
  const okBtn = screen.getByRole('button', { name: 'OK' })
  fireEvent.click(okBtn)

  await waitFor(() => {
    expect(mock.history.post.filter(r => r.url === `/api/delivery-orders/${inFlightOrderId}/fail`)).toHaveLength(1)
    expect(JSON.parse(mock.history.post[0].data)).toEqual({
      failureType: 'HARDWARE_FAULT',
      reason: 'Motor #3 telemetry lost and propeller disengaged',
    })
  })

  // Navigates to the valid UUID detail route and loads refreshed failure record
  expect(await screen.findByText('Resolve this delivery')).toBeInTheDocument()
  expect(screen.getByText('DD-202611-0077')).toBeInTheDocument()
  expect(mock.history.get.filter(r => r.url === `/api/dispatch/failed-orders/${inFlightOrderId}`)).toHaveLength(1)
}, 30000)

it('resolves failed delivery with RESCHEDULE and requires package verification', async () => {
  let resolved = false
  mock.onGet(`/api/dispatch/failed-orders/${failedOrderId}`).reply(() => [
    200,
    resolved
      ? {
          ...detailedFailureOrder,
          status: 'APPROVED',
          latestFailure: {
            ...detailedFailureOrder.latestFailure,
            resolutionStatus: 'RESOLVED',
            lastResolutionAction: 'RESCHEDULE',
          },
        }
      : detailedFailureOrder,
  ])
  mock.onPost(`/api/delivery-orders/${failedOrderId}/resolve-failure`).reply(() => {
    resolved = true
    return [
      200,
      {
        orderId: failedOrderId,
        trackingNumber: 'DD-202611-0099',
        status: 'APPROVED',
        updatedAt: new Date().toISOString(),
      },
    ]
  })

  show(`/dispatcher/failures/${failedOrderId}`)
  expect(await screen.findByText('Resolve this delivery')).toBeInTheDocument()

  const resolutionSelect = screen.getByLabelText('Resolution')
  fireEvent.mouseDown(resolutionSelect)
  const rescheduleOption = await screen.findByText('Prepare for redelivery')
  fireEvent.click(rescheduleOption)

  // Requires checking package verification checkbox
  expect(await screen.findByText('Package has been physically verified')).toBeInTheDocument()

  // Try submitting without checking the box
  fireEvent.change(screen.getByLabelText('Resolution note'), {
    target: { value: ' Parcel recovered from emergency landing site in good shape ' },
  })
  fireEvent.click(screen.getByRole('button', { name: 'Review resolution' }))
  expect(await screen.findByText('Confirm that the package has been verified.')).toBeInTheDocument()
  expect(mock.history.post).toHaveLength(0)

  // Check the verification box
  fireEvent.click(screen.getByRole('checkbox', { name: 'Package has been physically verified' }))
  fireEvent.click(screen.getByRole('button', { name: 'Review resolution' }))

  // Modal confirmation
  expect(await screen.findByText('Apply this resolution?')).toBeInTheDocument()
  const confirmBtn = screen.getByRole('button', { name: 'OK' })
  fireEvent.click(confirmBtn)

  await waitFor(() => {
    expect(mock.history.post.filter(r => r.url === `/api/delivery-orders/${failedOrderId}/resolve-failure`)).toHaveLength(1)
    expect(JSON.parse(mock.history.post[0].data)).toEqual({
      action: 'RESCHEDULE',
      note: 'Parcel recovered from emergency landing site in good shape',
      packageVerified: true,
    })
  })

  expect(await screen.findByText(/Delivery approved for redelivery/)).toBeInTheDocument()
  expect(await screen.findByText('This failure is no longer awaiting a resolution.')).toBeInTheDocument()
  expect(mock.history.get.filter(r => r.url === `/api/dispatch/failed-orders/${failedOrderId}`)).toHaveLength(2)
})

it('blocks RESCHEDULE resolution when parcel is still in destination slot', async () => {
  mock.onGet(`/api/dispatch/failed-orders/${failedOrderId}`).reply(200, {
    ...detailedFailureOrder,
    parcelLocation: { stationId: 'station-2', slotId: 'dest-slot-9' },
  })

  show(`/dispatcher/failures/${failedOrderId}`)
  expect(await screen.findByText('The parcel is still in a destination slot. The station operator must clear it before redelivery.')).toBeInTheDocument()

  const resolutionSelect = screen.getByLabelText('Resolution')
  fireEvent.mouseDown(resolutionSelect)

  // The option "Prepare for redelivery" is disabled
  const disabledOption = await screen.findByText('Prepare for redelivery')
  expect(disabledOption.closest('.ant-select-item-option')).toHaveClass('ant-select-item-option-disabled')
})

it('redirects to queue and shows alert when order ID does not exist (404)', async () => {
  mock.onGet('/api/dispatch/failed-orders/a1b2c3d4-0000-0000-0000-000000000000').reply(404)

  show('/dispatcher/failures/a1b2c3d4-0000-0000-0000-000000000000')

  expect(await screen.findByText('The selected failure record could not be found. Choose another delivery.')).toBeInTheDocument()
})
