import { useCallback, useState } from 'react'
import { orderIdPattern } from './deliveryInsight'
import { Button, Form, Input, Pagination, Select } from 'antd'
import { customerOrderApi } from '../../services/customerOrderApi'
import { trackingApi } from '../tracking/trackingApi'
import { ResourceState } from './OperationsUi'
import { useResource } from './useResource'
import './delivery-insight.css'

const pageSize = 20

interface OrderPickerProps {
  customer: boolean
  onSelect: (id: string) => void
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
          onChange={onSelect}
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

function StaffOrders({ onSelect }: Pick<OrderPickerProps, 'onSelect'>) {
  const active = useResource(trackingApi.active)
  return (
    <div className="insight-picker">
      <ResourceState loading={active.loading} error={active.error} retry={active.reload} />
      {active.data?.length ? (
        <Select
          aria-label="Active delivery"
          placeholder="Choose an active delivery"
          showSearch
          optionFilterProp="label"
          onChange={onSelect}
          options={active.data.map(order => ({ value: order.orderId, label: order.trackingNumber }))}
        />
      ) : active.data && <p>No active deliveries are available.</p>}
      <Form<{ orderId: string }> layout="vertical" onFinish={({ orderId }) => onSelect(orderId.trim())}>
        <Form.Item
          name="orderId"
          label="Open another delivery by order ID"
          rules={[
            { required: true, message: 'Enter the order ID from the delivery record.' },
            { validator: (_, value: string | undefined) =>
              !value || orderIdPattern.test(value.trim()) ? Promise.resolve() : Promise.reject(new Error('Enter a valid order ID.')) },
          ]}
        >
          <Input placeholder="Order UUID" />
        </Form.Item>
        <Button htmlType="submit">Open delivery</Button>
      </Form>
    </div>
  )
}
