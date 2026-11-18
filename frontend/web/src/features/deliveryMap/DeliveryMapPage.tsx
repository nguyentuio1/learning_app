import { useCallback, useEffect, useState } from 'react'
import { Alert, Button, Select, Tag } from 'antd'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { useAuth } from '../../hooks/useAuth'
import { orderStatusLabel } from '../../utils/orderDisplay'
import { OperationsPage, ResourceState } from '../operations/OperationsUi'
import { trackingApi, type TrackingSnapshot } from './trackingApi'
import { useTrackingPoll } from './useTrackingPoll'
import { useTrackingLive } from './useTrackingLive'
import { DeliveryMap } from './DeliveryMap'
import './delivery-map.css'

const uuid = /^[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}$/i
export function DeliveryMapPage() {
  const { id } = useParams()
  const { user } = useAuth()
  return <div className="delivery-map-feature"><OperationsPage title="Delivery map" subtitle="See the stations and the latest position reported for a delivery."
    action={id && <Link className="dm-link" to="/delivery-map">← Active delivery maps</Link>}>
    {!id ? <ActiveMaps role={user?.role.code} />
      : uuid.test(id) ? <SelectedMap key={`${user?.id}:${id}`} id={id}/> : <Alert type="error" showIcon title="Invalid order ID" description="Choose a delivery from the list."/>}
  </OperationsPage></div>
}

export function CustomerDeliveryMapPage() {
  const { id = '' } = useParams()
  const { user } = useAuth()
  return <div className="delivery-map-feature"><OperationsPage title="Delivery map" subtitle="See the route and tracking updates for your order."
    action={<Link className="dm-link" to={uuid.test(id) ? `/orders/${encodeURIComponent(id)}` : '/orders'}>← {uuid.test(id) ? 'Order details' : 'My orders'}</Link>}>
    {uuid.test(id) ? <SelectedMap key={`${user?.id}:${id}`} id={id} /> : <Alert type="error" showIcon title="Order unavailable" description="Open the map from My orders." />}
  </OperationsPage></div>
}

function ActiveMaps({ role }: { role?: string }) {
  const navigate = useNavigate()
  const list = useTrackingPoll(trackingApi.active)
  const canSchedule = role === 'ADMIN' || role === 'DISPATCHER'
  return <>
    <div className="dm-selection">
      {!!list.data?.length && <label>Active delivery<Select aria-label="Active delivery" showSearch optionFilterProp="label" placeholder="Choose a delivery"
        onChange={value => navigate('/delivery-map/' + encodeURIComponent(value))} options={list.data.map(order => ({ value: order.orderId, label: `${order.trackingNumber} · ${order.droneSerialNumber}` }))}/></label>}
      <Button onClick={list.reload} loading={list.refreshing}>Refresh deliveries</Button>
    </div>
    <ResourceState loading={list.loading} error={list.error} retry={list.reload}/>
    {list.error && list.data && <p>The picker shows the last received list. Refresh to check current availability.</p>}
    {!list.error && list.data?.length === 0 && <section className="dm-card dm-empty">
      <h2>No active deliveries</h2>
      <p>Maps appear here once a drone is assigned to a planned or in-flight mission. The origin station must confirm parcel intake before takeoff.</p>
      <Link className="dm-link" to={canSchedule ? '/dispatcher/scheduling' : '/manager/analytics'}>{canSchedule ? 'Review approved deliveries' : 'Open dashboard and reports'}</Link>
    </section>}
  </>
}

function positionMessage(order: TrackingSnapshot) {
  switch (order.status) {
    case 'PENDING': return 'Awaiting approval. Scheduling and drone assignment follow approval.'
    case 'APPROVED': return 'Approved. The delivery still needs to complete preparation for takeoff.'
    case 'READY_FOR_TAKEOFF': return 'Ready for takeoff. The delivery has not departed yet.'
    case 'IN_TRANSIT': return 'In transit. Waiting for a valid telemetry sample.'
    case 'ARRIVED': return 'Arrived at the destination station. Pickup confirmation is pending.'
    case 'DELIVERED': return 'Delivered. Pickup has been confirmed.'
    default: return 'Live telemetry is unavailable for this delivery state.'
  }
}

function SelectedMap({ id }: { id: string }) {
  const [connected, setConnected] = useState(false)
  const load = useCallback((signal: AbortSignal) => trackingApi.snapshot(id, signal), [id])
  const resource = useTrackingPoll(load, connected ? 30000 : 5000)
  const enabled = !!resource.data && !resource.blocked
  const live = useTrackingLive(id, resource.refresh, enabled, setConnected)
  const [now, setNow] = useState(Date.now)
  useEffect(() => { const timer = setInterval(() => setNow(Date.now()), 1000); return () => clearInterval(timer) }, [])
  const order = resource.data
  const candidate = order?.status === 'IN_TRANSIT' ? order.lastPosition : null
  const validSample = candidate && Number.isFinite(candidate.latitude) && Math.abs(candidate.latitude) <= 90 && Number.isFinite(candidate.longitude) && Math.abs(candidate.longitude) <= 180 && Number.isFinite(Date.parse(candidate.timestamp))
  const position = validSample ? candidate : null
  const stale = !!position && (order?.telemetryStale || now - Date.parse(position.timestamp) > 60000)
  const interrupted = !!resource.error || (resource.receivedAt !== undefined && now - resource.receivedAt > (connected ? 45000 : 15000))
  const time = (value: string | null) => value && Number.isFinite(Date.parse(value)) ? new Date(value).toLocaleString() : 'Not available'
  return <>
    <div className="dm-update-row"><Tag color={live === 'live' ? 'green' : 'default'}>{live === 'live' ? 'Live updates connected' : live === 'connecting' ? 'Connecting live updates' : live === 'reconnecting' ? 'Reconnecting' : live === 'paused' ? 'Updates paused' : 'Using periodic updates'}</Tag><Button onClick={resource.reload} loading={resource.refreshing}>Refresh map data</Button></div>
    <ResourceState loading={resource.loading} error={resource.error} retry={resource.reload}/>
    {interrupted && order && <Alert type="warning" showIcon title="Updates interrupted" description="The map retains the last received snapshot. Its position and delivery status may have changed."/>}
    {stale && <Alert type="warning" showIcon title="Position is out of date" description="This is the last reported position, not a current location. A telemetry gap does not mean the delivery failed."/>}
    {order && <>
      <div className="dm-summary"><div><span className="dm-eyebrow">SELECTED DELIVERY</span><h2>{order.trackingNumber}</h2></div><Tag>{orderStatusLabel(order.status)}</Tag><div className="dm-eta"><span>Estimated arrival</span><strong>{time(order.estimatedDeliveryTime)}</strong><small>An estimate, not actual arrival.</small></div></div>
      {!position && <Alert type="info" showIcon title={positionMessage(order)} />}
      <DeliveryMap key={`${id}:${order.originStation.id}:${order.destinationStation.id}`} origin={order.originStation} destination={order.destinationStation} position={position} stale={!!stale || interrupted}/>
      <div className="dm-detail-grid"><section className="dm-card"><h3>Stations</h3><dl><div><dt>From</dt><dd>{order.originStation.name}</dd></div><div><dt>To</dt><dd>{order.destinationStation.name}</dd></div></dl></section><section className="dm-card"><h3>Simulated telemetry</h3>{position ? <dl><div><dt>{stale || interrupted ? 'Last-known coordinates' : 'Latest reported coordinates'}</dt><dd>{position.latitude.toFixed(6)}, {position.longitude.toFixed(6)}</dd></div><div><dt>Reported at</dt><dd>{time(position.timestamp)}</dd></div></dl> : <p>No position to display.</p>}</section></div>
    </>}
  </>
}
