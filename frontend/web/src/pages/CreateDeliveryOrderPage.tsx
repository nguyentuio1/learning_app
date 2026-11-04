import { useEffect, useRef, useState } from 'react'
import axios from 'axios'
import { Alert, Button, Checkbox, Form, Input, Modal, Select, Spin } from 'antd'
import { orderCreationApi } from '../services/orderCreationApi'
import type { CreatedOrder, CreateOrderValues, OrderAddress, OrderPackage, OrderStation } from '../types/orderCreation'
import { orderFeedback } from '../utils/orderFeedback'
import { OrderReveal } from '../components/orders/OrderReveal'
import './customer-orders.css'

export function CreateDeliveryOrderPage() {
  const [form] = Form.useForm<CreateOrderValues>()
  const values = Form.useWatch([], form) as Partial<CreateOrderValues> | undefined
  const [packages, setPackages] = useState<OrderPackage[]>([])
  const [addresses, setAddresses] = useState<OrderAddress[]>([])
  const [stations, setStations] = useState<OrderStation[]>([])
  const [loading, setLoading] = useState(true)
  const [loadIssue, setLoadIssue] = useState('')
  const [issue, setIssue] = useState('')
  const [attempt, setAttempt] = useState(0)
  const [review, setReview] = useState<CreateOrderValues | null>(null)
  const [saving, setSaving] = useState(false)
  const submitting = useRef(false)
  const [uncertain, setUncertain] = useState(false)
  const [created, setCreated] = useState<CreatedOrder | null>(null)
  const [notedCode, setNotedCode] = useState(false)
  useEffect(() => {
    const controller = new AbortController()
    Promise.all([orderCreationApi.packages(controller.signal), orderCreationApi.addresses(controller.signal), orderCreationApi.stations(controller.signal)])
      .then(([p, a, s]) => {
        if (controller.signal.aborted) return
        const operational = s.filter(x => x.status === 'OPERATIONAL')
        setPackages(p); setAddresses(a); setStations(operational)
        // A refresh after a conflict must not retain a removed or unavailable choice.
        for (const [field, choices] of [['packageId', p], ['originStationId', operational], ['destinationStationId', operational], ['deliveryAddressId', a]] as const) {
          const selected = form.getFieldValue(field)
          if (selected && !choices.some(choice => choice.id === selected)) form.setFieldValue(field, undefined)
        }
      }).catch(error => { if (!controller.signal.aborted) setLoadIssue(orderFeedback(error, 'Unable to load the choices for this order.')) })
      .finally(() => { if (!controller.signal.aborted) setLoading(false) })
    return () => controller.abort()
  }, [attempt, form])
  function reload() { setLoading(true); setLoadIssue(''); setIssue(''); setAttempt(x => x + 1) }
  const selectedPackage = packages.find(p => p.id === values?.packageId)
  const stationName = (id?: string) => stations.find(s => s.id === id)?.name || 'Choose a station'
  async function create() {
    if (!review || submitting.current || uncertain) return
    submitting.current = true; setSaving(true); setIssue('')
    try { setCreated(await orderCreationApi.create(review)); setReview(null) }
    catch (error) {
      setReview(null)
      if (axios.isAxiosError(error) && (!error.response || error.response.status >= 500)) {
        setUncertain(true)
        setIssue('We could not confirm whether this order was created. Check your order history before trying again to avoid a duplicate.')
      } else setIssue(orderFeedback(error, 'Unable to create the order.'))
    } finally { submitting.current = false; setSaving(false) }
  }
  if (created) return <main className="order-page order-success"><OrderReveal><section className="order-panel">
    <span className="order-eyebrow">ORDER RECEIVED</span><h1>Ready for review</h1>
    <p>Your order <strong>{created.trackingNumber}</strong> has been submitted.</p>
    <Alert type="warning" showIcon title="Save your pickup code now" description="This code is shown only once. Keep it private and share it only with the recipient." />
    {/^\d{6}$/.test(created.pickupSecureCode) ? <output className="order-code" aria-label="Pickup code">{created.pickupSecureCode}</output> : <Alert type="error" title="The server did not return a valid pickup code. Contact support with your tracking number. Do not create the same order again." />}
    <Checkbox checked={notedCode} onChange={e => setNotedCode(e.target.checked)}>I have saved the tracking number and pickup information.</Checkbox>
    <p><Button disabled={!notedCode} onClick={() => { setCreated(null); setNotedCode(false); form.resetFields(); reload() }}>Create another order</Button></p>
  </section></OrderReveal></main>
  return <main className="order-page">
    <header className="order-heading"><div><span className="order-eyebrow">YOUR DELIVERY</span><h1>Create delivery order</h1><p className="order-subtitle">Choose your package, plan the route and tell us who will receive it.</p></div></header>
    {loadIssue ? <Alert type="error" showIcon title={loadIssue} action={<Button onClick={reload}>Try again</Button>} /> : loading ? <div className="order-loading" role="status"><Spin description="Loading order choices" /></div> :
      <Form form={form} layout="vertical" onFinish={setReview} disabled={saving || uncertain} initialValues={{ deliveryAddressId: null }}>
        {issue && <Alert type="error" showIcon title={issue} action={uncertain ? <Button disabled={false} onClick={() => { setUncertain(false); setIssue(''); reload() }}>I checked my orders</Button> : <Button onClick={reload}>Refresh choices</Button>} />}
        {(!packages.length || stations.length < 2) && <Alert type="info" showIcon title={!packages.length ? 'Add a package before creating an order.' : 'Two operational stations are needed to create a delivery.'} />}
        <div className="order-columns"><div>
          <OrderReveal><section className="order-panel"><h2><span className="order-step">01</span>Package & route</h2>
            <Form.Item name="packageId" label="Package" rules={[{ required: true, message: 'Choose a package.' }]}><Select placeholder="Choose a saved package" options={packages.map(p => ({ value: p.id, label: p.packageName + ' · ' + p.weightGram + ' g', disabled: p.weightGram <= 0 || p.weightGram > 3000 || p.lengthCm <= 0 || p.lengthCm > 30 || p.widthCm <= 0 || p.widthCm > 20 || p.heightCm <= 0 || p.heightCm > 15 }))} /></Form.Item>
            <div className="order-fields">
              <Form.Item name="originStationId" label="Origin station" rules={[{ required: true, message: 'Choose an origin station.' }]}><Select placeholder="Where it starts" options={stations.map(s => ({ value: s.id, label: s.name }))} /></Form.Item>
              <Form.Item name="destinationStationId" label="Destination station" dependencies={['originStationId']} rules={[{ required: true, message: 'Choose a destination station.' }, ({ getFieldValue }) => ({ validator(_, value) { return !value || value !== getFieldValue('originStationId') ? Promise.resolve() : Promise.reject(new Error('Choose a different destination station.')) } })]}><Select placeholder="Where it arrives" options={stations.map(s => ({ value: s.id, label: s.name, disabled: s.id === values?.originStationId }))} /></Form.Item>
            </div>
          </section></OrderReveal>
          <OrderReveal><section className="order-panel"><h2><span className="order-step">02</span>Recipient</h2>
            <div className="order-fields"><Form.Item name="recipientName" label="Recipient name" rules={[{ required: true, whitespace: true, max: 150, message: 'Enter a recipient name of up to 150 characters.' }]}><Input maxLength={150} autoComplete="name" /></Form.Item>
            <Form.Item name="recipientPhone" label="Recipient phone" rules={[{ required: true, pattern: /^\d{9,11}$/, message: 'Enter 9 to 11 digits.' }]}><Input inputMode="tel" autoComplete="tel" maxLength={11} /></Form.Item></div>
            <Form.Item name="deliveryAddressId" label="Saved delivery address (optional)"><Select allowClear placeholder="No saved address" options={addresses.map(a => ({ value: a.id, label: a.label + ' — ' + a.addressLine }))} /></Form.Item>
          </section></OrderReveal>
          <Button type="primary" htmlType="submit" disabled={!packages.length || stations.length < 2 || uncertain} loading={saving}>Review order</Button>
        </div><OrderReveal className="order-summary order-panel"><h2>Delivery summary</h2><div className="order-route"><div><small>FROM</small><strong>{stationName(values?.originStationId)}</strong></div><div aria-hidden="true">↓</div><div><small>TO</small><strong>{stationName(values?.destinationStationId)}</strong></div></div>
          <dl className="order-facts"><div><dt>Package</dt><dd>{selectedPackage?.packageName || 'Not selected'}</dd></div><div><dt>Weight</dt><dd>{selectedPackage ? selectedPackage.weightGram + ' g' : '—'}</dd></div><div><dt>Dimensions</dt><dd>{selectedPackage ? `${selectedPackage.lengthCm} × ${selectedPackage.widthCm} × ${selectedPackage.heightCm} cm` : '—'}</dd></div><div><dt>Recipient</dt><dd>{values?.recipientName || 'Not entered'}</dd></div></dl><p className="order-help">Your request will be reviewed before a drone is assigned.</p>
        </OrderReveal></div>
      </Form>}
    <Modal title="Confirm delivery order" open={review !== null} onOk={() => void create()} onCancel={() => { if (!saving) setReview(null) }} confirmLoading={saving} cancelButtonProps={{ disabled: saving }} closable={!saving} mask={{ closable: !saving }} keyboard={!saving} okText="Create order" destroyOnHidden>
      <p>Review these details before submitting your delivery.</p>
      <dl className="order-confirm-summary"><dt>Package</dt><dd>{packages.find(p => p.id === review?.packageId)?.packageName}</dd><dt>Route</dt><dd>{stationName(review?.originStationId)} → {stationName(review?.destinationStationId)}</dd><dt>Recipient</dt><dd>{review?.recipientName} · {review?.recipientPhone}</dd></dl>
      <p>The pickup code will be displayed once after creation.</p>
    </Modal>
  </main>
}
