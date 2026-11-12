// LA-117: real React forms/hooks/API adapter; only the HTTP boundary is simulated.
import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { ConfigProvider } from 'antd'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import MockAdapter from 'axios-mock-adapter'
import { StationsPage } from '../src/features/stations/StationsPage'
import { StationPage } from '../src/features/stations/StationPage'
import { apiClient } from '../src/services/apiClient'
import { session } from '../src/services/authSession'

const actor = vi.hoisted(() => ({ user: { id: 'operator', role: { code: 'ADMIN' } } }))
vi.mock('../src/hooks/useAuth', () => ({ useAuth: () => actor }))
const station = { id: 'station', stationCode: 'HCM-01', name: 'District Hub', address: 'Main street', latitude: 10, longitude: 106, status: 'OPERATIONAL', totalSlots: 4, emptySlots: 4, operatorId: 'operator', operatorName: 'Assigned operator', hostedDroneCount: 0, slots: [] }
let mock: MockAdapter
beforeEach(() => {
  session.clear(); actor.user.role.code = 'ADMIN'
  mock = new MockAdapter(apiClient)
  mock.onGet('/api/users').reply(200, [])
  mock.onGet('/api/landing-stations/station').reply(200, station)
})
afterEach(() => mock.restore())
function show(path = '/stations/station') {
  render(<ConfigProvider theme={{ token: { motion: false } }}><MemoryRouter initialEntries={[path]}><Routes>
    <Route path="/stations" element={<StationsPage />} />
    <Route path="/stations/new" element={<StationPage />} />
    <Route path="/stations/:id" element={<StationPage />} />
  </Routes></MemoryRouter></ConfigProvider>)
}
function fillNew(slots: string) {
  for (const [label, value] of [['Station name', ' New hub '], ['Station code', 'NEW-01'], ['Address', ' New street '], ['Latitude', '10'], ['Longitude', '106'], ['Initial slots', slots]]) {
    fireEvent.change(screen.getByLabelText(label), { target: { value } })
  }
}
it('recovers from a list failure and forwards the search filter', async () => {
  mock.onGet('/api/landing-stations').replyOnce(503)
  mock.onGet('/api/landing-stations').reply(200, [station])
  show('/stations')
  await userEvent.click(await screen.findByRole('button', { name: 'Try again' }))
  expect(await screen.findByText('Main street')).toBeVisible()
  fireEvent.change(screen.getByLabelText('Search'), { target: { value: ' Hub ' } })
  await userEvent.click(screen.getByRole('button', { name: 'Apply filters' }))
  await waitFor(() => expect(mock.history.get.at(-1)?.params).toEqual({ search: 'Hub', status: undefined }))
})
it.each(['0', '21', '1.5'])('rejects invalid initial slot count %s before sending a request', async count => {
  show('/stations/new')
  await waitFor(() => expect(screen.getByRole('button', { name: 'Save station' })).toBeEnabled())
  fillNew(count)
  await userEvent.click(screen.getByRole('button', { name: 'Save station' }))
  expect(await screen.findByText('Enter a whole number from 1 to 20.')).toBeVisible()
  expect(mock.history.post).toHaveLength(0)
})
it('creates a valid station and opens the returned record', async () => {
  mock.onPost('/api/landing-stations').reply(201, station)
  show('/stations/new')
  await waitFor(() => expect(screen.getByRole('button', { name: 'Save station' })).toBeEnabled())
  fillNew('20')
  await userEvent.click(screen.getByRole('button', { name: 'Save station' }))
  expect(await screen.findByRole('heading', { name: 'Station overview' })).toBeVisible()
  expect(mock.history.post).toHaveLength(1)
  expect(JSON.parse(mock.history.post[0].data)).toMatchObject({ name: 'New hub', address: 'New street', totalSlots: 20 })
})
it('shows field validation from the API without leaving the form', async () => {
  mock.onPost('/api/landing-stations').reply(400, { errors: { StationCode: ['This station code is invalid.'] } })
  show('/stations/new')
  await waitFor(() => expect(screen.getByRole('button', { name: 'Save station' })).toBeEnabled())
  fillNew('4')
  await userEvent.click(screen.getByRole('button', { name: 'Save station' }))
  expect(await screen.findByText('This station code is invalid.')).toBeVisible()
  expect(screen.getByLabelText('Station name')).toHaveValue(' New hub ')
})
it.each(['DISPATCHER', 'STATION_OPERATOR'])('does not expose admin editing to %s', async role => {
  actor.user.role.code = role
  show()
  await screen.findByRole('heading', { name: 'Station overview' })
  expect(screen.queryByRole('button', { name: 'Save station' })).not.toBeInTheDocument()
  expect(mock.history.get.some(r => r.url === '/api/users')).toBe(false)
  expect(screen.queryByRole('button', { name: 'Change status' }) !== null).toBe(role === 'STATION_OPERATOR')
})
it('does not offer status changes to a different station operator', async () => {
  actor.user.role.code = 'STATION_OPERATOR'
  mock.onGet('/api/landing-stations/station').reply(200, { ...station, operatorId: 'other' })
  show()
  await screen.findByRole('heading', { name: 'Station overview' })
  expect(screen.queryByRole('button', { name: 'Change status' })).not.toBeInTheDocument()
})
it('displays affected missions and requires refresh after a status change', async () => {
  actor.user.role.code = 'STATION_OPERATOR'
  mock.onPut('/api/landing-stations/station/status').reply(200, { station, rerouteWarning: true, affectedMissionIds: ['mission-123'] })
  show()
  fireEvent.change(await screen.findByLabelText('Reason'), { target: { value: ' Safety inspection ' } })
  await userEvent.click(screen.getByLabelText('Operating status'))
  await userEvent.click(screen.getByText('OFFLINE', { selector: '.ant-select-item-option-content' }))
  await userEvent.click(screen.getByRole('button', { name: 'Change status' }))
  expect(await screen.findByText('Incoming flights need rerouting')).toBeVisible()
  expect(screen.getByText('mission-123')).toBeVisible()
  expect(screen.queryByRole('button', { name: 'Change status' })).not.toBeInTheDocument()
  expect(JSON.parse(mock.history.put[0].data)).toEqual({ newStatus: 'OFFLINE', reason: 'Safety inspection' })
})
it('blocks a blind repeat when a create response is lost', async () => {
  mock.onPost('/api/landing-stations').networkError()
  show('/stations/new')
  await waitFor(() => expect(screen.getByRole('button', { name: 'Save station' })).toBeEnabled())
  fillNew('4')
  await userEvent.click(screen.getByRole('button', { name: 'Save station' }))
  expect(await screen.findByText(/The save could not be confirmed/)).toBeVisible()
  expect(screen.getByRole('button', { name: 'Save station' })).toBeDisabled()
  expect(mock.history.post).toHaveLength(1)
})
