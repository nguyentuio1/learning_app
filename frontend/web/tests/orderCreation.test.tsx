import { afterEach, beforeEach, expect, it } from 'vitest'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { ConfigProvider } from 'antd'
import MockAdapter from 'axios-mock-adapter'
import { apiClient } from '../src/services/apiClient'
import { orderCreationApi } from '../src/services/orderCreationApi'
import { CreateDeliveryOrderPage } from '../src/pages/CreateDeliveryOrderPage'

let mock: MockAdapter
beforeEach(() => {
  mock = new MockAdapter(apiClient)
  mock.onGet('/api/packages').reply(200, [{ id: 'p1', packageName: 'Books', weightGram: 500, lengthCm: 10, widthCm: 10, heightCm: 5 }])
  mock.onGet('/api/customer-addresses').reply(200, [])
  mock.onGet('/api/landing-stations').reply(200, [
    { id: 's1', name: 'North station', status: 'OPERATIONAL' },
    { id: 's2', name: 'South station', status: 'OPERATIONAL' },
    { id: 's3', name: 'Closed station', status: 'MAINTENANCE' },
  ])
})
afterEach(() => mock.restore())
function open() { render(<ConfigProvider theme={{ token: { motion: false } }}><CreateDeliveryOrderPage /></ConfigProvider>) }
async function choose(label: string, option: string) {
  fireEvent.mouseDown(await screen.findByLabelText(label))
  await userEvent.click(await screen.findByText(option, { selector: '.ant-select-dropdown:not(.ant-select-dropdown-hidden) .ant-select-item-option-content' }))
}
async function fill() {
  await choose('Package', 'Books · 500 g')
  await choose('Origin station', 'North station')
  await choose('Destination station', 'South station')
  fireEvent.change(screen.getByLabelText('Recipient name'), { target: { value: 'Recipient' } })
  fireEvent.change(screen.getByLabelText('Recipient phone'), { target: { value: '0901234567' } })
}
it('sends only the six contract fields, trimming text and excluding identity/status/code', async () => {
  mock.onPost('/api/delivery-orders').reply(201, { id: 'o1' })
  await orderCreationApi.create({ packageId: 'p1', originStationId: 's1', destinationStationId: 's2', recipientName: ' Name ', recipientPhone: ' 0901234567 ', ...{ customerId: 'spoof', status: 'APPROVED', pickupSecureCode: '123456' } })
  expect(JSON.parse(mock.history.post[0].data)).toEqual({ packageId: 'p1', originStationId: 's1', destinationStationId: 's2', deliveryAddressId: null, recipientName: 'Name', recipientPhone: '0901234567' })
})
it('requires confirmation, prevents repeated creation and displays the one-time pickup code', async () => {
  let finish!: (result: [number, object]) => void
  mock.onPost('/api/delivery-orders').reply(() => new Promise(resolve => { finish = resolve }))
  open(); await fill()
  expect(mock.history.get.find(r => r.url === '/api/landing-stations')?.params).toEqual({ status: 'OPERATIONAL' })
  await userEvent.click(screen.getByRole('button', { name: 'Review order' }))
  const dialog = await screen.findByRole('dialog')
  expect(mock.history.post).toHaveLength(0)
  await userEvent.dblClick(within(dialog).getByRole('button', { name: 'Create order' }))
  await waitFor(() => expect(mock.history.post).toHaveLength(1))
  finish([201, { id: 'o1', trackingNumber: 'DD-202611-0001', status: 'PENDING', pickupSecureCode: '482917' }])
  expect(await screen.findByLabelText('Pickup code')).toHaveTextContent('482917')
  expect(screen.getByRole('button', { name: 'Create another order' })).toBeDisabled()
  expect(JSON.stringify(localStorage)).not.toContain('482917')
  expect(JSON.stringify(sessionStorage)).not.toContain('482917')
})
it('requires valid recipient input and revalidates the destination when origin changes', async () => {
  open(); await fill()
  await choose('Origin station', 'South station')
  fireEvent.change(screen.getByLabelText('Recipient phone'), { target: { value: 'abc' } })
  await userEvent.click(screen.getByRole('button', { name: 'Review order' }))
  expect(await screen.findByText('Choose a different destination station.')).toBeVisible()
  expect(await screen.findByText('Enter 9 to 11 digits.')).toBeVisible()
  expect(mock.history.post).toHaveLength(0)
})
it('does not blindly retry a creation whose response was lost', async () => {
  mock.onPost('/api/delivery-orders').networkError()
  open(); await fill()
  await userEvent.click(screen.getByRole('button', { name: 'Review order' }))
  await userEvent.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Create order' }))
  expect(await screen.findByText(/We could not confirm whether this order was created/)).toBeVisible()
  expect(screen.getByRole('button', { name: 'Review order' })).toBeDisabled()
  expect(mock.history.post).toHaveLength(1)
})
it('offers a retry after lookup failure without rendering an empty creation form', async () => {
  mock.onGet('/api/packages').reply(500)
  open()
  const retry = await screen.findByRole('button', { name: 'Try again' })
  mock.onGet('/api/packages').reply(200, [])
  await userEvent.click(retry)
  expect(await screen.findByLabelText('Package')).toBeVisible()
})
