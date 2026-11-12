import { afterEach, beforeEach, expect, it } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { apiClient } from '../src/services/apiClient'
import { stationApi } from '../src/features/stations/stationApi'
import { operationDestination } from '../src/features/operations/navigation'

let mock: MockAdapter
beforeEach(() => { mock = new MockAdapter(apiClient) })
afterEach(() => mock.restore())
const values = { stationCode: ' HCM-01 ', name: ' Hub ', address: ' Main street ', latitude: 10, longitude: 106, totalSlots: 4, operatorId: undefined }

it('creates only the writable contract fields, with an explicit unassigned operator', async () => {
  mock.onPost('/api/landing-stations').reply(201, { id: 'station' })
  await stationApi.create(values)
  expect(JSON.parse(mock.history.post[0].data)).toEqual({ stationCode: 'HCM-01', name: 'Hub', address: 'Main street', latitude: 10, longitude: 106, totalSlots: 4, operatorId: null })
})
it('does not send immutable code, slot count or status when editing', async () => {
  mock.onPut('/api/landing-stations/station').reply(200, {})
  await stationApi.update('station', values)
  expect(JSON.parse(mock.history.put[0].data)).toEqual({ name: 'Hub', address: 'Main street', latitude: 10, longitude: 106, operatorId: null })
})
it('preserves rerouting information from a status change and trims its reason', async () => {
  const result = { station: { id: 'station' }, rerouteWarning: true, affectedMissionIds: ['mission'] }
  mock.onPut('/api/landing-stations/station/status').reply(200, result)
  expect(await stationApi.status('station', 'OFFLINE', '  Pad closed  ')).toEqual(result)
  expect(JSON.parse(mock.history.put[0].data)).toEqual({ newStatus: 'OFFLINE', reason: 'Pad closed' })
})
it('forwards list filters and abort signal without changing the array response', async () => {
  const signal = new AbortController().signal
  mock.onGet('/api/landing-stations').reply(200, [])
  expect(await stationApi.list(signal, { search: 'Hub', status: 'OFFLINE' })).toEqual([])
  expect(mock.history.get[0]).toMatchObject({ signal, params: { search: 'Hub', status: 'OFFLINE' } })
})
it.each(['STATION_OPERATOR', 'DISPATCHER', 'CUSTOMER', 'MANAGER'])('does not route %s to station creation', role => {
  expect(operationDestination('/stations/new', role)).toBeUndefined()
})
it('allows admin creation and dispatcher station reading', () => {
  expect(operationDestination('/stations/new', 'ADMIN')).toBe('/stations/new')
  expect(operationDestination('/stations', 'DISPATCHER')).toBe('/stations')
})
