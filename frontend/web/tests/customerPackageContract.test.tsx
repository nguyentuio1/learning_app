import { afterEach, beforeEach, expect, it } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { ConfigProvider } from 'antd'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import MockAdapter from 'axios-mock-adapter'
import { CustomerPackageFormPage } from '../src/pages/CustomerPackageFormPage'
import { CustomerPackagesPage } from '../src/pages/CustomerPackagesPage'
import { apiClient } from '../src/services/apiClient'
import { session } from '../src/services/authSession'

const id = 'c41f8b0a-2d37-4a6b-8e1c-5f7a9d3b6e22'
const item = { id, packageName: 'Documents', weightGram: 0.01, lengthCm: 30, widthCm: 20, heightCm: 15, isFragile: false, packageImageUrl: 'https://storage.example.com/old.png' }
let mock: MockAdapter
beforeEach(() => { session.clear(); mock = new MockAdapter(apiClient) })
afterEach(() => mock.restore())
function form(editing = false) {
  return render(<ConfigProvider theme={{ token: { motion: false } }}><MemoryRouter initialEntries={[editing ? '/packages/' + id : '/packages/new']}><Routes>
    <Route path="/packages/new" element={<CustomerPackageFormPage />} />
    <Route path="/packages/:id" element={<CustomerPackageFormPage />} />
    <Route path="/packages" element={<h1>Package list</h1>} />
  </Routes></MemoryRouter></ConfigProvider>)
}
async function fill() {
  fireEvent.change(screen.getByLabelText('Package name'), { target: { value: 'Documents' } })
  for (const label of ['Weight (g)', 'Length (cm)', 'Width (cm)', 'Height (cm)']) {
    fireEvent.change(screen.getByLabelText(label), { target: { value: '0.01' } })
  }
}
it('preserves the current image in a full PUT and accepts two-decimal dimensions', async () => {
  mock.onGet('/api/packages/' + id).reply(200, item)
  mock.onPut('/api/packages/' + id).reply(200, item)
  form(true)
  await screen.findByDisplayValue('Documents')
  await userEvent.click(screen.getByRole('button', { name: 'Save changes' }))
  await screen.findByRole('heading', { name: 'Package list' })
  expect(JSON.parse(mock.history.put[0].data)).toEqual({
    packageName: item.packageName, weightGram: 0.01, lengthCm: 30, widthCm: 20, heightCm: 15,
    isFragile: false, packageImageUrl: item.packageImageUrl,
  })
})
it('retries only the photo after creation succeeds and upload fails', async () => {
  mock.onPost('/api/packages').reply(201, { ...item, packageImageUrl: null })
  mock.onPost('/api/packages/' + id + '/image').replyOnce(500)
  mock.onPost('/api/packages/' + id + '/image').reply(200, { imageUrl: item.packageImageUrl })
  const view = form()
  await fill()
  await userEvent.upload(view.container.querySelector('input[type="file"]') as HTMLInputElement, new File(['photo'], 'photo.png', { type: 'image/png' }))
  await userEvent.click(screen.getByRole('button', { name: 'Add package' }))
  const retry = await screen.findByRole('button', { name: 'Retry photo upload' })
  await waitFor(() => expect(retry).toBeEnabled())
  expect(screen.getByLabelText('Package name')).toBeDisabled()
  await userEvent.click(retry)
  await screen.findByRole('heading', { name: 'Package list' })
  expect(mock.history.post.filter(r => r.url === '/api/packages')).toHaveLength(1)
  const uploads = mock.history.post.filter(r => r.url?.endsWith('/image'))
  expect(uploads).toHaveLength(2)
  expect((uploads[1].data as FormData).get('file')).toBeInstanceOf(File)
  expect(mock.history.put).toHaveLength(0)
})
it('blocks dimensions above contract limits before sending a request', async () => {
  form()
  await fill()
  fireEvent.change(screen.getByLabelText('Weight (g)'), { target: { value: '3001' } })
  fireEvent.submit(screen.getByLabelText('Package name').closest('form')!)
  expect(await screen.findByText('Use 0.01 to 3000 grams.')).toBeVisible()
  expect(mock.history.post).toHaveLength(0)
})
it('does not allow saving an existing package when its data could not be loaded', async () => {
  mock.onGet('/api/packages/' + id).reply(404)
  form(true)
  expect(await screen.findByRole('button', { name: 'Save changes' })).toBeDisabled()
  expect(mock.history.put).toHaveLength(0)
})
it('keeps the package visible and shows an inline conflict when deletion is rejected', async () => {
  mock.onGet('/api/packages').reply(200, [item])
  mock.onDelete('/api/packages/' + id).reply(409, { detail: 'Package is linked to an order.' })
  render(<ConfigProvider theme={{ token: { motion: false } }}><MemoryRouter><CustomerPackagesPage /></MemoryRouter></ConfigProvider>)
  await userEvent.click(await screen.findByRole('button', { name: 'Delete', exact: true }))
  await userEvent.click(screen.getAllByRole('button', { name: 'Delete', exact: true }).at(-1)!)
  expect(await screen.findByRole('alert')).toHaveTextContent(/linked|order/i)
  expect(screen.getByText('Documents')).toBeVisible()
  expect(mock.history.delete).toHaveLength(1)
})
