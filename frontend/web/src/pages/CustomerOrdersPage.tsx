import { useEffect, useState } from 'react'
import { Alert, Button, DatePicker, Form, Input, Select, Table, Tag } from 'antd'
import dayjs from 'dayjs'
import { Link } from 'react-router-dom'
import { OrderReveal } from '../components/orders/OrderReveal'
import { OrderEmpty } from '../components/orders/OrderEmpty'
import { customerOrderApi } from '../services/customerOrderApi'
import { orderStatuses, type CustomerOrder, type OrderPage, type OrderQuery, type OrderStatus } from '../types/customerOrder'
import { orderFeedback } from '../utils/orderFeedback'
import { orderDateBoundary, orderStatusLabel, orderTime } from '../utils/orderDisplay'
import './customer-orders.css'

interface Filters { status?: OrderStatus; searchKeyword?: string; fromDate?: string; toDate?: string }
export function CustomerOrdersPage() {
  const [form] = Form.useForm<Filters>()
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
    { title: 'Status', dataIndex: 'status', key: 'status', render: (status: string) => <Tag className={`order-status order-status-${status.toLowerCase()}`}>{orderStatusLabel(status)}</Tag> },
    { title: 'Created', dataIndex: 'createdAt', key: 'created', render: (date: string) => orderTime(date) },
  ]
  return <main className="order-page"><header className="order-heading"><div><span className="order-eyebrow">YOUR DELIVERIES</span><h1>My orders</h1><p className="order-subtitle">Follow each delivery, from your request to collection.</p></div></header>
    <OrderReveal><section className="order-panel order-filter-panel" aria-label="Filter orders"><Form form={form} layout="vertical" onFinish={apply}>
      <div className="order-filter-grid"><Form.Item name="searchKeyword" label="Tracking number" rules={[{ max: 50 }]}><Input maxLength={50} placeholder="Search tracking number" allowClear /></Form.Item>
        <Form.Item name="status" label="Status"><Select allowClear placeholder="All statuses" options={orderStatuses.map(value => ({ value, label: orderStatusLabel(value) }))} /></Form.Item>
        <Form.Item name="fromDate" label="Created from" getValueProps={value => ({ value: value ? dayjs(value) : null })} normalize={value => value?.format('YYYY-MM-DD')}><DatePicker format="YYYY-MM-DD" placeholder="Start date" /></Form.Item>
        <Form.Item name="toDate" label="Created through" getValueProps={value => ({ value: value ? dayjs(value) : null })} normalize={value => value?.format('YYYY-MM-DD')} dependencies={['fromDate']} rules={[({ getFieldValue }) => ({ validator(_, value) { const from = getFieldValue('fromDate'); return !value || !from || value >= from ? Promise.resolve() : Promise.reject(new Error('End date must not be before start date.')) } })]}><DatePicker format="YYYY-MM-DD" placeholder="End date" /></Form.Item>
      </div><div className="order-actions"><Button htmlType="submit" type="primary">Apply filters</Button><Button onClick={() => { form.resetFields(); apply({}) }}>Reset</Button><Button onClick={() => { setResult(undefined); setAttempt(x => x + 1) }}>Refresh</Button></div>
    </Form></section></OrderReveal>
    {result?.error ? <Alert type="error" showIcon title={result.error} action={<Button onClick={() => { setResult(undefined); setAttempt(x => x + 1) }}>Try again</Button>} /> :
      <OrderReveal><section className="order-table" aria-label="Orders"><Table<CustomerOrder> rowKey="id" columns={columns} dataSource={result?.data?.items || []} loading={!result} scroll={{ x: 840 }} locale={{ emptyText: <OrderEmpty title="No orders match these filters." description="Try another tracking number or reset the filters to see all your deliveries." /> }} pagination={{ current: query.page, pageSize: query.pageSize, total: result?.data?.totalCount || 0, showSizeChanger: true, pageSizeOptions: [10, 20, 50, 100], showTotal: total => `${total} ${total === 1 ? 'order' : 'orders'}`, onChange: (page, pageSize) => { setResult(undefined); setQuery({ ...query, page: pageSize !== query.pageSize ? 1 : page, pageSize }) } }} /></section></OrderReveal>}
  </main>
}
