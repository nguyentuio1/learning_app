import { useCallback, useEffect, useRef, useState } from 'react'
import { Alert, Button, Tag, Timeline } from 'antd'
import axios from 'axios'
import { Link, useLocation, useNavigate, useParams } from 'react-router-dom'
import { OrderUiProvider } from '../../components/orders/OrderUiProvider'
import { useAuth } from '../../hooks/useAuth'
import { customerOrderApi } from '../../services/customerOrderApi'
import type { CustomerOrderDetail } from '../../types/customerOrder'
import { orderStatusLabel, orderTime } from '../../utils/orderDisplay'
import { orderIdPattern, type InsightRouteState } from '../operations/deliveryInsight'
import { InsightOrderPicker } from '../operations/InsightOrderPicker'
import { OperationsPage, ResourceState } from '../operations/OperationsUi'
import { useResource } from '../operations/useResource'
import { trackingApi } from '../tracking/trackingApi'
import { generateDeliverySummary, type DeliverySummary } from './summaryApi'

export function DeliverySummaryPage() {
  const { id } = useParams()
  const { user } = useAuth()
  const navigate = useNavigate()
  const { search, state } = useLocation()
  const context = state as InsightRouteState | null
  const customer = user?.role.code === 'CUSTOMER'
  return (
    <OrderUiProvider>
      <OperationsPage
        title="Delivery summary"
        subtitle="Read a summary alongside the recorded delivery information."
        action={id && (context?.reportSearch !== undefined && !customer ? (
          <Link className="insight-link" to={{ pathname: '/manager/analytics', search: context.reportSearch }}>Back to delivery report</Link>
        ) : <Link className="insight-link" to={{ pathname: '/delivery-summary', search }}>Choose another delivery</Link>)}
      >
        {!id ? (
          <InsightOrderPicker key={user?.id} customer={customer} onSelect={(value, trackingNumber) => navigate(
            { pathname: '/delivery-summary/' + value, search },
            { state: { trackingNumber } satisfies InsightRouteState },
          )} />
        ) : orderIdPattern.test(id) ? (
          <SummaryDetail key={user?.id + ':' + id} id={id} customer={customer} />
        ) : <Alert type="error" showIcon title="Invalid order ID" />}
      </OperationsPage>
    </OrderUiProvider>
  )
}

type SourceState = { trackingNumber: string; status: CustomerOrderDetail['status']; origin: string; destination: string; history?: CustomerOrderDetail['statusLogs'] }
type Notice = { type: 'info' | 'warning' | 'error'; title: string }

function SummaryDetail({ id, customer }: { id: string; customer: boolean }) {
  const load = useCallback(async (signal: AbortSignal): Promise<SourceState> => {
    if (customer) {
      const order = await customerOrderApi.detail(id, signal)
      return { trackingNumber: order.trackingNumber, status: order.status, origin: order.originStationName, destination: order.destinationStationName, history: order.statusLogs ?? [] }
    }
    const order = await trackingApi.snapshot(id, signal)
    return { trackingNumber: order.trackingNumber, status: order.status, origin: order.originStation.name, destination: order.destinationStation.name }
  }, [customer, id])

  const source = useResource(load)
  const [summary, setSummary] = useState<DeliverySummary>()
  const [notice, setNotice] = useState<Notice>()
  const [pending, setPending] = useState(false)
  const [blocked, setBlocked] = useState(false)
  const [cooldown, setCooldown] = useState(false)
  const requestRef = useRef<AbortController | undefined>(undefined)
  const retryTimer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined)

  useEffect(() => () => {
    requestRef.current?.abort()
    clearTimeout(retryTimer.current)
  }, [])

  async function generate() {
    if (requestRef.current || blocked || cooldown || !source.data) return
    setSummary(undefined)
    setNotice(undefined)
    setPending(true)
    const request = new AbortController()
    requestRef.current = request
    try {
      const result = await generateDeliverySummary(id, request.signal)
      if (request.signal.aborted) return
      source.reload()
      setSummary(result)
    } catch (error) {
      if (request.signal.aborted) return
      const response = axios.isAxiosError(error) ? error.response : undefined
      switch (response?.status) {
        case 401:
        case 403:
        case 404:
          setBlocked(true)
          setNotice({ type: 'error', title: 'This delivery summary is unavailable to your account.' })
          break
        case 409:
          source.reload()
          setNotice({ type: 'warning', title: 'The delivery changed while the summary was being generated. Review the refreshed record before trying again.' })
          break
        case 429: {
          const header = Number(response.headers['retry-after'])
          const delay = Number.isFinite(header) && header > 0 ? header : 5
          setCooldown(true)
          retryTimer.current = setTimeout(() => setCooldown(false), Math.min(delay * 1000, 2_147_483_647))
          setNotice({ type: 'warning', title: 'A summary request is already in progress. Please wait before trying again.' })
          break
        }
        case 503:
          setNotice({
            type: 'warning',
            title: response.data?.detail === 'Delivery summarization is not configured.'
              ? 'AI summaries are disabled in this environment. You can still review the delivery record.'
              : 'The summary service is temporarily unavailable.',
          })
          break
        case 504:
          setNotice({ type: 'warning', title: 'Summary generation took too long. You can try again.' })
          break
        default:
          setNotice({ type: 'error', title: 'Could not generate a summary. Please try again.' })
      }
    } finally {
      if (requestRef.current === request) requestRef.current = undefined
      if (!request.signal.aborted) setPending(false)
    }
  }

  return (
    <>
      {notice && <Alert type={notice.type} showIcon title={notice.title} />}
      {!blocked && <ResourceState
        loading={source.loading}
        error={source.error}
        retry={() => {
          setSummary(undefined)
          source.reload()
        }}
      />}
      {!blocked && source.data && (
        <div className="insight-grid">
          <section className="operations-panel insight-panel">
            <h2>{source.data.trackingNumber}</h2>
            <Button type="primary" loading={pending} disabled={cooldown} onClick={() => void generate()}>
              {summary?.outcome === 'GENERATED' ? 'Generate again' : 'Generate summary'}
            </Button>
            <div aria-live="polite">
              {summary?.outcome === 'GENERATED' ? (
                <>
                  <p><Tag color="gold">AI-generated summary</Tag></p>
                  <p className="insight-summary-text">{summary.text}</p>
                  <p className="insight-timestamp">Generated {orderTime(summary.generatedAt)}</p>
                </>
              ) : summary?.outcome === 'NO_HISTORY' ? <p>There is no recorded history to summarize.</p> : null}
            </div>
          </section>
          <section className="operations-panel insight-panel">
            <h2>Delivery record</h2>
            <dl className="operations-facts">
              <div><dt>Status</dt><dd>{orderStatusLabel(source.data.status)}</dd></div>
              <div><dt>Origin</dt><dd>{source.data.origin}</dd></div>
              <div><dt>Destination</dt><dd>{source.data.destination}</dd></div>
            </dl>
            {source.data.history?.length ? (
              <Timeline items={source.data.history.map(log => ({
                content: <>{orderStatusLabel(log.toStatus)} <span className="insight-timestamp">{orderTime(log.createdAt)}</span></>,
              }))} />
            ) : customer ? <p>No status history has been recorded yet.</p> : null}
            <p><Link className="insight-link" to={customer ? '/orders/' + id : '/delivery-tracking/' + id}>
              {customer ? 'Open order details' : 'View delivery tracking'}
            </Link></p>
          </section>
        </div>
      )}
    </>
  )
}
