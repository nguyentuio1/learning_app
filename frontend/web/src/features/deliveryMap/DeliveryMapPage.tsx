import { useCallback, useEffect, useState } from 'react'
import { Alert, Button, Select, Tag } from 'antd'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { useAuth } from '../../hooks/useAuth'
import { OperationsPage, ResourceState } from '../operations/OperationsUi'
import { trackingApi } from './trackingApi'
import { useTrackingPoll } from './useTrackingPoll'
import { useTrackingLive } from './useTrackingLive'
import { DeliveryMap } from './DeliveryMap'
import './delivery-map.css'

const uuid = /^[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}$/i
export function DeliveryMapPage() {
  const { id } = useParams()
  const { user } = useAuth()
  const navigate = useNavigate()
  const list = useTrackingPoll(trackingApi.active)
  return <div className="delivery-map-feature"><OperationsPage title="Delivery map" subtitle="See the stations and the latest position reported for a delivery."
    action={id && <Link className="dm-link" to="/delivery-map">← Choose another delivery</Link>}>
    <div className="dm-selection"><label>Active delivery<Select aria-label="Active delivery" showSearch optionFilterProp="label" placeholder="Choose a delivery" value={id} loading={list.loading}
      onChange={value => navigate('/delivery-map/' + value)} options={list.data?.map(order => ({ value: order.orderId, label: `${order.trackingNumber} · ${order.droneSerialNumber}` }))}/></label><Button onClick={list.reload} loading={list.refreshing}>Refresh deliveries</Button></div>
    {!!list.error && <ResourceState loading={false} error={list.error} retry={list.reload}/>}
    {!id ? <><ResourceState loading={list.loading} retry={list.reload}/>{!list.loading && !list.error && <section className="dm-card dm-empty"><p>{list.data?.length ? 'Choose a delivery to open its map.' : 'There are no active deliveries right now.'}</p></section>}</>
      : uuid.test(id) ? <SelectedMap key={`${user?.id}:${id}`} id={id}/> : <Alert type="error" showIcon title="Invalid order ID" description="Choose a delivery from the list."/>}
  </OperationsPage></div>
}
function SelectedMap({ id }: { id: string }) {
  const [connected, setConnected] = useState(false)
  const load = useCallback((signal: AbortSignal) => trackingApi.snapshot(id, signal), [id])
  const resource = useTrackingPoll(load, connected ? 30000 : 5000)
  const enabled = !resource.loading && !resource.blocked
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
      <div className="dm-summary"><div><span className="dm-eyebrow">SELECTED DELIVERY</span><h2>{order.trackingNumber}</h2></div><Tag>{order.status.replaceAll('_', ' ')}</Tag><div className="dm-eta"><span>Estimated arrival</span><strong>{time(order.estimatedDeliveryTime)}</strong><small>An estimate, not actual arrival.</small></div></div>
      <DeliveryMap key={`${id}:${order.originStation.id}:${order.destinationStation.id}`} origin={order.originStation} destination={order.destinationStation} position={position} stale={!!stale || interrupted}/>
      <div className="dm-detail-grid"><section className="dm-card"><h3>Stations</h3><dl><div><dt>From</dt><dd>{order.originStation.name}</dd></div><div><dt>To</dt><dd>{order.destinationStation.name}</dd></div></dl></section><section className="dm-card"><h3>Reported position</h3>{position ? <dl><div><dt>Coordinates</dt><dd>{position.latitude.toFixed(6)}, {position.longitude.toFixed(6)}</dd></div><div><dt>Reported at</dt><dd>{time(position.timestamp)}</dd></div></dl> : <p>{order.status === 'IN_TRANSIT' ? 'No valid position has been reported yet.' : 'No live position: this delivery is not in transit.'}</p>}</section></div>
    </>}
  </>
}
