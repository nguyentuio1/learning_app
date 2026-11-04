import { afterEach, beforeEach, expect, it } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { ConfigProvider } from 'antd'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import MockAdapter from 'axios-mock-adapter'
import { apiClient } from '../src/services/apiClient'
import { CustomerOrdersPage } from '../src/pages/CustomerOrdersPage'
import { CustomerOrderDetailPage } from '../src/pages/CustomerOrderDetailPage'
import { orderDateBoundary } from '../src/utils/orderDisplay'

let mock: MockAdapter
const detail = {
  id: 'o1', trackingNumber: 'DD-202611-0001', status: 'PENDING', packageName: 'Books', recipientName: 'Recipient', recipientPhone: '0901234567',
  originStationName: 'North', destinationStationName: 'South', createdAt: '2026-11-01T00:00:00Z',
  package: { weightGram: 500, lengthCm: 10, widthCm: 10, heightCm: 5, isFragile: true },
  // A backend accidentally returning this must still never expose it on a detail screen.
  pickupSecureCode: '987654',
  statusLogs: [
    { fromStatus: 'PENDING', toStatus: 'APPROVED', createdAt: '2026-11-02T00:00:00Z', remarks: 'Approved by dispatcher' },
    { fromStatus: null, toStatus: 'PENDING', createdAt: '2026-11-01T00:00:00Z', remarks: 'Order created' },
  ],
}
beforeEach(() => { mock = new MockAdapter(apiClient) })
afterEach(() => mock.restore())
function open(path = '/orders') {
  render(<ConfigProvider theme={{ token: { motion: false } }}><MemoryRouter initialEntries={[path]}><Routes>
    <Route path="/orders" element={<CustomerOrdersPage />} /><Route path="/orders/:id" element={<CustomerOrderDetailPage />} />
  </Routes></MemoryRouter></ConfigProvider>)
}
async function setDate(label: string, value: string) {
  const input = screen.getByLabelText(label)
  fireEvent.change(input, { target: { value } })
  fireEvent.keyDown(input, { key: 'Enter', code: 'Enter' })
  fireEvent.blur(input)
}
it('uses the paged contract, sends UTC date boundaries and resets pagination on filtering', async () => {
  mock.onGet('/api/delivery-orders').reply(200, { items: [detail], page: 1, pageSize: 20, totalCount: 21, totalPages: 2 })
  open()
  expect(await screen.findByRole('link', { name: detail.trackingNumber })).toBeVisible()
  await userEvent.click(screen.getByTitle('2'))
  await waitFor(() => expect(mock.history.get.at(-1)?.params.page).toBe(2))
  fireEvent.change(screen.getByLabelText('Tracking number'), { target: { value: '  DD-202611  ' } })
  await setDate('Created from', '2026-11-01')
  await setDate('Created through', '2026-11-03')
  await userEvent.click(screen.getByRole('button', { name: 'Apply filters' }))
  await waitFor(() => expect(mock.history.get.at(-1)?.params).toMatchObject({ page: 1, pageSize: 20, searchKeyword: 'DD-202611', fromDate: orderDateBoundary('2026-11-01'), toDate: orderDateBoundary('2026-11-03', true) }))
})
it('rejects reversed dates before requesting another page', async () => {
  mock.onGet('/api/delivery-orders').reply(200, { items: [], totalCount: 0 })
  open(); await screen.findByText('No orders match these filters.')
  await setDate('Created from', '2026-11-03')
  await setDate('Created through', '2026-11-01')
  await userEvent.click(screen.getByRole('button', { name: 'Apply filters' }))
  expect(await screen.findByText('End date must not be before start date.')).toBeVisible()
  expect(mock.history.get).toHaveLength(1)
})
it('shows server failures separately from an empty list and supports retry', async () => {
  mock.onGet('/api/delivery-orders').reply(500)
  open(); const retry = await screen.findByRole('button', { name: 'Try again' })
  expect(screen.queryByText('No orders match these filters.')).not.toBeInTheDocument()
  mock.onGet('/api/delivery-orders').reply(200, { items: [], totalCount: 0 })
  await userEvent.click(retry)
  expect(await screen.findByText('No orders match these filters.')).toBeVisible()
})
it('shows detail with chronological history and never renders the pickup code', async () => {
  mock.onGet('/api/delivery-orders/o1').reply(200, detail)
  open('/orders/o1')
  expect(await screen.findByRole('heading', { name: detail.trackingNumber })).toBeVisible()
  expect(screen.getAllByRole('listitem').map(li => li.textContent)).toEqual([expect.stringContaining('Order created'), expect.stringContaining('Approved by dispatcher')])
  expect(screen.queryByText('987654')).not.toBeInTheDocument()
  expect(mock.history.get.map(r => r.url)).toEqual(['/api/delivery-orders/o1'])
  expect(screen.getAllByText('Not available yet')).toHaveLength(4)
})
it('handles a missing or foreign-owned order without showing private details', async () => {
  mock.onGet('/api/delivery-orders/foreign').reply(404)
  open('/orders/foreign')
  expect(await screen.findByText('The requested order or selected item is no longer available.')).toBeVisible()
  expect(screen.queryByRole('heading', { name: 'Package & recipient' })).not.toBeInTheDocument()
})
