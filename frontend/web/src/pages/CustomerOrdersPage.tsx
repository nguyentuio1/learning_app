import { useEffect, useState } from 'react'
import { Alert, Button, Form, Input, Pagination, Select, Spin, Table, Tag } from 'antd'
import { CalendarDayPicker } from '../components/CalendarPicker'
import { Link } from 'react-router-dom'
import { OrderReveal } from '../components/orders/OrderReveal'
import { OrderEmpty } from '../components/orders/OrderEmpty'
import { customerOrderApi } from '../services/customerOrderApi'
import { orderStatuses, type CustomerOrder, type OrderPage, type OrderQuery, type OrderStatus } from '../types/customerOrder'
import { orderFeedback } from '../utils/orderFeedback'
import { orderDateBoundary, orderStatusLabel, orderTime } from '../utils/orderDisplay'
import './customer-orders.css'

interface Filters { status?: OrderStatus; searchKeyword?: string; fromDate?: string; toDate?: string }
const narrowScreen = '(max-width: 1140px)'

function OrderStatusTag({ status }: { status: OrderStatus }) {
  return <Tag className={`order-status order-status-${status.toLowerCase()}`}>{orderStatusLabel(status)}</Tag>
}

function OrderCard({ order }: { order: CustomerOrder }) {
  const path = `/orders/${encodeURIComponent(order.id)}`
  const tracking = order.status === 'READY_FOR_TAKEOFF' || order.status === 'IN_TRANSIT'
  const href = order.status === 'PENDING' ? path + '/manage' : tracking ? path + '/map' : path
  const action = order.status === 'PENDING' ? 'Edit or cancel order' : tracking ? 'Track delivery' : 'View order'
  return <li className="order-panel order-list-card">
    <div className="order-list-card-top"><Link to={path}>{order.trackingNumber}</Link><OrderStatusTag status={order.status} /></div>
    <p>{order.originStationName} → {order.destinationStationName}</p>
    <Link to={href} className="order-primary-link" aria-label={`${action} · ${order.trackingNumber}`}>{action}</Link>
  </li>
}

export function CustomerOrdersPage() {
  const [form] = Form.useForm<Filters>()
  const [narrow, setNarrow] = useState(() => window.matchMedia(narrowScreen).matches)
  const [showDates, setShowDates] = useState(false)
  useEffect(() => {
    const media = window.matchMedia(narrowScreen)
    const update = () => setNarrow(media.matches)
    media.addEventListener('change', update)
    return () => media.removeEventListener('change', update)
  }, [])
  const [query, setQuery] = useState<OrderQuery>({ page: 1, pageSize: 20 })
  const [result, setResult] = useState<{ data?: OrderPage; error?: string }>()
  const [attempt, setAttempt] = useState(0)
  useEffect(() => {
    const controller = new AbortController()
    customerOrderApi.list(query, controller.signal)
      .then(data => { if (!controller.signal.aborted) setResult({ data }) })
      .catch(error => { if (!controller.signal.aborted) setResult({ error: orderFeedback(error, 'Unable to load your orders.') }) })
    return () => controller.abort()
  }, [query, attempt])
  function apply(values: Filters) {
    setResult(undefined)
    setQuery({ page: 1, pageSize: query.pageSize, status: values.status || undefined, searchKeyword: values.searchKeyword?.trim() || undefined,
      fromDate: orderDateBoundary(values.fromDate), toDate: orderDateBoundary(values.toDate, true) })
  }
  const columns = [
    { title: 'Order', dataIndex: 'trackingNumber', key: 'tracking', render: (value: string, row: CustomerOrder) => <Link to={`/orders/${encodeURIComponent(row.id)}`}>{value}</Link> },
    { title: 'Package / recipient', key: 'recipient', render: (_: unknown, row: CustomerOrder) => <><strong>{row.packageName}</strong><small className="order-cell-secondary">{row.recipientName}</small></> },
    { title: 'Route', key: 'route', render: (_: unknown, row: CustomerOrder) => <>{row.originStationName}<small className="order-cell-secondary">→ {row.destinationStationName}</small></> },
    { title: 'Status', dataIndex: 'status', key: 'status', render: (status: OrderStatus) => <OrderStatusTag status={status} /> },
    { title: 'Created', dataIndex: 'createdAt', key: 'created', render: (date: string) => orderTime(date) },
  ]
  const pagination = { current: query.page, pageSize: query.pageSize, total: result?.data?.totalCount || 0,
    showSizeChanger: !narrow, pageSizeOptions: [10, 20, 50, 100],
    showTotal: (total: number) => `${total} ${total === 1 ? 'order' : 'orders'}`,
    onChange: (page: number, pageSize: number) => {
      setResult(undefined)
      setQuery({ ...query, page: pageSize !== query.pageSize ? 1 : page, pageSize })
    } }
  const filtered = Boolean(query.status || query.searchKeyword || query.fromDate || query.toDate)
  const empty = <OrderEmpty title={filtered ? 'No orders match these filters.' : 'No orders yet.'}
    description={filtered ? 'Try another tracking number or reset the filters to see all your deliveries.' : 'Create a delivery to send a package between two landing stations.'} />
  return <main className="order-page"><header className="order-heading"><div><span className="order-eyebrow">YOUR DELIVERIES</span><h1>My orders</h1><p className="order-subtitle">Follow each delivery, from your request to collection.</p></div><Link className="order-primary-link" to="/orders/new">Create delivery</Link></header>
    <OrderReveal><section className="order-panel order-filter-panel" aria-label="Filter orders"><Form form={form} layout="vertical" onFinish={apply} onFinishFailed={() => setShowDates(true)}>
      <div className="order-filter-grid"><Form.Item name="searchKeyword" label="Tracking number" rules={[{ max: 50 }]}><Input maxLength={50} placeholder="Search tracking number" allowClear /></Form.Item>
        <Form.Item name="status" label="Status"><Select allowClear placeholder="All statuses" options={orderStatuses.map(value => ({ value, label: orderStatusLabel(value) }))} /></Form.Item>
      </div>
      {narrow && <Button type="text" className="order-date-toggle" aria-expanded={showDates} aria-controls="order-date-filters"
        onClick={() => setShowDates(value => !value)}>{showDates ? 'Hide date filters' : 'Show date filters'}</Button>}
      <div className="order-filter-grid order-date-filters" id="order-date-filters" hidden={narrow && !showDates}>
        <Form.Item name="fromDate" label="Created from"><CalendarDayPicker placeholder="Start date" /></Form.Item>
        <Form.Item name="toDate" label="Created through" dependencies={['fromDate']} rules={[({ getFieldValue }) => ({ validator(_, value) { const from = getFieldValue('fromDate'); return !value || !from || value >= from ? Promise.resolve() : Promise.reject(new Error('End date must not be before start date.')) } })]}><CalendarDayPicker placeholder="End date" /></Form.Item>
      </div><div className="order-actions"><Button htmlType="submit" type="primary">Apply filters</Button><Button onClick={() => { form.resetFields(); apply({}) }}>Reset</Button><Button onClick={() => { setResult(undefined); setAttempt(x => x + 1) }}>Refresh</Button></div>
    </Form></section></OrderReveal>
    {result?.error ? <Alert type="error" showIcon title={result.error} action={<Button onClick={() => { setResult(undefined); setAttempt(x => x + 1) }}>Try again</Button>} /> :
      narrow ? <section aria-label="Orders" className="order-list-mobile">
        {!result ? <div className="order-loading" role="status"><Spin description="Loading orders" /></div>
          : result.data?.items.length ? <><ul>{result.data.items.map(order => <OrderCard key={order.id} order={order} />)}</ul>
            <Pagination {...pagination} simple /></> : empty}
      </section> : <OrderReveal><section className="order-table" aria-label="Orders"><Table<CustomerOrder> rowKey="id" columns={columns} dataSource={result?.data?.items || []} loading={!result} scroll={{ x: 840 }} locale={{ emptyText: empty }} pagination={pagination} /></section></OrderReveal>}
  </main>
}
