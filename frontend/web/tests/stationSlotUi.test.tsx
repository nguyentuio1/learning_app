// LA-120: component integration with real forms, hooks and API adapters.
import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { ConfigProvider } from 'antd'
import MockAdapter from 'axios-mock-adapter'
import { StationSlotsPage } from '../src/features/slots/StationSlotsPage'
import { apiClient } from '../src/services/apiClient'
import { session } from '../src/services/authSession'

const actor = vi.hoisted(() => ({ user: { id: 'operator', role: { code: 'ADMIN' } } }))
vi.mock('../src/hooks/useAuth', () => ({ useAuth: () => actor }))
const station = { id: 'station', name: 'District Hub', stationCode: 'HCM-01', operatorId: 'operator' }
function slot(number: number, status = 'EMPTY', linked = false) {
  return { id: 'slot-' + number, stationId: station.id, slotNumber: number, status, lastUpdated: '2026-11-01T00:00:00Z', currentOrderId: null, currentMissionId: linked ? 'mission-1' : null }
}
let mock: MockAdapter
beforeEach(() => {
  session.clear(); actor.user.role.code = 'ADMIN'; mock = new MockAdapter(apiClient)
  mock.onGet('/api/landing-stations').reply(200, [station])
  mock.onGet('/api/landing-stations/station').reply(200, station)
  mock.onGet('/api/landing-stations/station/slots').reply(200, [slot(1), slot(2, 'ERROR')])
})
afterEach(() => mock.restore())
async function show() {
  render(<ConfigProvider theme={{ token: { motion: false } }}><StationSlotsPage /></ConfigProvider>)
  await waitFor(() => expect(mock.history.get.some(r => r.url === '/api/landing-stations')).toBe(true))
  await userEvent.click(screen.getByRole('combobox', { name: 'Station' }))
  await userEvent.click(await screen.findByText('District Hub · HCM-01', { selector: '.ant-select-item-option-content' }))
  await screen.findByRole('heading', { name: 'District Hub · Slots' })
}
function card(number: number) { return within(screen.getByRole('heading', { name: 'Slot ' + number, exact: true }).closest('article')!) }
it('adds with automatic numbering and reloads the server inventory', async () => {
  mock.onPost('/api/landing-stations/station/slots').reply(201, slot(3))
  await show()
  await userEvent.click(screen.getByRole('button', { name: 'Add slot' }))
  await userEvent.click(await screen.findByRole('button', { name: 'Confirm' }))
  expect(await screen.findByText('Slot inventory updated.')).toBeVisible()
  expect(JSON.parse(mock.history.post[0].data)).toEqual({})
  await waitFor(() => expect(mock.history.get.filter(r => r.url?.endsWith('/slots'))).toHaveLength(2))
})
it('isolates a slot with a required reason', async () => {
  mock.onPut('/api/station-slots/slot-1/status').reply(200, slot(1, 'ERROR'))
  await show()
  await userEvent.click(card(1).getByRole('button', { name: 'Mark unavailable' }))
  await userEvent.click(await screen.findByRole('button', { name: 'Confirm' }))
  expect(mock.history.put).toHaveLength(0)
  fireEvent.change(screen.getByLabelText('Reason'), { target: { value: '  Charging pin damaged  ' } })
  await userEvent.click(screen.getByRole('button', { name: 'Confirm' }))
  await screen.findByText('Slot inventory updated.')
  expect(JSON.parse(mock.history.put[0].data)).toEqual({ targetStatus: 'ERROR', reason: 'Charging pin damaged' })
})
it('restores an ERROR slot to EMPTY', async () => {
  mock.onPut('/api/station-slots/slot-2/status').reply(200, slot(2))
  await show()
  await userEvent.click(card(2).getByRole('button', { name: 'Restore availability' }))
  fireEvent.change(await screen.findByLabelText('Reason'), { target: { value: 'Repair complete' } })
  await userEvent.click(screen.getByRole('button', { name: 'Confirm' }))
  await screen.findByText('Slot inventory updated.')
  expect(JSON.parse(mock.history.put[0].data)).toEqual({ targetStatus: 'EMPTY', reason: 'Repair complete' })
})
it('prevents deletion of occupied, reserved, linked and last slots', async () => {
  mock.onGet('/api/landing-stations/station/slots').reply(200, [slot(1, 'OCCUPIED'), slot(2, 'RESERVED'), slot(3, 'EMPTY', true)])
  await show()
  for (const n of [1, 2, 3]) expect(card(n).getByRole('button', { name: 'Remove slot' })).toBeDisabled()
  expect(card(1).queryByRole('button', { name: 'Mark unavailable' })).not.toBeInTheDocument()
  expect(card(2).queryByRole('button', { name: 'Mark unavailable' })).not.toBeInTheDocument()
  mock.onGet('/api/landing-stations/station/slots').reply(200, [slot(1)])
  await userEvent.click(screen.getByRole('button', { name: 'Refresh slots' }))
  await waitFor(() => expect(screen.queryByRole('heading', { name: 'Slot 2' })).not.toBeInTheDocument())
  expect(card(1).getByRole('button', { name: 'Remove slot' })).toBeDisabled()
})
it('prevents adding a twenty-first slot', async () => {
  mock.onGet('/api/landing-stations/station/slots').reply(200, Array.from({ length: 20 }, (_, i) => slot(i + 1)))
  await show()
  expect(screen.getByRole('button', { name: 'Add slot' })).toBeDisabled()
})
it('keeps dispatcher inventory read-only', async () => {
  actor.user.role.code = 'DISPATCHER'
  await show()
  expect(screen.queryByRole('button', { name: 'Add slot' })).not.toBeInTheDocument()
  expect(screen.queryByRole('button', { name: 'Mark unavailable' })).not.toBeInTheDocument()
  expect(screen.queryByRole('button', { name: 'Remove slot' })).not.toBeInTheDocument()
})
it('lets the assigned operator change status but not add or remove slots', async () => {
  actor.user.role.code = 'STATION_OPERATOR'
  await show()
  expect(card(1).getByRole('button', { name: 'Mark unavailable' })).toBeEnabled()
  expect(screen.queryByRole('button', { name: 'Add slot' })).not.toBeInTheDocument()
  expect(screen.queryByRole('button', { name: 'Remove slot' })).not.toBeInTheDocument()
})
it('requires reviewing the refreshed inventory after a removal conflict', async () => {
  mock.onDelete('/api/station-slots/slot-1').reply(409)
  await show()
  await userEvent.click(card(1).getByRole('button', { name: 'Remove slot' }))
  await userEvent.click(await screen.findByRole('button', { name: 'Confirm' }))
  expect(await screen.findByText(/The record changed or is in use/)).toBeVisible()
  expect(screen.getByRole('button', { name: 'Confirm' })).toBeDisabled()
  expect(mock.history.delete).toHaveLength(1)
  expect(await screen.findByRole('heading', { name: 'Slot 1' })).toBeVisible()
})
