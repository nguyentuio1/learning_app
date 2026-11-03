import { useState } from 'react'
import { Alert, Button, Spin, Tag } from 'antd'
import { Link, useLocation, useParams } from 'react-router-dom'
import { CustomerOrderActions } from '../components/orders/CustomerOrderActions'
import { OrderReveal } from '../components/orders/OrderReveal'
import { useCustomerOrder } from '../hooks/useCustomerOrder'
import { orderStatusLabel } from '../utils/orderDisplay'
import './customer-orders.css'

// A focused mutation screen; the order list and full history belong to LA-112.
export function ManageCustomerOrderPage() {
  const { id = '' } = useParams()
  const { order, loading, error, refresh } = useCustomerOrder(id)
  const location = useLocation()
  const [notice, setNotice] = useState('')
  return <main className="order-page">
    <Link to="/profile">← My profile</Link>
    <header className="order-heading"><div><span className="order-eyebrow">YOUR DELIVERY</span><h1>Manage order</h1><p className="order-subtitle">Update a pending delivery or cancel an eligible order.</p></div></header>
    {(notice || location.state?.orderNotice) && <Alert type="info" showIcon title={notice || location.state.orderNotice} />}
    {loading ? <div role="status" className="order-loading"><Spin description="Loading order" /></div> : error ? <Alert type="error" showIcon title={error} action={<Button onClick={refresh}>Try again</Button>} /> : order && <OrderReveal>
      <section className="order-panel" aria-label="Manage delivery">
        <header className="order-heading"><div><h2>{order.trackingNumber}</h2><Tag className={`order-status order-status-${order.status.toLowerCase()}`}>{orderStatusLabel(order.status)}</Tag></div><Button onClick={refresh}>Refresh</Button></header>
        <dl className="order-facts">
          <div><dt>Package</dt><dd>{order.packageName}</dd></div>
          <div><dt>Recipient</dt><dd>{order.recipientName}</dd></div>
          <div><dt>Phone</dt><dd>{order.recipientPhone}</dd></div>
          <div><dt>Origin</dt><dd>{order.originStationName}</dd></div>
          <div><dt>Destination</dt><dd>{order.destinationStationName}</dd></div>
          <div><dt>Address</dt><dd>{order.deliveryAddress?.addressLine || 'No saved address'}</dd></div>
        </dl>
        <CustomerOrderActions order={order} onRefresh={message => { setNotice(message); refresh() }} />
        {order.cancelReason && <Alert type="info" showIcon title="Cancellation reason" description={order.cancelReason} />}
      </section>
    </OrderReveal>}
  </main>
}
