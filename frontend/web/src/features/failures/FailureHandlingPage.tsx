import { useCallback, useRef, useState } from 'react'
import axios from 'axios'
import { Alert, Button, Checkbox, Form, Input, Modal, Select, Timeline } from 'antd'
import { Navigate, useLocation, useNavigate, useParams } from 'react-router-dom'
import { OperationsPage, ResourceState, StatusTag } from '../operations/OperationsUi'
import { useResource } from '../operations/useResource'
import { operationError, uncertainMutation, validationFields } from '../operations/feedback'
import { orderTime } from '../../utils/orderDisplay'
import { failureApi, type FailureFilters, type FailureOrder, type Resolution } from './failureApi'
import { FailedOrders } from './FailedOrders'
import { failureTypeOptions } from './failureOptions'
import { IncidentReport } from './IncidentReport'
import './failures.css'

const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i
const resolutionOptions = [{ value: 'RESCHEDULE', label: 'Prepare for redelivery' }, { value: 'RETURN_TO_ORIGIN', label: 'Request return to origin' }, { value: 'CANCEL', label: 'Cancel delivery' }]
const typeLabel = (value: string) => failureTypeOptions.find(type => type.value === value)?.label || value
interface Values { action: Resolution; note: string; packageVerified?: boolean }
export function FailureHandlingPage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const location = useLocation()
  const [reporting, setReporting] = useState(false)
  const [filters, setFilters] = useState<FailureFilters>({ page: 1, pageSize: 20, resolutionStatus: 'UNRESOLVED' })
  const load = useCallback((signal: AbortSignal) => failureApi.queue(filters, signal), [filters])
  const queue = useResource(load)
  return <div className="failure-feature"><OperationsPage title="Failed deliveries" subtitle="Review delivery incidents and their history, then choose the next step."
    action={id ? <Button onClick={() => navigate('/dispatcher/failures')}>Back to failed deliveries</Button> : <div className="operations-actions"><Button onClick={queue.reload} disabled={queue.loading}>Refresh deliveries</Button><Button type="primary" onClick={() => setReporting(true)}>Report an incident</Button></div>}>
    {!id ? <>
      {location.state?.failureMissing && <Alert type="warning" showIcon title="The selected failure record could not be found. Choose another delivery." />}
      {reporting && <IncidentReport onQueueRefresh={queue.reload} onClose={() => setReporting(false)} onReported={orderId => { setReporting(false); queue.reload(); navigate('/dispatcher/failures/' + orderId) }} />}
      <FailedOrders {...queue} filters={filters} onFilter={setFilters} />
    </> : uuid.test(id) ? <HandleOrder key={id} id={id} onChanged={queue.reload} /> : <Alert type="error" title="Invalid order ID." />}
  </OperationsPage></div>
}
function HandleOrder({ id, onChanged }: { id: string; onChanged: () => void }) {
  const load = useCallback((signal: AbortSignal) => failureApi.order(id, signal), [id])
  const resource = useResource(load)
  const [form] = Form.useForm<Values>()
  const action = Form.useWatch('action', form)
  const [pending, setPending] = useState<Values>()
  const [saving, setSaving] = useState(false)
  const busy = useRef(false)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [uncertain, setUncertain] = useState(false)
  const order = resource.data
  const canResolve = order?.status === 'FAILED' && order.latestFailure.resolutionStatus === 'UNRESOLVED' && order.returnToOriginStatus === 'NOT_REQUESTED'
  const parcelInDestinationSlot = !!order?.parcelLocation?.slotId && order.parcelLocation.stationId === order.destinationStation.id
  function refresh() { resource.reload(); onChanged() }
  async function save() {
    if (!pending || busy.current || uncertain || !canResolve || (pending.action === 'RESCHEDULE' && parcelInDestinationSlot)) return
    busy.current = true; setSaving(true); setError('')
    try {
      const result = await failureApi.resolve(id, pending.action, pending.note, pending.packageVerified === true)
      setNotice(pending.action === 'RETURN_TO_ORIGIN' ? 'Return requested. Receipt at the origin station still needs confirmation.'
        : pending.action === 'RESCHEDULE' ? 'Delivery approved for redelivery. A new departure and drone assignment are required.'
        : `${result.trackingNumber}: ${result.status.replaceAll('_', ' ')}.`)
      form.resetFields(); refresh()
    } catch (cause) {
      setError(axios.isAxiosError(cause) && typeof cause.response?.data?.detail === 'string' ? cause.response.data.detail : operationError(cause))
      setUncertain(uncertainMutation(cause))
      form.setFields(validationFields(cause, ['action', 'note', 'packageVerified'] as const))
      if (uncertainMutation(cause) || (axios.isAxiosError(cause) && [404, 409].includes(cause.response?.status || 0))) refresh()
    } finally { busy.current = false; setSaving(false); setPending(undefined) }
  }
  if (axios.isAxiosError(resource.error) && resource.error.response?.status === 404) {
    return <Navigate to="/dispatcher/failures" replace state={{ failureMissing: true }} />
  }
  return <>
    {notice && <Alert type="success" showIcon title={notice} />}
    {error && <Alert type="error" showIcon title={error} />}
    {uncertain && <Alert type="warning" showIcon title="The result could not be confirmed. Review the refreshed delivery and history before another change."
      action={<Button disabled={!order || saving} onClick={() => setUncertain(false)}>I have reviewed the current delivery</Button>} />}
    <ResourceState loading={resource.loading} error={resource.error} retry={refresh} />
    {order && <>
      <div className="operations-grid"><section className="operations-panel"><h2>{order.trackingNumber}</h2><StatusTag status={order.status} /> <StatusTag status={order.latestFailure.resolutionStatus} />
        <dl className="operations-facts"><div><dt>Route</dt><dd>{order.originStation.name} → {order.destinationStation.name}</dd></div>
          <div><dt>Package</dt><dd>{order.package.packageName} · {order.package.weightGram} g · {order.package.lengthCm} × {order.package.widthCm} × {order.package.heightCm} cm{order.package.isFragile ? ' · Fragile' : ''}</dd></div>
          <div><dt>Latest failure</dt><dd>{typeLabel(order.latestFailure.failureType)} · {orderTime(order.latestFailure.failedAt)}</dd></div>
          <div><dt>Reason</dt><dd>{order.latestFailure.failureReason || 'No reason recorded'}</dd></div>
          <div><dt>Return to origin</dt><dd>{order.returnToOriginStatus.replaceAll('_', ' ')}</dd></div>
          <div><dt>Parcel location</dt><dd>{order.parcelLocation ? <>{order.parcelLocation.stationId === order.originStation.id ? order.originStation.name : order.parcelLocation.stationId === order.destinationStation.id ? order.destinationStation.name : order.parcelLocation.stationId}<small className="operations-small">{order.parcelLocation.slotId ? `Slot: ${order.parcelLocation.slotId}` : 'Held outside a slot'}</small></> : 'Location has not been verified'}</dd></div>
          <div><dt>Active mission</dt><dd>{order.currentMission ? `${order.currentMission.drone.serialNumber} · ${order.currentMission.flightStatus}` : 'No active mission'}</dd></div>
        </dl><div className="operations-actions"><Button onClick={refresh} disabled={saving}>Refresh delivery</Button></div>
      </section><section className="operations-panel"><h2>Resolve this delivery</h2>
        {!canResolve ? <Alert type="info" showIcon title={order.returnToOriginStatus === 'REQUESTED' ? 'Return requested — awaiting confirmation of receipt at the origin station.' : 'This failure is no longer awaiting a resolution.'} />
          : <Form form={form} layout="vertical" disabled={saving || uncertain} onFinish={setPending}>
            <Form.Item name="action" label="Resolution" rules={[{ required: true, message: 'Choose a resolution.' }]}><Select options={resolutionOptions.map(option => ({ ...option, disabled: option.value === 'RESCHEDULE' && parcelInDestinationSlot }))} /></Form.Item>
            {parcelInDestinationSlot && <Alert type="warning" showIcon title="The parcel is still in a destination slot. The station operator must clear it before redelivery." />}
            {action === 'RESCHEDULE' && <>
              <p>Confirm the parcel has been physically checked. A new schedule and drone assignment will be needed.</p>
              <Form.Item name="packageVerified" valuePropName="checked" rules={[{ validator: (_, value) => value === true ? Promise.resolve() : Promise.reject(new Error('Confirm that the package has been verified.')) }]}><Checkbox>Package has been physically verified</Checkbox></Form.Item>
            </>}
            {action === 'RETURN_TO_ORIGIN' && <p>The delivery stays failed until receipt at the origin is confirmed. Emptying a destination slot does not confirm the return.</p>}
            {action === 'CANCEL' && <p>The delivery will be cancelled and its resources released.</p>}
            <Form.Item name="note" label="Resolution note" rules={[{ required: true, whitespace: true, max: 500, message: 'Enter a note of 1 to 500 characters.' }]}><Input.TextArea rows={4} maxLength={500} showCount /></Form.Item>
            <Button type="primary" htmlType="submit" disabled={uncertain || (action === 'RESCHEDULE' && parcelInDestinationSlot)} loading={saving}>Review resolution</Button>
          </Form>}
      </section></div>
      <FailureHistory order={order} />
    </>}
    <Modal title="Apply this resolution?" open={!!pending} confirmLoading={saving} onOk={() => void save()} onCancel={() => { if (!saving) setPending(undefined) }}
      okButtonProps={{ disabled: uncertain || !canResolve || (pending?.action === 'RESCHEDULE' && parcelInDestinationSlot) }} cancelButtonProps={{ disabled: saving }} closable={!saving} keyboard={!saving} mask={{ closable: !saving }}>
      <p><strong>{order?.trackingNumber}</strong></p><p>{pending?.action.replaceAll('_', ' ')}</p><p>{pending?.note}</p><p>The customer will be notified of this decision.</p>
    </Modal>
  </>
}
function FailureHistory({ order }: { order: FailureOrder }) {
  return <>
    <div className="operations-grid"><section className="operations-panel"><h2>Failure history</h2>
      {order.failureHistory.length ? <Timeline items={order.failureHistory.map(episode => ({ content: <>
        <strong>{typeLabel(episode.failureType)}</strong> · {orderTime(episode.failedAt)}<p>{episode.failureReason || 'No reason recorded'}</p>
        <StatusTag status={episode.resolutionStatus} /><small className="operations-small">Episode {episode.failureLogId}{episode.resolvedAt ? ` · Resolved ${orderTime(episode.resolvedAt)}` : ''}</small>
      </> }))} /> : <p>No failure episodes recorded.</p>}
    </section><section className="operations-panel"><h2>Resolution history</h2>
      {!order.resolutionHistoryComplete && <Alert type="warning" showIcon title="Some older handling records are unavailable. This history may be incomplete." />}
      {order.resolutionHistory.length ? <Timeline items={order.resolutionHistory.map(event => ({ content: <>
        <strong>{event.action.replaceAll('_', ' ')}</strong> · {orderTime(event.createdAt)}<p>{event.note || 'No note recorded'}</p>
        <small className="operations-small">Episode {event.failureLogId} · {event.performedBy ? `Recorded by ${event.performedBy}` : 'Actor not recorded'}</small>
      </> }))} /> : <p>No handling events recorded.</p>}
    </section></div>
    <section className="operations-panel"><h2>Status history</h2>{order.statusLogs.length ? <Timeline items={order.statusLogs.map(log => ({ content: <><strong>{log.toStatus.replaceAll('_', ' ')}</strong> · {orderTime(log.createdAt)}{log.remarks && <p>{log.remarks}</p>}</> }))} /> : <p>No status changes recorded.</p>}</section>
  </>
}
