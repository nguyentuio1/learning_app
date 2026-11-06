import { afterEach, beforeEach, expect, it } from 'vitest'
import { fireEvent, screen, waitFor } from '@testing-library/react'
import { Route, Routes } from 'react-router-dom'
import MockAdapter from 'axios-mock-adapter'
import { apiClient } from '../src/services/apiClient'
import { session } from '../src/services/authSession'
import { StationPage } from '../src/features/stations/StationPage'
import { stationApi } from '../src/features/stations/stationApi'
import { mount, station, stationId } from './operationsFixtures'

let mock: MockAdapter
beforeEach(() => { session.clear(); mock = new MockAdapter(apiClient) })
afterEach(() => mock.restore())
function page(role: 'ADMIN' | 'DISPATCHER' | 'STATION_OPERATOR' = 'ADMIN') {
  mount(<Routes><Route path="/stations/:id" element={<StationPage />} /></Routes>, role, '/stations/' + stationId)
}
it('updates only editable fields, never code, capacity or status', async () => {
  mock.onPut('/api/landing-stations/' + stationId).reply(200, station)
  await stationApi.update(stationId, station)
  expect(JSON.parse(mock.history.put[0].data)).toEqual({ name: station.name, address: station.address, latitude: 10.77, longitude: 106.7, operatorId: station.operatorId })
})
it('keeps dispatcher station details read-only and does not request admin users', async () => {
  mock.onGet('/api/landing-stations/' + stationId).reply(200, station)
  page('DISPATCHER')
  await screen.findByText('Station overview')
  expect(screen.queryByRole('button', { name: 'Save station' })).not.toBeInTheDocument()
  expect(screen.queryByRole('button', { name: 'Change status' })).not.toBeInTheDocument()
  expect(mock.history.get.some(request => request.url === '/api/users')).toBe(false)
})
it('shows rerouting warnings after an assigned operator changes station status', async () => {
  mock.onGet('/api/landing-stations/' + stationId).reply(200, station)
  mock.onPut('/api/landing-stations/' + stationId + '/status').reply(200, { station, rerouteWarning: true, affectedMissionIds: ['mission-needs-reroute'] })
  page('STATION_OPERATOR')
  fireEvent.mouseDown(await screen.findByLabelText('Operating status'))
  fireEvent.click(await screen.findByText('MAINTENANCE', { selector: '.ant-select-item-option-content' }))
  fireEvent.change(screen.getByLabelText('Reason'), { target: { value: ' Planned maintenance ' } })
  fireEvent.click(screen.getByRole('button', { name: 'Change status' }))
  await screen.findByText('Incoming flights need rerouting')
  expect(screen.getByText('mission-needs-reroute')).toBeVisible()
  expect(JSON.parse(mock.history.put[0].data)).toEqual({ newStatus: 'MAINTENANCE', reason: 'Planned maintenance' })
})
it('does not let an operator change another station', async () => {
  mock.onGet('/api/landing-stations/' + stationId).reply(200, { ...station, operatorId: 'someone-else' })
  page('STATION_OPERATOR')
  await screen.findByText('Station overview')
  expect(screen.queryByRole('button', { name: 'Change status' })).not.toBeInTheDocument()
})
it('never submits initial capacity above the contract limit of 20 slots', async () => {
  mock.onGet('/api/users').reply(200, [])
  mock.onPost('/api/landing-stations').reply(201, station)
  mount(<StationPage />)
  await waitFor(() => expect(screen.getByRole('button', { name: 'Save station' })).toBeEnabled())
  for (const [label, value] of [['Station name', 'New hub'], ['Station code', 'HCM-02'], ['Address', '10 Main Street'], ['Latitude', '10'], ['Longitude', '106'], ['Initial slots', '21']]) {
    fireEvent.change(screen.getByLabelText(label), { target: { value } })
  }
  fireEvent.click(screen.getByRole('button', { name: 'Save station' }))
  await screen.findByText('Enter a whole number from 1 to 20.')
  expect(mock.history.post).toHaveLength(0)
})
