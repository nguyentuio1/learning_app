import { afterEach, beforeEach, expect, it } from 'vitest'
import { fireEvent, screen } from '@testing-library/react'
import MockAdapter from 'axios-mock-adapter'
import { apiClient } from '../src/services/apiClient'
import { session } from '../src/services/authSession'
import { StationAvailabilityPage } from '../src/features/availability/StationAvailabilityPage'
import { mount, station, stationId, selectStation } from './operationsFixtures'

let mock: MockAdapter
const url = '/api/landing-stations/' + stationId + '/availability'
const snapshot = { stationId, stationStatus: 'MAINTENANCE', totalSlots: 4, emptySlots: 2, occupiedSlots: 1, reservedSlots: 1, errorSlots: 0, isAvailableForArrival: false, isAvailableForDeparture: false, checkedAt: station.updatedAt }
beforeEach(() => { session.clear(); mock = new MockAdapter(apiClient); mock.onGet('/api/landing-stations').reply(200, [station]) })
afterEach(() => mock.restore())
it('uses server availability flags even when empty slots exist', async () => {
  mock.onGet(url).reply(200, snapshot)
  mount(<StationAvailabilityPage />, 'DISPATCHER')
  await selectStation()
  expect(await screen.findByText('Arrivals unavailable')).toBeVisible()
  expect(screen.getByText('Departures unavailable')).toBeVisible()
  expect(screen.getByText('2 empty / 4 total')).toBeVisible()
})
it('removes stale availability on a failed refresh and supports retry', async () => {
  mock.onGet(url).replyOnce(200, snapshot).onGet(url).replyOnce(503).onGet(url).reply(200, { ...snapshot, stationStatus: 'OPERATIONAL', isAvailableForArrival: true, isAvailableForDeparture: true })
  mount(<StationAvailabilityPage />)
  await selectStation()
  await screen.findByText('Arrivals unavailable')
  fireEvent.click(screen.getByRole('button', { name: 'Refresh availability' }))
  await screen.findByText('Availability is unknown until a fresh check succeeds.')
  expect(screen.queryByText('2 empty / 4 total')).not.toBeInTheDocument()
  fireEvent.click(screen.getByRole('button', { name: 'Try again' }))
  expect(await screen.findByText('Arrivals available')).toBeVisible()
})
it('shows an empty list without inventing a station or availability request', async () => {
  mock.onGet('/api/landing-stations').reply(200, [])
  mount(<StationAvailabilityPage />)
  await screen.findByText('No records match this view.')
  expect(mock.history.get).toHaveLength(1)
})
