import { afterEach, beforeEach, expect, it } from 'vitest'
import { fireEvent, screen, waitFor } from '@testing-library/react'
import MockAdapter from 'axios-mock-adapter'
import { apiClient } from '../src/services/apiClient'
import { session } from '../src/services/authSession'
import { StationSlotsPage } from '../src/features/slots/StationSlotsPage'
import { mount, station, stationId, selectStation } from './operationsFixtures'

let mock: MockAdapter
const slots = ['EMPTY', 'OCCUPIED', 'RESERVED', 'ERROR'].map((status, index) => ({ id: 'slot-' + index, stationId, slotNumber: index + 1, status, lastUpdated: station.updatedAt, currentOrderId: index === 1 ? 'order' : null, currentMissionId: index === 2 ? 'mission' : null }))
beforeEach(() => {
  session.clear(); mock = new MockAdapter(apiClient)
  mock.onGet('/api/landing-stations').reply(200, [station])
  mock.onGet('/api/landing-stations/' + stationId).reply(200, station)
  mock.onGet('/api/landing-stations/' + stationId + '/slots').reply(200, slots)
})
afterEach(() => mock.restore())
it('disables removal of occupied and reserved slots', async () => {
  mount(<StationSlotsPage />)
  await selectStation()
  const buttons = await screen.findAllByRole('button', { name: 'Remove slot' })
  expect(buttons[0]).toBeEnabled()
  expect(buttons[1]).toBeDisabled()
  expect(buttons[2]).toBeDisabled()
  expect(buttons[3]).toBeEnabled()
})
it('allows assigned operators to change availability but not add or delete slots', async () => {
  mount(<StationSlotsPage />, 'STATION_OPERATOR')
  await selectStation()
  expect(await screen.findByRole('button', { name: 'Mark unavailable' })).toBeEnabled()
  expect(screen.queryByRole('button', { name: 'Add slot' })).not.toBeInTheDocument()
  expect(screen.queryByRole('button', { name: 'Remove slot' })).not.toBeInTheDocument()
})
it('keeps dispatcher inventory read-only', async () => {
  mount(<StationSlotsPage />, 'DISPATCHER')
  await selectStation()
  await screen.findByText('Slot 1')
  expect(screen.queryByRole('button', { name: 'Mark unavailable' })).not.toBeInTheDocument()
  expect(screen.queryByRole('button', { name: 'Add slot' })).not.toBeInTheDocument()
})
it('uses the slot contract and refreshes a rejected stale action before retry', async () => {
  mock.onPut('/api/station-slots/slot-0/status').reply(409, { detail: 'Slot in use' })
  mount(<StationSlotsPage />)
  await selectStation()
  fireEvent.click(await screen.findByRole('button', { name: 'Mark unavailable' }))
  fireEvent.change(screen.getByLabelText('Reason'), { target: { value: ' Faulty sensor ' } })
  fireEvent.click(screen.getByRole('button', { name: 'Confirm' }))
  await screen.findByText(/record changed or is in use/)
  expect(JSON.parse(mock.history.put[0].data)).toEqual({ targetStatus: 'ERROR', reason: 'Faulty sensor' })
  expect(screen.getByRole('button', { name: 'Confirm' })).toBeDisabled()
  await waitFor(() => expect(mock.history.get.filter(request => request.url?.endsWith('/slots')).length).toBe(2))
})
