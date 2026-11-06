import { afterEach, beforeEach, expect, it } from 'vitest'
import { fireEvent, screen } from '@testing-library/react'
import { Route, Routes } from 'react-router-dom'
import MockAdapter from 'axios-mock-adapter'
import { apiClient } from '../src/services/apiClient'
import { session } from '../src/services/authSession'
import { DronePage } from '../src/features/drones/DronePage'
import { droneApi } from '../src/features/drones/droneApi'
import { mount, station } from './operationsFixtures'

let mock: MockAdapter
const drone = { id: 'drone', serialNumber: 'DRN-001', modelName: 'Aero', status: 'MAINTENANCE' as const, maxPayloadGram: 3000, maxFlightRangeKm: 25, batteryCapacityMah: 22000, currentStationId: station.id, currentStationName: station.name, activeMissionId: null, createdAt: station.createdAt }
beforeEach(() => { session.clear(); mock = new MockAdapter(apiClient); mock.onGet('/api/drones/drone').reply(200, drone) })
afterEach(() => mock.restore())
function page() { mount(<Routes><Route path="/drones/:id" element={<DronePage />} /></Routes>, 'DISPATCHER', '/drones/drone') }
it('does not send immutable fields in an update or accept a forged initial status', async () => {
  mock.onPut('/api/drones/drone').reply(200, drone)
  mock.onPost('/api/drones').reply(201, drone)
  await droneApi.update('drone', drone)
  await droneApi.create(drone)
  expect(JSON.parse(mock.history.put[0].data)).toEqual({ modelName: 'Aero', maxPayloadGram: 3000, maxFlightRangeKm: 25, batteryCapacityMah: 22000 })
  expect(JSON.parse(mock.history.post[0].data)).not.toHaveProperty('status')
})
it('does not complete maintenance without inspection confirmation', async () => {
  page()
  fireEvent.change(await screen.findByLabelText('Reason'), { target: { value: 'Checks complete' } })
  fireEvent.click(screen.getByRole('button', { name: 'Complete maintenance' }))
  expect(await screen.findByText('Confirm that the inspection passed.')).toBeVisible()
  expect(mock.history.put).toHaveLength(0)
})
it('sends expected status and boolean readiness, preserving a false value', async () => {
  mock.onPut('/api/drones/drone/maintenance').reply(200, { ...drone, status: 'CHARGING' })
  page()
  fireEvent.click(await screen.findByLabelText('Inspection passed'))
  fireEvent.mouseDown(screen.getByLabelText('Ready for service'))
  fireEvent.click(await screen.findByText('Charging still required'))
  fireEvent.change(screen.getByLabelText('Reason'), { target: { value: ' Ground checks passed ' } })
  fireEvent.click(screen.getByRole('button', { name: 'Complete maintenance' }))
  await screen.findByText('Maintenance saved. Current status: CHARGING.')
  expect(JSON.parse(mock.history.put[0].data)).toEqual({ action: 'COMPLETE', expectedStatus: 'MAINTENANCE', inspectionPassed: true, readyForService: false, reason: 'Ground checks passed' })
})
it('blocks edits and maintenance while a mission is active', async () => {
  mock.onGet('/api/drones/drone').reply(200, { ...drone, status: 'FLYING', activeMissionId: 'mission' })
  page()
  expect(await screen.findByRole('button', { name: 'Save drone' })).toBeDisabled()
  expect(screen.queryByRole('button', { name: 'Start maintenance' })).not.toBeInTheDocument()
  expect(screen.queryByRole('button', { name: 'Complete maintenance' })).not.toBeInTheDocument()
})
