// LA-130: real UI and API adapter integration, simulated HTTP responses only.
import { afterEach, beforeEach, expect, it } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { ConfigProvider } from 'antd'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import MockAdapter from 'axios-mock-adapter'
import { DronePage } from '../src/features/drones/DronePage'
import { DronesPage } from '../src/features/drones/DronesPage'
import { apiClient } from '../src/services/apiClient'
import { session } from '../src/services/authSession'

const drone = { id: 'drone', serialNumber: 'DRN-01', modelName: 'Carrier', maxPayloadGram: 3000, maxFlightRangeKm: 25, batteryCapacityMah: 22000, status: 'MAINTENANCE', currentStationId: null, currentStationName: null, activeMissionId: null, createdAt: '2026-11-01T00:00:00Z' }
let mock: MockAdapter
beforeEach(() => {
  session.clear(); mock = new MockAdapter(apiClient)
  mock.onGet('/api/landing-stations').reply(200, [])
  mock.onGet('/api/drones/drone').reply(200, drone)
})
afterEach(() => mock.restore())
function show(path = '/drones/drone') {
  render(<ConfigProvider theme={{ token: { motion: false } }}><MemoryRouter initialEntries={[path]}><Routes>
    <Route path="/drones" element={<DronesPage />} /><Route path="/drones/new" element={<DronePage />} /><Route path="/drones/:id" element={<DronePage />} />
  </Routes></MemoryRouter></ConfigProvider>)
}
it('requires inspection and readiness before completing maintenance', async () => {
  mock.onPut('/api/drones/drone/maintenance').reply(200, { ...drone, status: 'IDLE' })
  show()
  fireEvent.change(await screen.findByLabelText('Reason'), { target: { value: ' Inspection complete ' } })
  await userEvent.click(screen.getByRole('button', { name: 'Complete maintenance' }))
  expect(await screen.findByText('Confirm that the inspection passed.')).toBeVisible()
  expect(mock.history.put).toHaveLength(0)
  await userEvent.click(screen.getByRole('checkbox', { name: 'Inspection passed' }))
  await userEvent.click(screen.getByLabelText('Ready for service'))
  await userEvent.click(screen.getByText('Ground checks complete and battery ready', { selector: '.ant-select-item-option-content' }))
  await userEvent.click(screen.getByRole('button', { name: 'Complete maintenance' }))
  expect(await screen.findByText('Maintenance saved. Current status: IDLE.')).toBeVisible()
  expect(JSON.parse(mock.history.put[0].data)).toEqual({ action: 'COMPLETE', expectedStatus: 'MAINTENANCE', reason: 'Inspection complete', inspectionPassed: true, readyForService: true })
  expect(screen.queryByRole('button', { name: 'Complete maintenance' })).not.toBeInTheDocument()
})
it.each(['ASSIGNED', 'FLYING'])('does not permit maintenance from %s', async status => {
  mock.onGet('/api/drones/drone').reply(200, { ...drone, status })
  show()
  expect(await screen.findByText(/Maintenance is unavailable while/)).toBeVisible()
  expect(screen.queryByRole('button', { name: /Start maintenance|Complete maintenance/ })).not.toBeInTheDocument()
})
it('blocks specification edits and maintenance when an active mission exists', async () => {
  mock.onGet('/api/drones/drone').reply(200, { ...drone, status: 'IDLE', activeMissionId: 'mission' })
  show()
  expect(await screen.findByRole('button', { name: 'Save drone' })).toBeDisabled()
  expect(screen.getByText(/Maintenance is unavailable while/)).toBeVisible()
  expect(mock.history.put).toHaveLength(0)
})
it('sends the observed status on START and requires refresh after a stale-state conflict', async () => {
  mock.onGet('/api/drones/drone').reply(200, { ...drone, status: 'CHARGING' })
  mock.onPut('/api/drones/drone/maintenance').reply(409)
  show()
  fireEvent.change(await screen.findByLabelText('Reason'), { target: { value: 'Check the rotor' } })
  await userEvent.click(screen.getByRole('button', { name: 'Start maintenance' }))
  expect(await screen.findByText(/The record changed or is in use/)).toBeVisible()
  expect(JSON.parse(mock.history.put[0].data)).toEqual({ action: 'START', expectedStatus: 'CHARGING', reason: 'Check the rotor' })
  expect(screen.queryByRole('button', { name: 'Start maintenance' })).not.toBeInTheDocument()
})
it('creates a fleet record and shows the returned MAINTENANCE state', async () => {
  mock.onPost('/api/drones').reply(201, drone)
  show('/drones/new')
  await waitFor(() => expect(screen.getByRole('button', { name: 'Save drone' })).toBeEnabled())
  for (const [label, value] of [['Serial number', 'DRN-01'], ['Model name', 'Carrier'], ['Maximum payload (g)', '3000'], ['Maximum range (km)', '25'], ['Battery capacity (mAh)', '22000']]) {
    fireEvent.change(screen.getByLabelText(label), { target: { value } })
  }
  await userEvent.click(screen.getByRole('button', { name: 'Save drone' }))
  expect(await screen.findByRole('heading', { name: 'Drone overview' })).toBeVisible()
  expect(screen.getByText('MAINTENANCE', { selector: '.ant-tag' })).toBeVisible()
  expect(mock.history.post).toHaveLength(1)
})
it('shows API field errors on edit without losing entered values', async () => {
  mock.onPut('/api/drones/drone').reply(400, { errors: { ModelName: ['Model is invalid.'] } })
  show()
  fireEvent.change(await screen.findByLabelText('Model name'), { target: { value: 'New carrier' } })
  await userEvent.click(screen.getByRole('button', { name: 'Save drone' }))
  expect(await screen.findByText('Model is invalid.')).toBeVisible()
  expect(screen.getByLabelText('Model name')).toHaveValue('New carrier')
})
it('retries list loading and applies a server-side search', async () => {
  mock.onGet('/api/drones').replyOnce(503)
  mock.onGet('/api/drones').reply(200, [drone])
  show('/drones')
  await userEvent.click(await screen.findByRole('button', { name: 'Try again' }))
  expect(await screen.findByRole('link', { name: /DRN-01/ })).toBeVisible()
  fireEvent.change(screen.getByLabelText('Search'), { target: { value: ' Carrier ' } })
  await userEvent.click(screen.getByRole('button', { name: 'Apply filters' }))
  await waitFor(() => expect(mock.history.get.filter(r => r.url === '/api/drones').at(-1)?.params).toMatchObject({ search: 'Carrier' }))
})
