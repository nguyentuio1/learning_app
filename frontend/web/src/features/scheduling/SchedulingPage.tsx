import { useCallback, useRef, useState } from 'react'
import axios from 'axios'
import { Alert, Button, DatePicker, Form, Modal, Radio, Timeline } from 'antd'
import dayjs, { type Dayjs } from 'dayjs'
import { useNavigate, useParams } from 'react-router-dom'
import { OperationsPage, ResourceState, StatusTag } from '../operations/OperationsUi'
import { operationError, uncertainMutation, validationFields } from '../operations/feedback'
import { useResource } from '../operations/useResource'
import { orderTime } from '../../utils/orderDisplay'
import { schedulingApi, type DispatchOrder, type QueueFilters } from './schedulingApi'
import { ApprovedOrders } from './ApprovedOrders'
import './scheduling.css'

const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i
const reasons: Record<string, string> = { NOT_IDLE: 'Drone is not idle', PAYLOAD_EXCEEDED: 'Package exceeds payload capacity', RANGE_INSUFFICIENT: 'Insufficient range including the required reserve', TIME_OVERLAP: 'Another mission overlaps this departure', NOT_AT_ORIGIN: 'Drone is not at the origin station' }
function failureMessage(error: unknown) {
  return axios.isAxiosError(error) && typeof error.response?.data?.detail === 'string' ? error.response.data.detail : operationError(error)
}
export function SchedulingPage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const [filters, setFilters] = useState<QueueFilters>({ page: 1, pageSize: 20 })
  const load = useCallback((signal: AbortSignal) => schedulingApi.queue(filters, signal), [filters])
  const queue = useResource(load)
  return <div className="scheduling-feature"><OperationsPage title="Delivery scheduling" subtitle="Plan an approved delivery, then choose an eligible drone."
    action={id ? <Button onClick={() => navigate('/dispatcher/scheduling')}>Back to approved deliveries</Button> : <Button onClick={queue.reload} disabled={queue.loading}>Refresh deliveries</Button>}>
    {!id ? <ApprovedOrders {...queue} filters={filters} onFilter={setFilters} />
      : uuid.test(id) ? <ScheduleDelivery key={id} id={id} onChanged={queue.reload} /> : <Alert type="error" title="Invalid order ID." />}
  </OperationsPage></div>
}
function ScheduleDelivery({ id, onChanged }: { id: string; onChanged: () => void }) {
  const load = useCallback((signal: AbortSignal) => schedulingApi.order(id, signal), [id])
  const resource = useResource(load)
  const [notice, setNotice] = useState('')
  const [error, setError] = useState('')
  const [uncertain, setUncertain] = useState(false)
  const [saving, setSaving] = useState(false)
  const refresh = () => { resource.reload(); onChanged() }
  function changed(message: string) { setNotice(message); setError(''); setUncertain(false); refresh() }
  function failed(cause: unknown) {
    setError(failureMessage(cause))
    const unknown = uncertainMutation(cause)
    setUncertain(unknown)
    if (unknown || (axios.isAxiosError(cause) && [404, 409].includes(cause.response?.status || 0))) refresh()
  }
  const order = resource.data
  return <>
    {notice && <Alert type="success" showIcon title={notice} />}
    {error && <Alert type="error" showIcon title={error} />}
    {uncertain && <Alert type="warning" showIcon title="The save result could not be confirmed."
      description="The current delivery has been requested again. Review the saved departure and mission before allowing another change."
      action={<Button disabled={!order || saving} onClick={() => setUncertain(false)}>I have reviewed the current delivery</Button>} />}
    <ResourceState loading={resource.loading} error={resource.error} retry={refresh} />
    {order && <>
      <section className="operations-panel"><h2>{order.trackingNumber}</h2><StatusTag status={order.status} /> <StatusTag status={order.assignmentStatus} />
        <div className="scheduling-route"><strong>{order.originStation.name}</strong><span aria-hidden="true">→</span><strong>{order.destinationStation.name}</strong></div>
        <dl className="operations-facts"><div><dt>Package</dt><dd>{order.package.packageName} · {order.package.weightGram} g</dd></div>
          <div><dt>Dimensions</dt><dd>{order.package.lengthCm} × {order.package.widthCm} × {order.package.heightCm} cm{order.package.isFragile ? ' · Fragile' : ''}</dd></div>
          <div><dt>Departure</dt><dd>{order.scheduledDepartureTime ? orderTime(order.scheduledDepartureTime) : 'Not scheduled'}</dd></div>
          <div><dt>Estimated arrival</dt><dd>{order.estimatedDeliveryTime ? orderTime(order.estimatedDeliveryTime) : 'Not available'}</dd></div>
        </dl><div className="operations-actions"><Button disabled={saving} onClick={refresh}>Refresh delivery</Button></div>
      </section>
      {order.currentMission && <section className="operations-panel"><h2>Current flight mission</h2>
        <dl className="operations-facts"><div><dt>Drone</dt><dd>{order.currentMission.drone.serialNumber} · {order.currentMission.drone.modelName}</dd></div>
          <div><dt>Flight status</dt><dd><StatusTag status={order.currentMission.flightStatus} /></dd></div>
          <div><dt>Mission</dt><dd>{order.currentMission.id}</dd></div>
          <div><dt>Destination slot</dt><dd>{order.currentMission.destinationSlotId || 'Not recorded'}</dd></div></dl>
        <p className="operations-small">This delivery already has an active assignment. Changing the schedule or drone is unavailable.</p>
      </section>}
      {order.status !== 'APPROVED' ? <Alert type="info" showIcon title="This delivery is no longer approved. Scheduling and assignment are unavailable." />
        : order.assignmentStatus === 'ASSIGNED' || order.currentMission ? null : <div className="operations-grid">
          <ScheduleForm key={'schedule-' + order.updatedAt + (order.scheduledDepartureTime || '')} order={order} disabled={saving || uncertain} onBusy={setSaving} onSaved={changed} onError={failed} />
          {order.scheduledDepartureTime ? <CandidatePicker key={'candidates-' + order.updatedAt + order.scheduledDepartureTime} id={id} disabled={saving || uncertain} onBusy={setSaving} onSaved={changed} onError={failed} />
            : <section className="operations-panel"><h2>02 · Assign a drone</h2><p>Save a departure first to check suitable drones for this delivery.</p></section>}
        </div>}
      <section className="operations-panel"><h2>Status history</h2>{order.statusLogs.length ? <Timeline items={order.statusLogs.map(log => ({ content: <><strong>{log.toStatus.replaceAll('_', ' ')}</strong> · {orderTime(log.createdAt)}{log.remarks && <p>{log.remarks}</p>}</> }))} /> : <p>No status changes recorded.</p>}</section>
    </>}
  </>
}
interface MutationProps { disabled: boolean; onBusy: (value: boolean) => void; onSaved: (message: string) => void; onError: (error: unknown) => void }
function ScheduleForm({ order, disabled, onBusy, onSaved, onError }: MutationProps & { order: DispatchOrder }) {
  const [form] = Form.useForm<{ departure: Dayjs }>()
  const busy = useRef(false)
  const [saving, setSaving] = useState(false)
  async function save({ departure }: { departure: Dayjs }) {
    if (busy.current || disabled) return
    if (!departure?.isValid() || departure.valueOf() < Date.now() + 15 * 60 * 1000) {
      form.setFields([{ name: 'departure', errors: ['Choose a departure at least 15 minutes from now.'] }]); return
    }
    busy.current = true; setSaving(true); onBusy(true)
    try {
      const result = await schedulingApi.schedule(order.orderId, departure.toDate().toISOString())
      onSaved(`Departure saved. Estimated flight: ${result.estimatedFlightMinutes} minutes.`)
    } catch (cause) {
      form.setFields(validationFields(cause, ['scheduledDepartureTime']).map(field => ({ ...field, name: 'departure' })))
      onError(cause)
    } finally { busy.current = false; setSaving(false); onBusy(false) }
  }
  return <section className="operations-panel"><h2>01 · Schedule departure</h2><p>Choose a time at least 15 minutes from now. Times are shown in your local timezone.</p>
    <Form form={form} layout="vertical" onFinish={save} disabled={disabled} initialValues={{ departure: order.scheduledDepartureTime ? dayjs(order.scheduledDepartureTime) : undefined }}>
      <Form.Item name="departure" label="Scheduled departure" rules={[{ required: true, message: 'Choose a departure time.' }]}><DatePicker showTime format="YYYY-MM-DD HH:mm" /></Form.Item>
      <Button type="primary" htmlType="submit" disabled={disabled} loading={saving}>Save departure</Button>
    </Form>
  </section>
}
function CandidatePicker({ id, disabled, onBusy, onSaved, onError }: MutationProps & { id: string }) {
  const load = useCallback((signal: AbortSignal) => schedulingApi.candidates(id, signal), [id])
  const resource = useResource(load)
  const [selected, setSelected] = useState<string>()
  const [confirm, setConfirm] = useState(false)
  const [saving, setSaving] = useState(false)
  const busy = useRef(false)
  const candidate = resource.data?.find(item => item.droneId === selected && item.isEligible && item.status === 'IDLE')
  function refresh() { setSelected(undefined); setConfirm(false); resource.reload() }
  async function assign() {
    if (busy.current || !candidate || disabled) return
    busy.current = true; setSaving(true); onBusy(true)
    try {
      await schedulingApi.assign(id, candidate.droneId)
      onSaved('Drone assigned. The current mission and delivery queue have been refreshed.')
    } catch (cause) { setSelected(undefined); resource.reload(); onError(cause) }
    finally { busy.current = false; setSaving(false); onBusy(false); setConfirm(false) }
  }
  return <section className="operations-panel"><h2>02 · Assign a drone</h2><p>Availability is checked again when you confirm. The destination slot is selected automatically.</p>
    <Button onClick={refresh} disabled={saving || disabled} loading={resource.loading}>Refresh available drones</Button>
    <ResourceState loading={resource.loading} error={resource.error} retry={refresh} empty={resource.data?.length === 0} />
    {resource.data && <div className="scheduling-candidates"><Radio.Group value={selected} disabled={saving || disabled} onChange={event => setSelected(event.target.value)}>
      {resource.data.map(item => <div key={item.droneId} className="scheduling-candidate"><Radio value={item.droneId} disabled={!item.isEligible || item.status !== 'IDLE'}><strong>{item.serialNumber} · {item.modelName}</strong></Radio>
        <small className="operations-small">{item.maxPayloadGram} g payload · {item.maxFlightRangeKm} km range · {item.status}</small>
        {item.reasonsNotEligible.map(reason => <p key={reason} className="operations-small">{reasons[reason] || reason}</p>)}
      </div>)}
    </Radio.Group></div>}
    <div className="operations-actions"><Button type="primary" onClick={() => setConfirm(true)} disabled={!candidate || saving || disabled}>Assign selected drone</Button></div>
    <Modal title="Plan this flight mission?" open={confirm} onOk={() => void assign()} onCancel={() => { if (!saving) setConfirm(false) }} confirmLoading={saving}
      okButtonProps={{ disabled: !candidate || disabled }} cancelButtonProps={{ disabled: saving }} closable={!saving} keyboard={!saving} mask={{ closable: !saving }}>
      <p>Assign <strong>{candidate?.serialNumber}</strong> to this delivery?</p><p>This reserves an available destination slot. It does not launch the drone.</p>
    </Modal>
  </section>
}
