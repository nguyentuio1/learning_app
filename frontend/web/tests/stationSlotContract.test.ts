import { afterEach, beforeEach, expect, it } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { apiClient } from '../src/services/apiClient'
import { slotApi } from '../src/features/slots/slotApi'
import { operationDestination } from '../src/features/operations/navigation'

let mock: MockAdapter
beforeEach(() => { mock = new MockAdapter(apiClient) })
afterEach(() => mock.restore())
it.each([undefined, 7])('adds a slot with optional number %s and no client-assigned status', async slotNumber => {
  mock.onPost('/api/landing-stations/station/slots').reply(201, {})
  await slotApi.add('station', slotNumber)
  expect(JSON.parse(mock.history.post[0].data)).toEqual(slotNumber === undefined ? {} : { slotNumber })
})
it.each(['EMPTY', 'ERROR'] as const)('sends the exact status-change contract for %s', async targetStatus => {
  mock.onPut('/api/station-slots/slot/status').reply(200, {})
  await slotApi.status('slot', targetStatus, '  Repair checked  ')
  expect(JSON.parse(mock.history.put[0].data)).toEqual({ targetStatus, reason: 'Repair checked' })
})
it('loads station and slots together using the same cancellation signal', async () => {
  const station = { id: 'station', operatorId: 'operator' }
  const slots = [{ id: 'slot', status: 'EMPTY' }]
  mock.onGet('/api/landing-stations/station').reply(200, station)
  mock.onGet('/api/landing-stations/station/slots').reply(200, slots)
  const signal = new AbortController().signal
  expect(await slotApi.inventory('station', signal)).toEqual({ station, slots })
  expect(mock.history.get.every(request => request.signal === signal)).toBe(true)
})
it('surfaces a removal conflict instead of reporting successful deletion', async () => {
  mock.onDelete('/api/station-slots/slot').reply(409)
  await expect(slotApi.remove('slot')).rejects.toMatchObject({ response: { status: 409 } })
  expect(mock.history.delete).toHaveLength(1)
})
it.each(['ADMIN', 'DISPATCHER', 'STATION_OPERATOR'])('allows %s to view slot inventory', role => {
  expect(operationDestination('/station-slots', role)).toBe('/station-slots')
})
it.each(['CUSTOMER', 'MANAGER'])('does not route %s to slot management', role => {
  expect(operationDestination('/station-slots', role)).toBeUndefined()
})
