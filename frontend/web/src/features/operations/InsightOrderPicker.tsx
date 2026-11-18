import { useCallback, useEffect, useMemo, useState } from 'react'
import { Button, Form, Input, Pagination, Select, Tag } from 'antd'
import { useSearchParams } from 'react-router-dom'
import { CalendarDayPicker } from '../../components/CalendarPicker'
import { orderStatuses, type OrderStatus } from '../../types/customerOrder'
import { customerOrderApi } from '../../services/customerOrderApi'
import { orderStatusLabel } from '../../utils/orderDisplay'
import { findInsightOrders, type InsightLookupFilters } from './insightLookupApi'
import { validationFields } from './feedback'
import { ResourceState } from './OperationsUi'
import { useResource } from './useResource'
import './delivery-insight.css'

const pageSize = 20

interface OrderPickerProps {
  customer: boolean
  onSelect: (id: string, trackingNumber?: string) => void
}

export function InsightOrderPicker({ customer, onSelect }: OrderPickerProps) {
  return (
    <section className="operations-panel insight-panel">
      <h2>Choose a delivery</h2>
      {customer ? <OwnedOrders onSelect={onSelect} /> : <StaffOrders onSelect={onSelect} />}
    </section>
  )
}

function OwnedOrders({ onSelect }: Pick<OrderPickerProps, 'onSelect'>) {
  const [filter, setFilter] = useState({ page: 1, searchKeyword: '' })
  const load = useCallback((signal: AbortSignal) => customerOrderApi.list({
    page: filter.page,
    pageSize,
    searchKeyword: filter.searchKeyword || undefined,
  }, signal), [filter])
  const orders = useResource(load)
  return (
    <div className="insight-picker">
      <Input.Search
        aria-label="Filter your orders by tracking number"
        placeholder="Filter by tracking number"
        maxLength={50}
        allowClear
        enterButton="Search"
        onSearch={value => setFilter({ page: 1, searchKeyword: value.trim() })}
      />
      <ResourceState loading={orders.loading} error={orders.error} retry={orders.reload} />
      {orders.data && orders.data.items.length === 0 && <p>No orders match this search.</p>}
      {!!orders.data?.items.length && (
        <Select
          aria-label="Your delivery order"
          placeholder="Choose one of your orders"
          onChange={value => onSelect(value)}
          options={orders.data.items.map(order => ({ value: order.id, label: order.trackingNumber }))}
        />
      )}
      {(orders.data?.totalCount ?? 0) > pageSize && (
        <Pagination
          current={filter.page}
          pageSize={pageSize}
          total={orders.data?.totalCount}
          showSizeChanger={false}
          onChange={page => setFilter(previous => ({ ...previous, page }))}
        />
      )}
    </div>
  )
}

const filterFields = ['searchKeyword', 'status', 'fromDate', 'toDate'] as const

function lookupDate(value: string | null | undefined, field: 'fromDate' | 'toDate') {
  if (!value || !/^\d{4}-\d{2}-\d{2}$/.test(value) || value < '0001-01-01') return undefined
  if (field === 'fromDate' && value < '0001-01-02') return undefined
  if (field === 'toDate' && value > '9999-12-30') return undefined
  const time = Date.parse(value + 'T00:00:00Z')
  return Number.isFinite(time) && new Date(time).toISOString().slice(0, 10) === value ? value : undefined
}

function StaffOrders({ onSelect }: Pick<OrderPickerProps, 'onSelect'>) {
  const [form] = Form.useForm<InsightLookupFilters>()
  const [params, setParams] = useSearchParams()
  const searchKeyword = (params.get('tracking') ?? '').slice(0, 50).trim() || undefined
  const status = orderStatuses.find(value => value === params.get('status'))
  const fromDate = lookupDate(params.get('fromDate'), 'fromDate')
  const toDate = lookupDate(params.get('toDate'), 'toDate')
  const requestedPage = Number(params.get('page'))
  const page = Number.isInteger(requestedPage) && requestedPage >= 1 && requestedPage <= 2_147_483_647 ? requestedPage : 1
  const filters = useMemo(() => ({ searchKeyword, status, fromDate, toDate }), [searchKeyword, status, fromDate, toDate])
  const load = useCallback((signal: AbortSignal) => findInsightOrders(filters, page, pageSize, signal), [filters, page])
  const { data, loading, error, reload } = useResource(load)

  useEffect(() => form.setFieldsValue(filters), [filters, form])
  useEffect(() => {
    if (error) form.setFields(validationFields(error, filterFields))
  }, [error, form])

  function search(values: InsightLookupFilters, nextPage = 1) {
    const next = new URLSearchParams()
    const keyword = values.searchKeyword?.trim()
    if (keyword) next.set('tracking', keyword)
    if (values.status) next.set('status', values.status)
    if (values.fromDate) next.set('fromDate', values.fromDate)
    if (values.toDate) next.set('toDate', values.toDate)
    if (nextPage > 1) next.set('page', String(nextPage))
    if (next.toString() === params.toString()) reload()
    else setParams(next)
  }

  function clearFilters() {
    form.setFieldsValue({ searchKeyword: undefined, status: undefined, fromDate: undefined, toDate: undefined })
    search({})
  }

  return (
    <div className="insight-lookup">
      <Form form={form} layout="vertical" className="insight-lookup-filters" initialValues={filters} onFinish={values => search(values)}>
        <Form.Item name="searchKeyword" label="Tracking number">
          <Input placeholder="Enter a tracking number" maxLength={50} allowClear />
        </Form.Item>
        <Form.Item name="status" label="Status">
          <Select<OrderStatus> placeholder="All statuses" allowClear
            options={orderStatuses.map(value => ({ value, label: orderStatusLabel(value) }))} />
        </Form.Item>
        <Form.Item name="fromDate" label="Created from"
          rules={[{ validator: (_, value) => !value || lookupDate(value, 'fromDate')
            ? Promise.resolve() : Promise.reject(new Error('Choose a valid start date.')) }]}>
          <CalendarDayPicker />
        </Form.Item>
        <Form.Item name="toDate" label="Created through" dependencies={['fromDate']}
          rules={[{ validator: (_, value: string | undefined) => {
            if (!value) return Promise.resolve()
            if (!lookupDate(value, 'toDate')) return Promise.reject(new Error('Choose a valid end date.'))
            const start: string | undefined = form.getFieldValue('fromDate')
            return start && value < start ? Promise.reject(new Error('The end date must be on or after the start date.')) : Promise.resolve()
          } }]}>
          <CalendarDayPicker />
        </Form.Item>
        <div className="insight-lookup-actions">
          <Button type="primary" htmlType="submit">Search</Button>
          <Button onClick={clearFilters}>Clear filters</Button>
          <span>Creation dates are inclusive in Vietnam time (UTC+07).</span>
        </div>
      </Form>
      <ResourceState loading={loading} error={error} retry={reload} />
      {data && data.totalCount === 0 && <p role="status">No deliveries match this search. Check the tracking number or clear the filters.</p>}
      {data && data.totalCount > 0 && data.items.length === 0 && (
        <Button onClick={() => search(filters)}>Back to first page</Button>
      )}
      {!!data?.items.length && (
        <ul className="insight-lookup-results" aria-label="Matching deliveries">
          {data.items.map(order => (
            <li key={order.id}>
              <div>
                <strong>{order.trackingNumber}</strong>
                <span>{order.originStationName} → {order.destinationStationName}</span>
              </div>
              <Tag>{orderStatusLabel(order.status)}</Tag>
              <Button aria-label={`Open ${order.trackingNumber}`} onClick={() => onSelect(order.id, order.trackingNumber)}>Open delivery</Button>
            </li>
          ))}
        </ul>
      )}
      {data && data.totalPages > 1 && data.items.length > 0 && (
        <Pagination
          current={data.page}
          pageSize={data.pageSize}
          total={data.totalCount}
          showSizeChanger={false}
          onChange={nextPage => search(filters, nextPage)}
        />
      )}
    </div>
  )
}
