import { afterAll, afterEach, beforeEach, expect, it, vi } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { ConfigProvider } from 'antd'
import MockAdapter from 'axios-mock-adapter'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { CustomerAssistantPage } from '../src/features/customerAssistant/CustomerAssistantPage'
import { apiClient } from '../src/services/apiClient'

vi.hoisted(() => vi.stubEnv('VITE_AI_ASSISTANT_SEND_ENABLED', 'true'))
vi.mock('../src/hooks/useAuth', () => ({
  useAuth: () => ({ user: { id: 'customer-1', role: { code: 'CUSTOMER' } } }),
}))

const sessionId = '83ec4120-6388-426f-80a7-c8b627998d32'
const orderId = 'e8a3d1f4-5b62-4c79-8a0e-2f6b9c1d7e33'
const sessionPath = '/api/ai-assistant/sessions'
const historyPath = sessionPath + '/' + sessionId + '/messages'
const sendPath = '/api/ai-assistant/messages'
const savedTitle = 'Saved conversation'
const savedMessage = 'Saved conversation message'
const orderName = 'DD-202611-0012'
const createdAt = '2026-11-16T08:24:00Z'

function paginated<T>(items: T[]) {
  return { items, page: 1, pageSize: 20, totalCount: items.length }
}

function sessionList(sessionTitle: string) {
  return paginated([{ id: sessionId, sessionTitle, createdAt }])
}

function history(messageText: string) {
  return { sessionId, ...paginated([{ id: '9223372036854775807', messageText, senderType: 'USER', createdAt }]) }
}

let mock: MockAdapter

beforeEach(() => {
  mock = new MockAdapter(apiClient)
  mock.onGet(sessionPath).reply(200, sessionList(savedTitle))
  mock.onGet(historyPath).reply(200, history(savedMessage))
  mock.onGet('/api/delivery-orders').reply(200, paginated([{ id: orderId, trackingNumber: orderName }]))
})

afterEach(() => mock.restore())
afterAll(() => vi.unstubAllEnvs())

function show() {
  render(
    <ConfigProvider theme={{ token: { motion: false } }}>
      <MemoryRouter initialEntries={['/delivery-assistant/' + sessionId]}>
        <Routes>
          <Route path="/delivery-assistant/:id" element={<CustomerAssistantPage />} />
        </Routes>
      </MemoryRouter>
    </ConfigProvider>,
  )
}

async function selectOrder() {
  fireEvent.mouseDown(screen.getByRole('combobox', { name: 'Order (optional)' }))
  fireEvent.click(await screen.findByText(orderName))
}

async function send() {
  fireEvent.change(screen.getByRole('textbox', { name: 'Message' }), { target: { value: 'Where is my order?' } })
  fireEvent.click(screen.getByRole('button', { name: 'Send message' }))
  await waitFor(() => expect(mock.history.post.length).toBeGreaterThan(0))
}

it('discards protected state after a send denial and restores only newly authorized data', async () => {
  mock.onPost(sendPath).reply(403)
  show()
  expect(await screen.findByText(savedMessage)).toBeVisible()
  expect(await screen.findByRole('link', { name: new RegExp(savedTitle) })).toBeVisible()
  await selectOrder()
  await send()

  const recheck = await screen.findByRole('button', { name: 'Check access' })
  expect(screen.queryByText(savedMessage)).not.toBeInTheDocument()
  expect(screen.queryByText(savedTitle)).not.toBeInTheDocument()
  expect(screen.queryByText(orderName)).not.toBeInTheDocument()
  expect(screen.queryByRole('button', { name: 'Send message' })).not.toBeInTheDocument()

  mock.onGet(sessionPath).reply(403)
  fireEvent.click(recheck)
  await waitFor(() => expect(mock.history.get.filter(request => request.url === sessionPath)).toHaveLength(2))
  await waitFor(() => expect(recheck).not.toHaveClass('ant-btn-loading'))
  expect(screen.queryByRole('textbox', { name: 'Message' })).not.toBeInTheDocument()
  expect(screen.queryByText(savedMessage)).not.toBeInTheDocument()

  mock.onGet(sessionPath).reply(200, sessionList('Current conversation'))
  mock.onGet(historyPath).reply(200, history('Current conversation message'))
  mock.onGet('/api/delivery-orders').reply(200, paginated([]))
  fireEvent.click(recheck)

  expect(await screen.findByText('Current conversation message')).toBeVisible()
  expect(await screen.findByRole('link', { name: /Current conversation/ })).toBeVisible()
  expect(screen.getByRole('textbox', { name: 'Message' })).toHaveValue('')
  expect(screen.getByRole('button', { name: 'Send message' })).toBeDisabled()
  expect(screen.queryByText(savedMessage)).not.toBeInTheDocument()
  expect(screen.queryByText(savedTitle)).not.toBeInTheDocument()
  expect(screen.queryByText(orderName)).not.toBeInTheDocument()
})

it('keeps the owned conversation when the selected order becomes unavailable', async () => {
  mock.onPost(sendPath).replyOnce(404)
  mock.onPost(sendPath).reply(200, {
    outcome: 'HELP', sessionId: null, userMessageId: null,
    reply: { id: null, senderType: 'BOT', messageText: 'Choose a delivery order.', createdAt },
  })
  show()
  expect(await screen.findByText(savedMessage)).toBeVisible()
  await waitFor(() => expect(screen.getByRole('combobox', { name: 'Order (optional)' })).toBeEnabled())
  await selectOrder()
  await send()

  expect(await screen.findByText(/The selected order may be unavailable/)).toBeVisible()
  expect(await screen.findByText(savedMessage)).toBeVisible()
  await waitFor(() => expect(screen.getByRole('button', { name: 'Send message' })).toBeEnabled())
  fireEvent.click(screen.getByRole('button', { name: 'Send message' }))
  expect(await screen.findByText('Choose a delivery order.')).toBeVisible()
  expect(mock.history.post.map(request => JSON.parse(request.data))).toEqual([
    { sessionId, orderId, message: 'Where is my order?' },
    { sessionId, orderId: null, message: 'Where is my order?' },
  ])
})

it('hides the history and editor when the session read confirms the conversation was removed', async () => {
  mock.onPost(sendPath).reply(404)
  show()
  expect(await screen.findByText(savedMessage)).toBeVisible()
  await waitFor(() => expect(screen.getByRole('combobox', { name: 'Order (optional)' })).toBeEnabled())
  await selectOrder()

  mock.onGet(historyPath).reply(404)
  mock.onGet(sessionPath).reply(200, paginated([]))
  await send()

  expect(await screen.findByText('This conversation is unavailable.')).toBeVisible()
  expect(await screen.findByText('No conversations have been saved.')).toBeVisible()
  expect(screen.queryByText(savedMessage)).not.toBeInTheDocument()
  expect(screen.queryByRole('button', { name: 'Send message' })).not.toBeInTheDocument()
  expect(screen.queryByRole('button', { name: 'Check access' })).not.toBeInTheDocument()
})
