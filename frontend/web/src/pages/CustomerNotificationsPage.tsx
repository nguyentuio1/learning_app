import { useEffect, useState } from 'react'
import { Alert, Badge, Button, Card, Empty, Pagination, Radio, Space, Tag, Typography } from 'antd'
import dayjs from 'dayjs'
import { Link } from 'react-router-dom'
import { customerNotificationApi, type CustomerNotification, type NotificationPage } from '../services/customerNotificationApi'
import { OrderReveal } from '../components/orders/OrderReveal'
import './customer-notifications.css'

const { Text, Paragraph } = Typography

interface NotificationsState {
  data?: NotificationPage
  unreadCount: number
  error?: string
}

export function CustomerNotificationsPage() {
  const [filter, setFilter] = useState<'ALL' | 'UNREAD'>('ALL')
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(20)
  const [result, setResult] = useState<NotificationsState>()
  const [actionLoading, setActionLoading] = useState(false)
  const [actionError, setActionError] = useState<string>()
  const [refreshKey, setRefreshKey] = useState(0)

  const loading = !result
  const data = result?.data
  const unreadCount = result?.unreadCount ?? 0
  const error = actionError ?? result?.error

  useEffect(() => {
    const controller = new AbortController()
    const isReadParam = filter === 'UNREAD' ? false : undefined

    Promise.all([
      customerNotificationApi.list({ isRead: isReadParam, page, pageSize }, controller.signal),
      customerNotificationApi.unreadCount(controller.signal),
    ])
      .then(([pageData, count]) => {
        if (!controller.signal.aborted) {
          setResult({ data: pageData, unreadCount: count })
        }
      })
      .catch(() => {
        if (!controller.signal.aborted) {
          setResult({ unreadCount: 0, error: 'Unable to load notifications. Please try again.' })
        }
      })

    return () => controller.abort()
  }, [filter, page, pageSize, refreshKey])

  function reload() {
    setActionError(undefined)
    setResult(undefined)
    setRefreshKey(k => k + 1)
  }

  async function handleMarkAsRead(id: string) {
    try {
      setActionLoading(true)
      setActionError(undefined)
      await customerNotificationApi.markAsRead(id)
      reload()
    } catch {
      setActionError('Failed to mark notification as read.')
    } finally {
      setActionLoading(false)
    }
  }

  async function handleMarkAllAsRead() {
    try {
      setActionLoading(true)
      setActionError(undefined)
      await customerNotificationApi.markAllAsRead()
      reload()
    } catch {
      setActionError('Failed to mark all notifications as read.')
    } finally {
      setActionLoading(false)
    }
  }

  return (
    <main className="order-page notification-page">
      <header className="order-heading">
        <div>
          <span className="order-eyebrow">YOUR NOTIFICATIONS</span>
          <h1>Notifications</h1>
          <p className="order-subtitle">Review status updates and alerts for your delivery orders.</p>
        </div>
        <div className="notification-header-actions">
          <Button
            type="primary"
            onClick={handleMarkAllAsRead}
            loading={actionLoading}
            disabled={unreadCount === 0 || loading}
          >
            Mark all as read
          </Button>
        </div>
      </header>

      {error && (
        <Alert
          type="error"
          showIcon
          title={error}
          action={<Button size="small" onClick={reload}>Try again</Button>}
          style={{ marginBottom: 16 }}
        />
      )}

      <OrderReveal>
        <div className="notification-filter-bar">
          <Radio.Group
            value={filter}
            onChange={e => {
              setFilter(e.target.value)
              setPage(1)
              setActionError(undefined)
              setResult(undefined)
            }}
            optionType="button"
            buttonStyle="solid"
          >
            <Radio.Button value="ALL">All</Radio.Button>
            <Radio.Button value="UNREAD">
              Unread {unreadCount > 0 && <Badge count={unreadCount} style={{ marginLeft: 6, backgroundColor: '#52c41a' }} />}
            </Radio.Button>
          </Radio.Group>
        </div>
      </OrderReveal>

      <OrderReveal>
        <section className="notification-list-section" aria-label="Notifications list">
          {data?.items && data.items.length > 0 ? (
            <div className="notification-cards-container">
              {data.items.map((item: CustomerNotification) => (
                <Card
                  key={item.id}
                  className={`notification-card ${item.isRead ? 'read' : 'unread'}`}
                  variant="outlined"
                >
                  <div className="notification-card-header">
                    <Space orientation="horizontal" size="small">
                      {!item.isRead && <Badge status="processing" color="#1890ff" />}
                      <Text strong className="notification-card-title">{item.title}</Text>
                      {item.isRead ? (
                        <Tag color="default">Read</Tag>
                      ) : (
                        <Tag color="blue">Unread</Tag>
                      )}
                    </Space>
                    <Text type="secondary" className="notification-timestamp">
                      {dayjs(item.createdAt).format('YYYY-MM-DD HH:mm')}
                    </Text>
                  </div>

                  <Paragraph className="notification-message">
                    {item.message}
                  </Paragraph>

                  <div className="notification-card-footer">
                    <Space>
                      {!item.isRead && (
                        <Button
                          size="small"
                          onClick={() => handleMarkAsRead(item.id)}
                          loading={actionLoading}
                        >
                          Mark as read
                        </Button>
                      )}
                      {item.orderId && (
                        <Link to={`/orders/${encodeURIComponent(item.orderId)}`}>
                          <Button size="small" type="link">
                            View order
                          </Button>
                        </Link>
                      )}
                    </Space>
                  </div>
                </Card>
              ))}

              <div className="notification-pagination">
                <Pagination
                  current={page}
                  pageSize={pageSize}
                  total={data.totalCount}
                  showSizeChanger
                  pageSizeOptions={[10, 20, 50]}
                  onChange={(newPage, newPageSize) => {
                    setPage(newPageSize !== pageSize ? 1 : newPage)
                    setPageSize(newPageSize)
                    setActionError(undefined)
                    setResult(undefined)
                  }}
                  showTotal={total => `${total} ${total === 1 ? 'notification' : 'notifications'}`}
                />
              </div>
            </div>
          ) : (
            <Empty
              description="No notifications found."
              image={Empty.PRESENTED_IMAGE_SIMPLE}
              style={{ padding: '48px 0' }}
            />
          )}
        </section>
      </OrderReveal>
    </main>
  )
}
