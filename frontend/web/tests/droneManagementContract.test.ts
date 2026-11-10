import { afterEach, beforeEach, expect, it } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { apiClient } from '../src/services/apiClient'
import { droneApi } from '../src/features/drones/droneApi'
import { operationDestination } from '../src/features/operations/navigation'

let mock: MockAdapter
beforeEach(() => { mock = new MockAdapter(apiClient) })
afterEach(() => mock.restore())
const values = { serialNumber: ' DRN-01 ', modelName: ' Carrier ', maxPayloadGram: 3000, maxFlightRangeKm: 25, batteryCapacityMah: 22000 }
it('creates a drone without forcing IDLE or inventing a battery percentage', async () => {
  mock.onPost('/api/drones').reply(201, {})
  await droneApi.create(values)
  expect(JSON.parse(mock.history.post[0].data)).toEqual({ serialNumber: 'DRN-01', modelName: 'Carrier', maxPayloadGram: 3000, maxFlightRangeKm: 25, batteryCapacityMah: 22000, currentStationId: null })
})
it('updates specifications without changing serial, station or status', async () => {
  mock.onPut('/api/drones/drone').reply(200, {})
  await droneApi.update('drone', { ...values, currentStationId: 'station' })
  expect(JSON.parse(mock.history.put[0].data)).toEqual({ modelName: 'Carrier', maxPayloadGram: 3000, maxFlightRangeKm: 25, batteryCapacityMah: 22000 })
})
it('includes expectedStatus on START but no completion-only fields', async () => {
  mock.onPut('/api/drones/drone/maintenance').reply(200, {})
  await droneApi.maintenance('drone', { action: 'START', expectedStatus: 'IDLE', reason: '  Inspect rotor  ', inspectionPassed: true, readyForService: true })
  expect(JSON.parse(mock.history.put[0].data)).toEqual({ action: 'START', expectedStatus: 'IDLE', reason: 'Inspect rotor' })
})
it.each([true, false])('preserves readyForService=%s on COMPLETE', async readyForService => {
  mock.onPut('/api/drones/drone/maintenance').reply(200, {})
  await droneApi.maintenance('drone', { action: 'COMPLETE', expectedStatus: 'MAINTENANCE', reason: 'Inspection passed', inspectionPassed: true, readyForService })
  expect(JSON.parse(mock.history.put[0].data)).toEqual({ action: 'COMPLETE', expectedStatus: 'MAINTENANCE', reason: 'Inspection passed', inspectionPassed: true, readyForService })
})
it.each(['CUSTOMER', 'STATION_OPERATOR', 'MANAGER'])('does not route %s to the fleet', role => {
  expect(operationDestination('/drones', role)).toBeUndefined()
})
it.each(['ADMIN', 'DISPATCHER'])('allows %s to manage the fleet', role => {
  expect(operationDestination('/drones/new', role)).toBe('/drones/new')
})
