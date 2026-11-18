import { useCallback, useEffect, useState } from 'react'
import { Alert, Button, Tag } from 'antd'
import axios from 'axios'
import { Link, useLocation, useNavigate, useParams } from 'react-router-dom'
import { OrderUiProvider } from '../../components/orders/OrderUiProvider'
import { useAuth } from '../../hooks/useAuth'
import { orderStatusLabel, orderTime } from '../../utils/orderDisplay'
import { orderIdPattern, type InsightRouteState } from '../operations/deliveryInsight'
import { InsightOrderPicker } from '../operations/InsightOrderPicker'
import { OperationsPage, ResourceState } from '../operations/OperationsUi'
import { useTrackingLive } from '../tracking/useTrackingLive'
import { useTrackingPoll } from '../tracking/useTrackingPoll'
import { getDeliveryEta } from './etaApi'

const arrivalClock = new Intl.DateTimeFormat(undefined, { timeStyle: 'short' })
const arrivalDate = new Intl.DateTimeFormat(undefined, { dateStyle: 'medium' })

const stoppedStatuses = ['ARRIVED', 'DELIVERED', 'FAILED', 'CANCELLED']

export function DeliveryEtaPage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const { search, state } = useLocation()
  const context = state as InsightRouteState | null
  const { user } = useAuth()
  const customer = user?.role.code === 'CUSTOMER'
  return (
    <OrderUiProvider>
      <OperationsPage
        title="Arrival estimate"
        subtitle="See the expected arrival time and available flight estimate details."
        action={id && (context?.reportSearch !== undefined && !customer ? (
          <Link className="insight-link" to={{ pathname: '/manager/analytics', search: context.reportSearch }}>Back to delivery report</Link>
        ) : <Link className="insight-link" to={{ pathname: '/delivery-eta', search }}>Choose another delivery</Link>)}
      >
        {!id ? (
          <InsightOrderPicker key={user?.id} customer={customer} onSelect={(value, trackingNumber) => navigate(
            { pathname: '/delivery-eta/' + value, search },
            { state: { trackingNumber } satisfies InsightRouteState },
          )} />
        ) : orderIdPattern.test(id) ? (
          <EtaDetail key={user?.id + ':' + id} id={id} customer={customer} trackingNumber={customer ? undefined : context?.trackingNumber} />
        ) : <Alert type="error" showIcon title="Invalid order ID" />}
      </OperationsPage>
    </OrderUiProvider>
  )
}

function EtaDetail({ id, customer, trackingNumber }: { id: string; customer: boolean; trackingNumber?: string }) {
  const load = useCallback((signal: AbortSignal) => getDeliveryEta(id, signal), [id])
  const resource = useTrackingPoll(load, 30_000)
  const live = useTrackingLive(id, resource.refresh, !!resource.data && !resource.blocked, 'status')
  const [now, setNow] = useState(Date.now)
  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), 1000)
    return () => window.clearInterval(timer)
  }, [])

  const result = resource.data
  const failed = resource.error != null
  const unavailableRoute = axios.isAxiosError(resource.error) && resource.error.response?.status === 404
  const arrivalTime = result?.estimatedDeliveryTime
  const arrivalMs = arrivalTime ? Date.parse(arrivalTime) : NaN
  const ended = result ? stoppedStatuses.includes(result.status) : false
  const remaining = Math.ceil((arrivalMs - now) / 60_000)
  const validFuture = Number.isFinite(arrivalMs) && remaining > 0
  const prediction = result?.aiEtaPrediction

  return (
    <>
      <div className="operations-actions">
        <Button loading={resource.refreshing} onClick={resource.reload}>Refresh estimate</Button>
        {result && <Tag>{live === 'live' ? 'Status updates connected' : 'Checking periodically'}</Tag>}
      </div>
      {unavailableRoute ? (
        <Alert type="info" showIcon title="Estimate unavailable for this delivery"
          description="The delivery or its estimate endpoint is unavailable. You can retry or choose another delivery." />
      ) : <ResourceState loading={resource.loading} error={resource.error} retry={resource.reload} />}
      {result && failed && <Alert type="warning" showIcon title="Showing the last saved estimate"
        description="The latest request failed. The information below may have changed." />}
      {result && (
        <div className="insight-grid">
          <section className="operations-panel insight-panel insight-arrival">
            {trackingNumber && <p className="insight-timestamp">Tracking number {trackingNumber}</p>}
            <Tag>{orderStatusLabel(result.status)}</Tag>
            <h2>{ended ? 'Arrival estimate ended' : failed ? 'Last known estimated arrival' : 'Estimated arrival'}</h2>
            {ended ? (
              <p>{result.status === 'ARRIVED' ? 'The delivery has arrived and is awaiting confirmation.' :
                result.status === 'DELIVERED' ? 'Delivery has been confirmed.' : 'An arrival countdown is no longer available.'}</p>
            ) : validFuture ? (
              <>
                <time className="insight-time" dateTime={arrivalTime ?? undefined}>{arrivalClock.format(arrivalMs)}</time>
                <p className="insight-timestamp">{arrivalDate.format(arrivalMs)} · Your local time</p>
                {!failed && <p>{remaining} {remaining === 1 ? 'minute' : 'minutes'} remaining</p>}
              </>
            ) : <p>{Number.isFinite(arrivalMs) ? 'The expected arrival time has passed.' : 'An estimated arrival time is not available yet.'}</p>}
            {!ended && <p className="insight-timestamp">{prediction ? 'An estimated time can change.' : 'No AI prediction is available. Any arrival time shown comes from the order schedule.'}</p>}
            {result.actualDeliveryTime && (
              <dl className="operations-facts"><div><dt>Recorded delivery time</dt><dd>{orderTime(result.actualDeliveryTime)}</dd></div></dl>
            )}
          </section>
          <section className="operations-panel insight-panel">
            <h2>Prediction details</h2>
            {prediction ? (
              <dl className="operations-facts">
                <div><dt>Full-route distance</dt><dd>{prediction.calculatedDistanceKm.toLocaleString()} km</dd></div>
                <div><dt>Full-flight duration</dt><dd>{prediction.predictedDurationMinutes} min</dd></div>
                <div><dt>Calculated at</dt><dd>{orderTime(prediction.createdAt)}</dd></div>
                {prediction.weatherCondition !== null && <div><dt>Weather</dt><dd>{prediction.weatherCondition}</dd></div>}
                {prediction.confidenceScore !== null && <div><dt>Confidence</dt><dd>{new Intl.NumberFormat(undefined, { style: 'percent', maximumFractionDigits: 1 }).format(prediction.confidenceScore)}</dd></div>}
              </dl>
            ) : <p>No AI prediction has been recorded.</p>}
            <p><Link className="insight-link" to={customer ? '/orders/' + id : '/delivery-tracking/' + id}>{customer ? 'Open order details' : 'View delivery tracking'}</Link></p>
          </section>
        </div>
      )}
    </>
  )
}
