import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { ConfigProvider } from 'antd'
import MockAdapter from 'axios-mock-adapter'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { CustomerNotificationsPage } from '../src/pages/CustomerNotificationsPage'
import { apiClient } from '../src/services/apiClient'
import { session } from '../src/services/authSession'

const mockNotifications = [
  {
    id: 'notif-1',
    orderId: 'order-101',
    type: 'ORDER_STATUS_CHANGED',
    title: 'Order Approved',
    message: 'Your delivery order DD-202611-0012 was approved.',
    isRead: false,
    createdAt: '2026-11-13T09:30:00Z',
    readAt: null,
  },
  {
    id: 'notif-2',
    orderId: 'order-102',
    type: 'ORDER_STATUS_CHANGED',
    title: 'Delivery in transit',
    message: 'Drone has taken off with parcel DD-202611-0015.',
    isRead: true,
    createdAt: '2026-11-13T08:00:00Z',
    readAt: '2026-11-13T08:10:00Z',
  },
]

let mock: MockAdapter

beforeEach(() => {
  session.clear()
  mock = new MockAdapter(apiClient)
  mock.onGet('/api/notifications/unread-count').reply(200, { unreadCount: 1 })
  mock.onGet('/api/notifications').reply(config => {
    const isRead = config.params?.isRead
    if (isRead === false) {
      return [
        200,
        {
          items: mockNotifications.filter(n => !n.isRead),
          page: 1,
          pageSize: 20,
          totalCount: 1,
          totalPages: 1,
        },
      ]
    }
    return [
      200,
      {
        items: mockNotifications,
        page: 1,
        pageSize: 20,
        totalCount: 2,
        totalPages: 1,
      },
    ]
  })
})

afterEach(() => {
  mock.restore()
})

function renderComponent() {
  render(
    <ConfigProvider theme={{ token: { motion: false } }}>
      <MemoryRouter initialEntries={['/notifications']}>
        <Routes>
          <Route path="/notifications" element={<CustomerNotificationsPage />} />
          <Route path="/orders/:id" element={<div>Order Detail View</div>} />
        </Routes>
      </MemoryRouter>
    </ConfigProvider>
  )
}

describe('CustomerNotificationsPage', () => {
  it('renders notification list with items, tags, timestamps and unread count', async () => {
    renderComponent()

    expect(await screen.findByText('Order Approved')).toBeInTheDocument()
    expect(screen.getByText('Your delivery order DD-202611-0012 was approved.')).toBeInTheDocument()
    expect(screen.getByText('Delivery in transit')).toBeInTheDocument()
    expect(screen.getByText('Drone has taken off with parcel DD-202611-0015.')).toBeInTheDocument()

    // Tags
    expect(screen.getByText('Unread', { selector: '.ant-tag' })).toBeInTheDocument()
    expect(screen.getByText('Read', { selector: '.ant-tag' })).toBeInTheDocument()

    // Unread count badge in filter
    expect(screen.getByRole('button', { name: 'Mark all as read' })).toBeEnabled()
  })

  it('filters notifications by unread only when Unread tab is clicked', async () => {
    renderComponent()
    await screen.findByText('Order Approved')
    expect(screen.getByText('Delivery in transit')).toBeInTheDocument()

    const unreadRadio = screen.getByRole('radio', { name: /Unread/i })
    fireEvent.click(unreadRadio)

    await waitFor(() => {
      expect(mock.history.get.some(req => req.params?.isRead === false)).toBe(true)
      expect(screen.queryByText('Delivery in transit')).not.toBeInTheDocument()
    })
    expect(screen.getByText('Order Approved')).toBeInTheDocument()
  })

  it('marks a single notification as read on button click', async () => {
    mock.onPut('/api/notifications/notif-1/read').reply(200, {
      ...mockNotifications[0],
      isRead: true,
      readAt: new Date().toISOString(),
    })

    renderComponent()
    expect(await screen.findByText('Order Approved')).toBeInTheDocument()

    const markBtn = screen.getByRole('button', { name: 'Mark as read' })
    await userEvent.click(markBtn)

    await waitFor(() => {
      expect(mock.history.put.some(req => req.url === '/api/notifications/notif-1/read')).toBe(true)
    })
  })

  it('marks all notifications as read when clicking Mark all as read', async () => {
    mock.onPut('/api/notifications/read-all').reply(200, {
      updatedCount: 1,
    })

    renderComponent()
    expect(await screen.findByText('Order Approved')).toBeInTheDocument()

    const markAllBtn = screen.getByRole('button', { name: 'Mark all as read' })
    await userEvent.click(markAllBtn)

    await waitFor(() => {
      expect(mock.history.put.some(req => req.url === '/api/notifications/read-all')).toBe(true)
    })
  })

  it('renders View order button for notification linked to an order and navigates to it', async () => {
    renderComponent()
    expect(await screen.findByText('Order Approved')).toBeInTheDocument()

    const viewOrderLinks = screen.getAllByRole('link', { name: 'View order' })
    expect(viewOrderLinks.length).toBeGreaterThan(0)
    expect(viewOrderLinks[0]).toHaveAttribute('href', '/orders/order-101')

    await userEvent.click(viewOrderLinks[0])
    expect(await screen.findByText('Order Detail View')).toBeInTheDocument()
  })

  it('renders empty state when there are no notifications', async () => {
    mock.onGet('/api/notifications').reply(200, {
      items: [],
      page: 1,
      pageSize: 20,
      totalCount: 0,
      totalPages: 0,
    })
    mock.onGet('/api/notifications/unread-count').reply(200, { unreadCount: 0 })

    renderComponent()
    expect(await screen.findByText('No notifications found.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Mark all as read' })).toBeDisabled()
  })
})
