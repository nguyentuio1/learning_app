import { Alert, Button, Spin, Tag } from 'antd'
import { OrderEmpty } from '../components/orders/OrderEmpty'
import { Link, useParams } from 'react-router-dom'
import { useCustomerOrder } from '../hooks/useCustomerOrder'
import { OrderReveal } from '../components/orders/OrderReveal'
import { orderStatusLabel, orderTime } from '../utils/orderDisplay'
import './customer-orders.css'

export function CustomerOrderDetailPage() {
  const { id = '' } = useParams()
  const { order, loading, error, refresh } = useCustomerOrder(id)
  return <main className="order-page"><Link to="/orders">← Back to orders</Link>
    {loading ? <div role="status" className="order-loading"><Spin description="Loading order" /></div> : error ? <Alert type="error" showIcon title={error} action={<Button onClick={refresh}>Try again</Button>} /> : order && <>
      <header className="order-heading"><div><span className="order-eyebrow">DELIVERY DETAILS</span><h1>{order.trackingNumber}</h1><Tag className={`order-status order-status-${order.status.toLowerCase()}`}>{orderStatusLabel(order.status)}</Tag><p className="order-subtitle">Created {orderTime(order.createdAt)}</p></div>
        <div className="order-actions">
          {(order.status === 'PENDING' || order.status === 'APPROVED') && <Link className="order-primary-link" to={`/orders/${encodeURIComponent(order.id)}/manage`}>
            {order.status === 'PENDING' ? 'Edit or cancel order' : 'Cancel order'}
          </Link>}
          <Button onClick={refresh}>Refresh</Button>
        </div>
      </header>
      <div className="order-columns"><div>
        <OrderReveal><section className="order-panel"><h2>Package & recipient</h2><dl className="order-facts">
          <div><dt>Package</dt><dd>{order.packageName}</dd></div><div><dt>Weight</dt><dd>{order.package?.weightGram ?? '—'} g</dd></div>
          <div><dt>Dimensions</dt><dd>{order.package ? `${order.package.lengthCm} × ${order.package.widthCm} × ${order.package.heightCm} cm` : '—'}</dd></div>
          <div><dt>Handling</dt><dd>{order.package?.isFragile ? 'Fragile — handle with care' : 'Standard'}</dd></div>
          <div><dt>Recipient</dt><dd>{order.recipientName}</dd></div><div><dt>Phone</dt><dd>{order.recipientPhone}</dd></div>
          <div><dt>Address</dt><dd>{order.deliveryAddress?.addressLine || 'No saved address'}</dd></div>
        </dl></section></OrderReveal>
        <OrderReveal><section className="order-panel"><h2>Status history</h2>{order.statusLogs?.length ? <ol className="order-timeline">{[...order.statusLogs].sort((a, b) => Date.parse(a.createdAt) - Date.parse(b.createdAt)).map((log, i) => <li key={`${log.createdAt}-${i}`}><span className="order-timeline-dot" aria-hidden="true"/><strong>{log.fromStatus ? `${orderStatusLabel(log.fromStatus)} → ` : ''}{orderStatusLabel(log.toStatus)}</strong><time dateTime={log.createdAt}>{orderTime(log.createdAt)}</time>{log.remarks && <p>{log.remarks}</p>}</li>)}</ol> : <OrderEmpty title="No status history is available yet." description="Updates will appear here as your delivery progresses." />}</section></OrderReveal>
      </div><div><OrderReveal><section className="order-panel"><h2>Delivery route</h2><div className="order-route"><div><small>FROM</small><strong>{order.originStationName}</strong><p>{order.originStation?.address}</p></div><div aria-hidden="true">↓</div><div><small>TO</small><strong>{order.destinationStationName}</strong><p>{order.destinationStation?.address}</p></div></div>
        <dl className="order-facts"><div><dt>Scheduled departure</dt><dd>{orderTime(order.scheduledDepartureTime)}</dd></div><div><dt>Departed</dt><dd>{orderTime(order.actualDepartureTime)}</dd></div><div><dt>Estimated delivery</dt><dd>{orderTime(order.estimatedDeliveryTime)}</dd></div><div><dt>Delivered</dt><dd>{orderTime(order.actualDeliveryTime)}</dd></div></dl>
      </section></OrderReveal>{order.cancelReason && <Alert type="info" showIcon title="Cancellation reason" description={order.cancelReason} />}</div></div>
    </>}
  </main>
}
