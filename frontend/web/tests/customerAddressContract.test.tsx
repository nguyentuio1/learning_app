import { afterEach, beforeEach, expect, it } from 'vitest'
import { fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { ConfigProvider } from 'antd'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import MockAdapter from 'axios-mock-adapter'
import { CustomerAddressFormPage } from '../src/pages/CustomerAddressFormPage'
import { CustomerAddressesPage } from '../src/pages/CustomerAddressesPage'
import { apiClient } from '../src/services/apiClient'
import { session } from '../src/services/authSession'

let mock: MockAdapter
beforeEach(() => { session.clear(); mock = new MockAdapter(apiClient) })
afterEach(() => mock.restore())
function form(editing = false) {
  render(<ConfigProvider theme={{ token: { motion: false } }}><MemoryRouter initialEntries={[editing ? '/addresses/abc' : '/addresses/new']}><Routes>
    <Route path="/addresses/new" element={<CustomerAddressFormPage />} />
    <Route path="/addresses/:id" element={<CustomerAddressFormPage />} />
    <Route path="/addresses" element={<h1>Address list</h1>} />
  </Routes></MemoryRouter></ConfigProvider>)
}
function fill() {
  for (const [label, value] of [['Label', ' Home '], ['Contact name', 'An Nguyen'], ['Contact phone', '0901234567'], ['Address', '12 Nguyen Hue']]) {
    fireEvent.change(screen.getByLabelText(label, { exact: true }), { target: { value } })
  }
}
it('requires both coordinates and posts only contract fields once the pair is complete', async () => {
  mock.onPost('/api/customer-addresses').reply(201, { id: 'new-address' })
  form()
  fill()
  fireEvent.change(screen.getByLabelText('Latitude (optional)'), { target: { value: '10.774321' } })
  await userEvent.click(screen.getByRole('button', { name: 'Add address' }))
  expect((await screen.findAllByText('Provide both latitude and longitude, or leave both empty.')).length).toBeGreaterThan(0)
  expect(mock.history.post).toHaveLength(0)
  fireEvent.change(screen.getByLabelText('Longitude (optional)'), { target: { value: '106.703412' } })
  await userEvent.click(screen.getByRole('button', { name: 'Add address' }))
  await screen.findByRole('heading', { name: 'Address list' })
  expect(JSON.parse(mock.history.post[0].data)).toEqual({ label: 'Home', contactName: 'An Nguyen', contactPhone: '0901234567', addressLine: '12 Nguyen Hue', latitude: 10.774321, longitude: 106.703412, isDefault: false })
})
it('allows both coordinates to be omitted', async () => {
  mock.onPost('/api/customer-addresses').reply(201, { id: 'new-address' })
  form()
  fill()
  await userEvent.click(screen.getByRole('button', { name: 'Add address' }))
  await screen.findByRole('heading', { name: 'Address list' })
  expect(JSON.parse(mock.history.post[0].data)).toMatchObject({ latitude: null, longitude: null })
})
it('blocks saving when the existing address cannot be loaded', async () => {
  mock.onGet('/api/customer-addresses/abc').reply(404)
  form(true)
  expect(await screen.findByRole('button', { name: 'Save changes' })).toBeDisabled()
  expect(mock.history.put).toHaveLength(0)
})
it('keeps an address visible and shows an inline conflict when deletion is rejected', async () => {
  mock.onGet('/api/customer-addresses').reply(200, [{ id: 'abc', label: 'Home', contactName: 'An Nguyen', contactPhone: '0901234567', addressLine: '12 Nguyen Hue', isDefault: false }])
  mock.onDelete('/api/customer-addresses/abc').reply(409, { detail: 'Address is used by an order.' })
  render(<ConfigProvider theme={{ token: { motion: false } }}><MemoryRouter><CustomerAddressesPage /></MemoryRouter></ConfigProvider>)
  await userEvent.click(await screen.findByRole('button', { name: 'Delete', exact: true }))
  await userEvent.click(screen.getAllByRole('button', { name: 'Delete', exact: true }).at(-1)!)
  expect(await screen.findByRole('alert')).toHaveTextContent(/used|order/i)
  expect(screen.getByText('12 Nguyen Hue')).toBeVisible()
  expect(mock.history.delete).toHaveLength(1)
})
