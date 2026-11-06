import { useCallback, useRef, useState } from 'react'
import { Alert, Button, Form, Input, InputNumber, Select } from 'antd'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { useAuth } from '../../hooks/useAuth'
import { userManagementApi } from '../../services/userManagementApi'
import { OperationsPage, ResourceState, StatusTag } from '../operations/OperationsUi'
import { operationError, uncertainMutation, validationFields } from '../operations/feedback'
import { useResource } from '../operations/useResource'
import { stationStatuses, type StationStatus } from '../operations/stationTypes'
import { stationApi, type StationDetails, type StationValues } from './stationApi'

function StationForm({ station, onSaved }: { station?: StationDetails; onSaved: (id: string) => void }) {
  const [form] = Form.useForm<StationValues>()
  const operators = useResource(userManagementApi.list)
  const [busy, setBusy] = useState(false)
  const pending = useRef(false)
  const [error, setError] = useState('')
  const [uncertain, setUncertain] = useState(false)
  async function save(values: StationValues) {
    if (pending.current || uncertain) return
    pending.current = true
    setBusy(true)
    setError('')
    try {
      const saved = station ? await stationApi.update(station.id, values) : await stationApi.create(values)
      onSaved(saved.id)
    } catch (cause) {
      form.setFields(validationFields(cause, ['stationCode', 'name', 'address', 'latitude', 'longitude', 'totalSlots', 'operatorId'] as const))
      if (uncertainMutation(cause)) {
        setUncertain(true)
        setError('The save could not be confirmed. Return to the station list and check the record before saving again.')
      } else setError(operationError(cause))
    } finally { pending.current = false; setBusy(false) }
  }
  return <section className="operations-panel"><h2>{station ? 'Station details' : 'New station'}</h2>
    <ResourceState loading={operators.loading} error={operators.error} retry={operators.reload} />
    {error && <Alert type="error" showIcon title={error} />}
    <Form form={form} layout="vertical" onFinish={save} disabled={busy || uncertain || operators.loading || !!operators.error}
      initialValues={station || { totalSlots: 4, operatorId: null }}>
      <div className="operations-fields">
        <Form.Item name="name" label="Station name" rules={[{ required: true, whitespace: true, max: 150 }]}><Input maxLength={150} /></Form.Item>
        {!station && <Form.Item name="stationCode" label="Station code" rules={[{ required: true, min: 3, max: 50, pattern: /^[A-Z0-9-]+$/, message: 'Use 3–50 uppercase letters, numbers or hyphens.' }]}><Input maxLength={50} /></Form.Item>}
      </div>
      <Form.Item name="address" label="Address" rules={[{ required: true, whitespace: true }]}><Input.TextArea rows={2} /></Form.Item>
      <div className="operations-fields">
        <Form.Item name="latitude" label="Latitude" rules={[{ required: true, type: 'number', min: -90, max: 90 }]}><InputNumber min={-90} max={90} /></Form.Item>
        <Form.Item name="longitude" label="Longitude" rules={[{ required: true, type: 'number', min: -180, max: 180 }]}><InputNumber min={-180} max={180} /></Form.Item>
      </div>
      <Form.Item name="operatorId" label="Station operator">
        <Select allowClear placeholder="Not assigned" options={operators.data?.filter(user => user.status === 'ACTIVE' && user.roleCode === 'STATION_OPERATOR').map(user => ({ value: user.id, label: user.fullName }))} />
      </Form.Item>
      {!station && <Form.Item name="totalSlots" label="Initial slots" rules={[{ required: true, type: 'integer', min: 1, max: 20, message: 'Enter a whole number from 1 to 20.' }]}><InputNumber /></Form.Item>}
      <Button type="primary" htmlType="submit" loading={busy}>Save station</Button>
    </Form>
  </section>
}

function StationStatusForm({ station, refresh }: { station: StationDetails; refresh: () => void }) {
  const [form] = Form.useForm<{ newStatus: StationStatus; reason: string }>()
  const [busy, setBusy] = useState(false)
  const pending = useRef(false)
  const [notice, setNotice] = useState('')
  const [error, setError] = useState('')
  const [warning, setWarning] = useState<string[]>([])
  const [needsRefresh, setNeedsRefresh] = useState(false)
  async function save(values: { newStatus: StationStatus; reason: string }) {
    if (pending.current || needsRefresh) return
    pending.current = true
    setBusy(true)
    setError('')
    setWarning([])
    try {
      const result = await stationApi.status(station.id, values.newStatus, values.reason)
      setNotice('Station status updated.')
      if (result.rerouteWarning) setWarning(result.affectedMissionIds)
      setNeedsRefresh(true)
    } catch (cause) {
      form.setFields(validationFields(cause, ['newStatus', 'reason'] as const))
      setError(uncertainMutation(cause) ? 'The change could not be confirmed. Refresh the station before another change.' : operationError(cause))
      setNeedsRefresh(true)
    } finally { pending.current = false; setBusy(false) }
  }
  return <section className="operations-panel"><h2>Operating status</h2>
    {notice && <Alert type="success" title={notice} />}
    {error && <Alert type="error" showIcon title={error} />}
    {warning.length > 0 && <Alert type="warning" showIcon title="Incoming flights need rerouting" description={<><p>Contact dispatch about these affected missions.</p><ul>{warning.map(id => <li key={id}>{id}</li>)}</ul></>} />}
    {needsRefresh ? <Button onClick={refresh}>Refresh station</Button> : <Form form={form} layout="vertical" onFinish={save} disabled={busy} initialValues={{ newStatus: station.status }}>
      <Form.Item name="newStatus" label="Operating status" rules={[{ required: true }]}><Select options={stationStatuses.map(value => ({ value, label: value }))} /></Form.Item>
      <Form.Item name="reason" label="Reason" rules={[{ required: true, whitespace: true, min: 5, max: 500, transform: value => value?.trim() }]}><Input.TextArea rows={3} maxLength={500} /></Form.Item>
      <Button htmlType="submit" type="primary" loading={busy}>Change status</Button>
    </Form>}
  </section>
}

export function StationPage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const { user } = useAuth()
  const load = useCallback((signal: AbortSignal) => id ? stationApi.get(id, signal) : Promise.resolve(null), [id])
  const resource = useResource(load)
  const isAdmin = user?.role.code === 'ADMIN'
  const station = resource.data

  const refresh = resource.reload
  function saved(savedId: string) { if (!id) navigate('/stations/' + savedId); else refresh() }
  return <OperationsPage title={id ? station?.name || 'Station details' : 'Add landing station'} subtitle="Configure a network location and its operating status." action={<Link to="/stations">← Stations</Link>}>
    <ResourceState loading={resource.loading} error={resource.error} retry={refresh} />
    {!id && !resource.loading && isAdmin && <StationForm onSaved={saved} />}
    {station && <div className="operations-grid">
      <div>{isAdmin && <StationForm station={station} onSaved={saved} />}
        {(isAdmin || (user?.role.code === 'STATION_OPERATOR' && station.operatorId === user.id)) && <StationStatusForm station={station} refresh={refresh} />}
      </div>
      <section className="operations-panel"><h2>Station overview</h2><dl className="operations-facts">
        <div><dt>Code</dt><dd>{station.stationCode}</dd></div><div><dt>Status</dt><dd><StatusTag status={station.status} /></dd></div>
        <div><dt>Address</dt><dd>{station.address}</dd></div><div><dt>Coordinates</dt><dd>{station.latitude}, {station.longitude}</dd></div>
        <div><dt>Slots / empty</dt><dd>{station.totalSlots} / {station.emptySlots}</dd></div>
        <div><dt>Operator</dt><dd>{station.operatorName || 'Not assigned'}</dd></div><div><dt>Hosted drones</dt><dd>{station.hostedDroneCount}</dd></div>
      </dl><Button onClick={refresh} className="operations-actions">Refresh station</Button></section>
    </div>}
  </OperationsPage>
}
