import { afterEach, beforeEach, expect, it } from 'vitest'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { ConfigProvider } from 'antd'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import MockAdapter from 'axios-mock-adapter'
import { apiClient } from '../src/services/apiClient'
import { orderMutationApi } from '../src/services/orderMutationApi'
import { EditCustomerOrderPage } from '../src/pages/EditCustomerOrderPage'
import { ManageCustomerOrderPage } from '../src/pages/ManageCustomerOrderPage'

let mock: MockAdapter
const order = { id: 'o1', trackingNumber: 'DD-202611-0001', status: 'PENDING', recipientName: 'Recipient', recipientPhone: '0901234567', destinationStationId: 's2', originStationId: 's1', packageId: 'p1', packageName: 'Books', originStationName: 'North', destinationStationName: 'South', deliveryAddressId: null, statusLogs: [] }
beforeEach(() => {
  mock = new MockAdapter(apiClient)
  mock.onGet('/api/delivery-orders/o1').reply(200, order)
  mock.onGet('/api/customer-addresses').reply(200, [])
  mock.onGet('/api/landing-stations').reply(200, [{ id: 's1', name: 'North', status: 'OPERATIONAL' }, { id: 's2', name: 'South', status: 'OPERATIONAL' }])
})
afterEach(() => mock.restore())
function open(edit = false) {
  render(<ConfigProvider theme={{ token: { motion: false } }}><MemoryRouter initialEntries={[edit ? '/orders/o1/edit' : '/orders/o1/manage']}><Routes>
    <Route path="/orders/:id/manage" element={<ManageCustomerOrderPage />} /><Route path="/orders/:id/edit" element={<EditCustomerOrderPage />} />
  </Routes></MemoryRouter></ConfigProvider>)
}
it('whitelists update fields and uses cancellationReason rather than reason', async () => {
  mock.onPut('/api/delivery-orders/o1').reply(200, order)
  mock.onPost('/api/delivery-orders/o1/cancel').reply(200, order)
  await orderMutationApi.update('o1', { ...order, recipientName: ' Name ' })
  expect(JSON.parse(mock.history.put[0].data)).toEqual({ recipientName: 'Name', recipientPhone: '0901234567', destinationStationId: 's2', deliveryAddressId: null })
  await orderMutationApi.cancel('o1', '  Recipient unavailable  ')
  expect(JSON.parse(mock.history.post[0].data)).toEqual({ cancellationReason: 'Recipient unavailable' })
})
it('saves pending order changes once and reloads the detail instead of assuming a full mutation response', async () => {
  let finish!: (result: [number, object]) => void
  mock.onPut('/api/delivery-orders/o1').reply(() => new Promise(resolve => { finish = resolve }))
  open(true)
  fireEvent.change(await screen.findByLabelText('Recipient name'), { target: { value: 'Updated recipient' } })
  await userEvent.dblClick(screen.getByRole('button', { name: 'Save changes' }))
  await waitFor(() => expect(mock.history.put).toHaveLength(1))
  mock.onGet('/api/delivery-orders/o1').reply(200, { ...order, recipientName: 'Updated recipient' })
  finish([200, { id: 'o1', status: 'PENDING' }])
  expect(await screen.findByText('Order updated successfully.')).toBeVisible()
  expect(await screen.findByText('Updated recipient')).toBeVisible()
  expect(mock.history.get.filter(r => r.url === '/api/delivery-orders/o1')).toHaveLength(2)
})
it('refreshes after an edit conflict and removes the form if the order was approved', async () => {
  mock.onPut('/api/delivery-orders/o1').reply(409)
  open(true)
  const save = await screen.findByRole('button', { name: 'Save changes' })
  mock.onGet('/api/delivery-orders/o1').reply(200, { ...order, status: 'APPROVED' })
  await userEvent.click(save)
  expect(await screen.findByText('Only pending orders can be edited.')).toBeVisible()
  expect(screen.queryByRole('button', { name: 'Save changes' })).not.toBeInTheDocument()
  expect(mock.history.put).toHaveLength(1)
})
it.each(['APPROVED', 'READY_FOR_TAKEOFF', 'IN_TRANSIT', 'ARRIVED', 'DELIVERED', 'CANCELLED', 'FAILED'])('blocks direct editing in %s status', async status => {
  mock.onGet('/api/delivery-orders/o1').reply(200, { ...order, status })
  open(true)
  expect(await screen.findByText('Only pending orders can be edited.')).toBeVisible()
  expect(mock.history.get).toHaveLength(1)
  expect(mock.history.put).toHaveLength(0)
})
it('requires a nonblank cancellation reason and refreshes after successful cancellation', async () => {
  mock.onGet('/api/delivery-orders/o1').reply(200, { ...order, status: 'APPROVED' })
  mock.onPost('/api/delivery-orders/o1/cancel').reply(200, { status: 'CANCELLED' })
  open()
  await userEvent.click(await screen.findByRole('button', { name: 'Cancel order' }))
  const dialog = await screen.findByRole('dialog')
  fireEvent.change(within(dialog).getByLabelText('Reason for cancellation'), { target: { value: '   ' } })
  await userEvent.click(within(dialog).getByRole('button', { name: 'Confirm cancellation' }))
  expect(await screen.findByText('Enter a reason of up to 500 characters.')).toBeVisible()
  expect(mock.history.post).toHaveLength(0)
  fireEvent.change(within(dialog).getByLabelText('Reason for cancellation'), { target: { value: 'Plans changed' } })
  mock.onGet('/api/delivery-orders/o1').reply(200, { ...order, status: 'CANCELLED', cancelReason: 'Plans changed' })
  await userEvent.dblClick(within(dialog).getByRole('button', { name: 'Confirm cancellation' }))
  expect(await screen.findByText('This order can no longer be edited or cancelled.')).toBeVisible()
  expect(screen.queryByRole('button', { name: 'Cancel order' })).not.toBeInTheDocument()
  expect(mock.history.post).toHaveLength(1)
})
it('refreshes after a cancellation conflict without allowing a second stale action', async () => {
  mock.onPost('/api/delivery-orders/o1/cancel').reply(409)
  open()
  await userEvent.click(await screen.findByRole('button', { name: 'Cancel order' }))
  fireEvent.change(screen.getByLabelText('Reason for cancellation'), { target: { value: 'Plans changed' } })
  mock.onGet('/api/delivery-orders/o1').reply(200, { ...order, status: 'IN_TRANSIT' })
  await userEvent.click(screen.getByRole('button', { name: 'Confirm cancellation' }))
  expect(await screen.findByText('The order status changed. Review its latest details before taking another action.')).toBeVisible()
  expect(await screen.findByText('This order can no longer be edited or cancelled.')).toBeVisible()
  expect(mock.history.post).toHaveLength(1)
})
it('keeps form input on field validation failure', async () => {
  mock.onPut('/api/delivery-orders/o1').reply(400, { errors: { recipientName: ['Recipient name is invalid.'] } })
  open(true)
  fireEvent.change(await screen.findByLabelText('Recipient name'), { target: { value: 'Changed name' } })
  await userEvent.click(screen.getByRole('button', { name: 'Save changes' }))
  expect(await screen.findByText('Recipient name is invalid.')).toBeVisible()
  expect(screen.getByLabelText('Recipient name')).toHaveValue('Changed name')
})
