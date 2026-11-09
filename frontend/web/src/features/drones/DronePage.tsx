import { useCallback, useRef, useState } from 'react'
import { Alert, Button, Checkbox, Form, Input, InputNumber, Select } from 'antd'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { OperationsPage, ResourceState, StatusTag } from '../operations/OperationsUi'
import { operationError, uncertainMutation, validationFields } from '../operations/feedback'
import { useResource } from '../operations/useResource'
import { droneApi, type DroneDetails, type DroneValues } from './droneApi'

function DroneForm({ drone, onSaved }: { drone?: DroneDetails; onSaved: (id: string) => void }) {
  const [form] = Form.useForm<DroneValues>()
  const loadStations = useCallback((signal: AbortSignal) => drone ? Promise.resolve([]) : droneApi.stations(signal), [drone])
  const stations = useResource(loadStations)
  const pending = useRef(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [needsReview, setNeedsReview] = useState(false)
  async function save(values: DroneValues) {
    if (pending.current || needsReview || drone?.activeMissionId) return
    pending.current = true
    setBusy(true)
    setError('')
    try {
      const result = drone ? await droneApi.update(drone.id, values) : await droneApi.create(values)
      onSaved(result.id)
    } catch (cause) {
      form.setFields(validationFields(cause, ['serialNumber', 'modelName', 'maxPayloadGram', 'maxFlightRangeKm', 'batteryCapacityMah', 'currentStationId'] as const))
      setError(uncertainMutation(cause) ? 'The save could not be confirmed. Check the fleet record before saving again.' : operationError(cause))
      if (uncertainMutation(cause)) setNeedsReview(true)
    } finally { pending.current = false; setBusy(false) }
  }
  return <section className="operations-panel"><h2>Fleet record</h2>
    <ResourceState loading={stations.loading} error={stations.error} retry={stations.reload} />
    {error && <Alert type="error" title={error} showIcon />}
    {drone?.activeMissionId && <Alert type="info" title="This drone has an active mission. Editing is unavailable." />}
    <Form form={form} layout="vertical" onFinish={save} disabled={busy || needsReview || !!drone?.activeMissionId || stations.loading || !!stations.error} initialValues={drone}>
      {!drone && <Form.Item name="serialNumber" label="Serial number" rules={[{ required: true, whitespace: true, max: 100 }]}><Input maxLength={100} /></Form.Item>}
      <Form.Item name="modelName" label="Model name" rules={[{ required: true, whitespace: true, max: 100 }]}><Input maxLength={100} /></Form.Item>
      <div className="operations-fields">
        <Form.Item name="maxPayloadGram" label="Maximum payload (g)" rules={[{ required: true, type: 'number', max: 3000 }, { validator: (_, value) => value > 0 ? Promise.resolve() : Promise.reject(new Error('Enter a payload greater than zero.')) }]}><InputNumber min={0.01} max={3000} /></Form.Item>
        <Form.Item name="maxFlightRangeKm" label="Maximum range (km)" rules={[{ required: true, type: 'number', max: 9999.99 }, { validator: (_, value) => value > 0 ? Promise.resolve() : Promise.reject(new Error('Enter a range greater than zero.')) }]}><InputNumber min={0.01} max={9999.99} /></Form.Item>
      </div>
      <Form.Item name="batteryCapacityMah" label="Battery capacity (mAh)" extra="Design capacity of the battery." rules={[{ required: true, type: 'integer', min: 1 }]}><InputNumber min={1} precision={0} style={{ width: '100%' }} /></Form.Item>
      {!drone && <Form.Item name="currentStationId" label="Initial station (optional)"><Select allowClear placeholder="No station assigned" options={stations.data?.map(station => ({ value: station.id, label: station.name }))} /></Form.Item>}
      {!drone && <p className="operations-small">New drones start in maintenance and need an inspection before release.</p>}
      <Button type="primary" htmlType="submit" loading={busy}>Save drone</Button>
    </Form>
  </section>
}

function MaintenanceForm({ drone, refresh }: { drone: DroneDetails; refresh: () => void }) {
  const [form] = Form.useForm<{ reason: string; inspectionPassed: boolean; readyForService: string }>()
  const complete = drone.status === 'MAINTENANCE'
  const allowed = !drone.activeMissionId && (complete || drone.status === 'IDLE' || drone.status === 'CHARGING')
  const [busy, setBusy] = useState(false)
  const pending = useRef(false)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [reviewNeeded, setReviewNeeded] = useState(false)
  async function save(values: { reason: string; inspectionPassed: boolean; readyForService: string }) {
    if (!allowed || pending.current || reviewNeeded) return
    pending.current = true
    setBusy(true)
    try {
      const result = await droneApi.maintenance(drone.id, { ...values, readyForService: values.readyForService === 'ready', action: complete ? 'COMPLETE' : 'START', expectedStatus: drone.status })
      setNotice('Maintenance saved. Current status: ' + result.status + '.')
      setReviewNeeded(true)
    } catch (cause) {
      form.setFields(validationFields(cause, ['reason', 'inspectionPassed', 'readyForService'] as const))
      setError(uncertainMutation(cause) ? 'The change could not be confirmed. Refresh the drone before another action.' : operationError(cause))
      setReviewNeeded(true)
    } finally { pending.current = false; setBusy(false) }
  }
  return <section className="operations-panel"><h2>Maintenance</h2>
    {notice && <Alert type="success" title={notice} />}
    {error && <Alert type="error" showIcon title={error} />}
    {!allowed ? <p>Maintenance is unavailable while this drone is assigned, flying or linked to an active mission.</p> : reviewNeeded ? <Button onClick={refresh}>Refresh drone</Button> :
      <Form form={form} layout="vertical" onFinish={save} disabled={busy}>
        {complete && <>
          <Form.Item name="inspectionPassed" valuePropName="checked" rules={[{ validator: (_, value) => value === true ? Promise.resolve() : Promise.reject(new Error('Confirm that the inspection passed.')) }]}><Checkbox>Inspection passed</Checkbox></Form.Item>
          <Form.Item name="readyForService" label="Ready for service" rules={[{ required: true }]}><Select options={[{ value: 'ready', label: 'Ground checks complete and battery ready' }, { value: 'charging', label: 'Charging still required' }]} /></Form.Item>
        </>}
        <Form.Item name="reason" label="Reason" rules={[{ required: true, whitespace: true, min: 5, max: 500, transform: value => value?.trim() }]}><Input.TextArea rows={3} maxLength={500} /></Form.Item>
        <Button type="primary" htmlType="submit" loading={busy}>{complete ? 'Complete maintenance' : 'Start maintenance'}</Button>
      </Form>}
  </section>
}

export function DronePage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const load = useCallback((signal: AbortSignal) => id ? droneApi.get(id, signal) : Promise.resolve(null), [id])
  const resource = useResource(load)

  const refresh = resource.reload
  function saved(savedId: string) { if (!id) navigate('/drones/' + savedId); else refresh() }
  const drone = resource.data
  return <OperationsPage title={id ? drone?.serialNumber || 'Drone details' : 'Add drone'} subtitle="Manage fleet capabilities and maintenance readiness." action={<Link to="/drones">← Drone fleet</Link>}>
    <ResourceState loading={resource.loading} error={resource.error} retry={refresh} />
    {!id && !resource.loading && <DroneForm onSaved={saved} />}
    {drone && <div className="operations-grid"><div><DroneForm drone={drone} onSaved={saved} /><MaintenanceForm drone={drone} refresh={refresh} /></div>
      <section className="operations-panel"><h2>Drone overview</h2><dl className="operations-facts">
        <div><dt>Serial number</dt><dd>{drone.serialNumber}</dd></div><div><dt>Status</dt><dd><StatusTag status={drone.status} /></dd></div>
        <div><dt>Station</dt><dd>{drone.currentStationName || 'Not at a station'}</dd></div>
        <div><dt>Maximum payload</dt><dd>{drone.maxPayloadGram} g</dd></div><div><dt>Maximum range</dt><dd>{drone.maxFlightRangeKm} km</dd></div>
        <div><dt>Battery capacity</dt><dd>{drone.batteryCapacityMah.toLocaleString()} mAh</dd></div><div><dt>Active mission</dt><dd>{drone.activeMissionId || 'None'}</dd></div>
      </dl><Button className="operations-actions" onClick={refresh}>Refresh drone</Button></section>
    </div>}
  </OperationsPage>
}
