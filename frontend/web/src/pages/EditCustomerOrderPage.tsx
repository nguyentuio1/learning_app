import { useEffect, useRef, useState } from 'react'
import axios from 'axios'
import { Alert, Button, Form, Input, Select, Spin } from 'antd'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { OrderReveal } from '../components/orders/OrderReveal'
import { useCustomerOrder } from '../hooks/useCustomerOrder'
import { orderEditLookupApi } from '../services/orderEditLookupApi'
import { orderMutationApi, type UpdateOrderValues } from '../services/orderMutationApi'
import type { CustomerOrderDetail } from '../types/customerOrder'
import type { OrderAddress, OrderStation } from '../types/orderResources'
import { orderFeedback } from '../utils/orderFeedback'
import './customer-orders.css'

function EditOrderForm({ order, onReload }: { order: CustomerOrderDetail; onReload: (message: string) => void }) {
  const navigate = useNavigate()
  const [lookups, setLookups] = useState<{ addresses: OrderAddress[]; stations: OrderStation[] }>()
  const [loadError, setLoadError] = useState('')
  const [error, setError] = useState('')
  const [saving, setSaving] = useState(false)
  const submitting = useRef(false)
  useEffect(() => {
    const controller = new AbortController()
    Promise.all([orderEditLookupApi.addresses(controller.signal), orderEditLookupApi.stations(controller.signal)])
      .then(([addresses, stations]) => { if (!controller.signal.aborted) setLookups({ addresses, stations: stations.filter(s => s.status === 'OPERATIONAL' && s.id !== order.originStationId) }) })
      .catch(cause => { if (!controller.signal.aborted) setLoadError(orderFeedback(cause, 'Unable to load available destinations and addresses.')) })
    return () => controller.abort()
  }, [order.originStationId])
  async function save(values: UpdateOrderValues) {
    if (submitting.current || order.status !== 'PENDING') return
    submitting.current = true; setSaving(true); setError('')
    try {
      await orderMutationApi.update(order.id, values)
      navigate(`/orders/${encodeURIComponent(order.id)}/manage`, { replace: true, state: { orderNotice: 'Order updated successfully.' } })
    } catch (cause) {
      if (axios.isAxiosError(cause) && (cause.response?.status === 409 || !cause.response || cause.response.status >= 500)) {
        onReload(cause.response?.status === 409 ? 'The order status changed. The latest details have been requested; editing is allowed only while pending.' : 'We could not confirm the update. Check the refreshed details before saving again.')
      } else setError(orderFeedback(cause, 'Unable to update this order.'))
    } finally { submitting.current = false; setSaving(false) }
  }
  if (loadError) return <Alert type="error" showIcon title={loadError} action={<Button onClick={() => onReload('')}>Try again</Button>} />
  if (!lookups) return <div className="order-loading" role="status"><Spin description="Loading edit choices" /></div>
  return <Form layout="vertical" disabled={saving} onFinish={save} initialValues={{ recipientName: order.recipientName, recipientPhone: order.recipientPhone, destinationStationId: order.destinationStationId, deliveryAddressId: order.deliveryAddressId }}>
    {error && <Alert type="error" showIcon title={error} />}
    <div className="order-columns"><OrderReveal><section className="order-panel"><h2>Update delivery details</h2>
      <div className="order-fields"><Form.Item name="recipientName" label="Recipient name" rules={[{ required: true, whitespace: true, max: 150, message: 'Enter a recipient name of up to 150 characters.' }]}><Input maxLength={150} autoComplete="name" /></Form.Item>
      <Form.Item name="recipientPhone" label="Recipient phone" rules={[{ required: true, pattern: /^\d{9,11}$/, message: 'Enter 9 to 11 digits.' }]}><Input maxLength={11} inputMode="tel" autoComplete="tel" /></Form.Item></div>
      <Form.Item name="destinationStationId" label="Destination station" rules={[{ required: true, message: 'Choose a destination station.' }, { validator(_, value) { return lookups.stations.some(s => s.id === value) ? Promise.resolve() : Promise.reject(new Error('Choose an operational destination different from the origin.')) } }]}>
        <Select options={lookups.stations.map(s => ({ value: s.id, label: s.name }))} />
      </Form.Item>
      <Form.Item name="deliveryAddressId" label="Saved delivery address (optional)" rules={[{ validator(_, value) { return !value || lookups.addresses.some(a => a.id === value) ? Promise.resolve() : Promise.reject(new Error('Choose an available address or clear this selection.')) } }]}>
        <Select allowClear placeholder="No saved address" options={lookups.addresses.map(a => ({ value: a.id, label: `${a.label} — ${a.addressLine}` }))} />
      </Form.Item>
      <Button type="primary" htmlType="submit" loading={saving}>Save changes</Button>
    </section></OrderReveal><OrderReveal className="order-panel order-summary"><h2>Order information</h2><dl className="order-facts"><div><dt>Tracking</dt><dd>{order.trackingNumber}</dd></div><div><dt>Package</dt><dd>{order.packageName || order.package?.packageName}</dd></div><div><dt>Origin</dt><dd>{order.originStationName}</dd></div></dl><p className="order-help">The package and origin station cannot be changed after creation.</p></OrderReveal></div>
  </Form>
}

export function EditCustomerOrderPage() {
  const { id = '' } = useParams()
  const { order, loading, error, refresh } = useCustomerOrder(id)
  const [notice, setNotice] = useState('')
  return <main className="order-page"><Link to={`/orders/${encodeURIComponent(id)}/manage`}>← Back to order</Link><header className="order-heading"><div><span className="order-eyebrow">YOUR DELIVERY</span><h1>Edit order</h1><p className="order-subtitle">Keep recipient and destination details up to date before approval.</p></div></header>
    {notice && <Alert type="info" showIcon title={notice} />}
    {loading ? <div role="status" className="order-loading"><Spin description="Loading order" /></div> : error ? <Alert type="error" showIcon title={error} action={<Button onClick={refresh}>Try again</Button>} /> : order && (order.status !== 'PENDING' ? <Alert type="warning" showIcon title="Only pending orders can be edited." /> : <EditOrderForm key={`${order.id}:${order.updatedAt}`} order={order} onReload={message => { setNotice(message); refresh() }} />)}
  </main>
}
