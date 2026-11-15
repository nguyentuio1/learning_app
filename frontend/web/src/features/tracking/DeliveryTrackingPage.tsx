import { useCallback, useEffect, useState } from 'react'
import { Alert, Button, Input, Progress, Tag } from 'antd'
import { Link, useParams } from 'react-router-dom'
import { useAuth } from '../../hooks/useAuth'
import { OperationsPage, ResourceState } from '../operations/OperationsUi'
import { trackingApi, type TrackingSnapshot } from './trackingApi'
import { useTrackingPoll } from './useTrackingPoll'
import { useTrackingLive } from './useTrackingLive'
import './tracking.css'

const uuid = /^[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}$/i
const statusColors: Record<string, string> = { IN_TRANSIT: 'cyan', READY_FOR_TAKEOFF: 'gold', DELIVERED: 'green', ARRIVED: 'blue', FAILED: 'red', CANCELLED: 'default', APPROVED: 'gold' }
function Status({ value }: { value: string }) { return <Tag color={statusColors[value]}>{value.replaceAll('_', ' ')}</Tag> }
function date(value: string | null | undefined) {
  const parsed = value ? new Date(value) : undefined
  return parsed && Number.isFinite(parsed.getTime()) ? parsed.toLocaleString() : 'Not available'
}
function oldSample(timestamp: string | null | undefined, now: number) {
  return !timestamp || !Number.isFinite(Date.parse(timestamp)) || now - Date.parse(timestamp) > 60000
}
function validPosition(snapshot: TrackingSnapshot) {
  const p = snapshot.lastPosition
  return snapshot.status === 'IN_TRANSIT' && p && Number.isFinite(p.latitude) && Math.abs(p.latitude) <= 90
    && Number.isFinite(p.longitude) && Math.abs(p.longitude) <= 180 && Number.isFinite(Date.parse(p.timestamp)) ? p : null
}

export function DeliveryTrackingPage() {
  const { id } = useParams()
  const { user } = useAuth()
  // Re-mount reads when the signed-in account changes, clearing protected data.
  return <div className="tracking-feature"><OperationsPage title="Delivery tracking" subtitle="Follow active deliveries and their latest reported flight data."
    action={id && <Link className="tracking-back" to="/delivery-tracking">← Active deliveries</Link>}>
    {id ? uuid.test(id) ? <TrackingDetail key={`${user?.id}:${id}`} id={id} /> : <Alert type="error" showIcon title="Invalid order ID" description="Return to active deliveries and choose an order." /> : <ActiveDeliveries key={user?.id} />}
  </OperationsPage></div>
}

function ActiveDeliveries() {
  const [search, setSearch] = useState('')
  const resource = useTrackingPoll(trackingApi.active)
  const [now, setNow] = useState(Date.now)
  useEffect(() => { const timer = setInterval(() => setNow(Date.now()), 1000); return () => clearInterval(timer) }, [])
  const visible = resource.data?.filter(order => `${order.trackingNumber} ${order.droneSerialNumber}`.toLowerCase().includes(search.toLowerCase().trim()))
  return <>
    <div className="tracking-toolbar"><div><span className="tracking-kicker">FLIGHT OPERATIONS</span><p>Updates every 5 seconds while this tab is visible.</p></div><Button onClick={resource.reload} loading={resource.refreshing}>Refresh deliveries</Button></div>
    <label className="tracking-search">Find a delivery<Input allowClear value={search} onChange={event => setSearch(event.target.value)} placeholder="Tracking number or drone serial number" /></label>
    <ResourceState loading={resource.loading} error={resource.error} retry={resource.reload} />
    {resource.error && resource.data && <p className="tracking-muted">Showing the last received list. Its availability may have changed.</p>}
    {visible?.length === 0 && <div className="tracking-card tracking-empty"><p>{search.trim() ? 'No active deliveries match your search.' : 'No active deliveries right now.'}</p>{search.trim() && <Button onClick={() => setSearch('')}>Clear search</Button>}</div>}
    <div className="tracking-list">{visible?.map(order => {
      const stale = order.telemetryStale || oldSample(order.lastTelemetryAt, now)
      return <article className="tracking-card tracking-flight" key={order.orderId}>
        <div className="tracking-card-heading"><div><span className="tracking-kicker">DELIVERY</span><h2>{order.trackingNumber}</h2></div><Status value={order.orderStatus}/></div>
        <dl className="tracking-facts"><div><dt>Drone</dt><dd>{order.droneSerialNumber}</dd></div><div><dt>Flight</dt><dd>{order.flightStatus.replaceAll('_', ' ')}</dd></div><div><dt>Scheduled departure</dt><dd>{date(order.scheduledDepartureTime)}</dd></div><div><dt>Estimated arrival</dt><dd>{date(order.estimatedDeliveryTime)}</dd></div></dl>
        <div className="tracking-flight-foot"><span className="tracking-muted">{!order.lastTelemetryAt ? 'No position reported yet' : order.flightStatus !== 'IN_AIR' ? 'Flight is not in the air' : stale || resource.error ? 'Latest position needs an update' : 'Position recently reported'}</span><Link to={'/delivery-tracking/' + order.orderId}>Track delivery <span aria-hidden="true">→</span></Link></div>
      </article>
    })}</div>
  </>
}

function TrackingDetail({ id }: { id: string }) {
  const load = useCallback((signal: AbortSignal) => trackingApi.snapshot(id, signal), [id])
  const resource = useTrackingPoll(load)
  const live = useTrackingLive(id, resource.refresh, !resource.loading && !resource.blocked)
  const [now, setNow] = useState(Date.now)
  useEffect(() => { const timer = setInterval(() => setNow(Date.now()), 1000); return () => clearInterval(timer) }, [])
  const order = resource.data
  const position = order ? validPosition(order) : null
  const stale = !!position && (order?.telemetryStale || oldSample(position.timestamp, now))
  const stopped = order && order.status !== 'IN_TRANSIT'
  const recentlyReceived = resource.receivedAt !== undefined && now - resource.receivedAt <= 15000
  const fresh = !!position && !stale && !resource.error && recentlyReceived
  return <>
    <div className="tracking-transport" role="status"><Tag color={live === 'live' ? 'green' : 'default'}>{live === 'live' ? 'Live updates connected' : live === 'reconnecting' ? 'Reconnecting live updates' : live === 'connecting' ? 'Connecting live updates' : live === 'paused' ? 'Updates paused' : 'Live updates unavailable'}</Tag><span>Snapshots also refresh every 5 seconds to check freshness.</span></div>
    <div className="tracking-toolbar"><div><span className="tracking-kicker">{fresh ? 'POSITION RECENTLY REPORTED' : 'DELIVERY UPDATES'}</span><p>{resource.receivedAt ? `Last checked: ${date(new Date(resource.receivedAt).toISOString())}` : 'Waiting for the first update'} · Times shown in your local timezone</p></div><Button loading={resource.refreshing} onClick={resource.reload}>Refresh tracking</Button></div>
    <ResourceState loading={resource.loading} error={resource.error} retry={resource.reload}/>
    {resource.error && order && <Alert type="warning" showIcon title="Updates interrupted" description="The details below are the last received snapshot. They do not confirm the current position or status. Automatic retries continue every 5 seconds." />}
    {!resource.error && !recentlyReceived && order && <Alert type="warning" showIcon title="Waiting for an update" description="The last received details may be out of date. Updates resume when this tab is active." />}
    {order && <>
      <section className="tracking-card tracking-journey"><div className="tracking-card-heading"><div><span className="tracking-kicker">YOUR SELECTED DELIVERY</span><h2>{order.trackingNumber}</h2></div><Status value={order.status}/></div>
        <div className="tracking-route"><div><span>FROM</span><strong>{order.originStation.name}</strong></div><span className="tracking-route-arrow" aria-hidden="true">→</span><div><span>TO</span><strong>{order.destinationStation.name}</strong></div></div>
        <div className="tracking-arrival"><span>Estimated arrival</span><strong>{date(order.estimatedDeliveryTime)}</strong><small>An estimate, not a guaranteed arrival time.</small></div>
      </section>
      {stale && <Alert type="warning" showIcon title="Position is out of date" description="The last reported position is older than 60 seconds or marked stale by the service. It is not a live position; a telemetry gap does not mean this delivery has failed." />}
      <div className="tracking-detail-grid"><section className="tracking-card"><div className="tracking-card-heading"><h2>{fresh ? 'Latest flight position' : 'Last reported position'}</h2><Tag color={fresh ? 'green' : 'default'}>{fresh ? 'Recent sample' : 'Not live'}</Tag></div>
        {position ? <><dl className="tracking-facts"><div><dt>Latitude</dt><dd>{position.latitude.toFixed(6)}°</dd></div><div><dt>Longitude</dt><dd>{position.longitude.toFixed(6)}°</dd></div><div><dt>Reported at</dt><dd>{date(position.timestamp)}</dd></div></dl><p className="tracking-muted">Coordinates are reported by the delivery service. No position is estimated between updates.</p></> : <div className="tracking-empty tracking-empty-inset"><p>{stopped ? 'No live position: this delivery is not in transit.' : 'No position has been reported yet.'}</p></div>}
      </section><section className="tracking-card"><h2>Flight readings</h2>{position ? <>
        <dl className="tracking-readings"><div><dt>Altitude</dt><dd>{Number.isFinite(position.altitudeMeters) && position.altitudeMeters >= 0 ? `${position.altitudeMeters} m` : 'Not available'}</dd></div><div><dt>Speed</dt><dd>{Number.isFinite(position.speedMps) && position.speedMps >= 0 ? `${position.speedMps} m/s` : 'Not available'}</dd></div></dl>
        <div className="tracking-battery"><span>Battery · reported sample</span>{Number.isFinite(position.batteryPercentage) && position.batteryPercentage >= 0 && position.batteryPercentage <= 100 ? <Progress percent={position.batteryPercentage} strokeColor="#4a704b"/> : <p>Not available</p>}</div>
      </> : <p className="tracking-muted">Flight readings appear when the service reports a position for an in-transit delivery.</p>}</section></div>
    </>}
  </>
}
