import { useCallback, useRef, useState } from 'react'
import { Alert, Button, Form, Input, InputNumber, Modal, Select } from 'antd'
import { useAuth } from '../../hooks/useAuth'
import { OperationsPage, Metrics, ResourceState, StatusTag } from '../operations/OperationsUi'
import { useResource } from '../operations/useResource'
import { operationError, uncertainMutation, validationFields } from '../operations/feedback'
import type { StationSlot } from '../operations/stationTypes'
import { slotApi } from './slotApi'

type Action = { kind: 'add' } | { kind: 'remove' | 'status'; slot: StationSlot }

function SlotInventory({ stationId }: { stationId: string }) {
  const { user } = useAuth()
  const load = useCallback((signal: AbortSignal) => slotApi.inventory(stationId, signal), [stationId])
  const resource = useResource(load)
  const [action, setAction] = useState<Action | null>(null)
  const [busy, setBusy] = useState(false)
  const pending = useRef(false)
  const [form] = Form.useForm<{ reason: string; slotNumber?: number }>()
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [reviewNeeded, setReviewNeeded] = useState(false)
  const isAdmin = user?.role.code === 'ADMIN'
  const canChange = isAdmin || (user?.role.code === 'STATION_OPERATOR' && resource.data?.station.operatorId === user.id)
  function open(next: Action) { form.resetFields(); setError(''); setReviewNeeded(false); setAction(next) }
  async function save(values: { reason: string; slotNumber?: number }) {
    if (!action || pending.current || reviewNeeded) return
    pending.current = true
    setBusy(true)
    try {
      if (action.kind === 'add') await slotApi.add(stationId, values.slotNumber)
      else if (action.kind === 'remove') await slotApi.remove(action.slot.id)
      else await slotApi.status(action.slot.id, action.slot.status === 'EMPTY' ? 'ERROR' : 'EMPTY', values.reason)
      setAction(null)
      setNotice('Slot inventory updated.')
      resource.reload()
    } catch (cause) {
      form.setFields(validationFields(cause, ['reason', 'slotNumber'] as const))
      setError(uncertainMutation(cause) ? 'The result could not be confirmed. Close this dialog and review the refreshed inventory before another action.' : operationError(cause))
      setReviewNeeded(true)
      resource.reload()
    } finally { pending.current = false; setBusy(false) }
  }
  return <>
    {notice && <Alert type="success" showIcon title={notice} />}
    <ResourceState loading={resource.loading} error={resource.error} retry={resource.reload} />
    {resource.data && <>
      <Metrics items={[
        { label: 'Total slots', value: resource.data.slots.length },
        { label: 'Empty', value: resource.data.slots.filter(slot => slot.status === 'EMPTY').length },
        { label: 'Occupied / reserved', value: resource.data.slots.filter(slot => ['OCCUPIED', 'RESERVED'].includes(slot.status)).length },
        { label: 'Errors', value: resource.data.slots.filter(slot => slot.status === 'ERROR').length },
      ]} />
      <section className="operations-panel"><h2>{resource.data.station.name} · Slots</h2>
        <div className="operations-actions"><Button onClick={resource.reload}>Refresh slots</Button>
          {isAdmin && <Button type="primary" disabled={resource.data.slots.length >= 20} onClick={() => open({ kind: 'add' })}>Add slot</Button>}
        </div>
        <ResourceState loading={false} empty={!resource.data.slots.length} retry={resource.reload} />
        <div className="operations-slot-grid">{resource.data.slots.map(slot => {
          const manuallyManaged = slot.status === 'EMPTY' || slot.status === 'ERROR'
          const linked = !!slot.currentMissionId || !!slot.currentOrderId
          return <article className="operations-slot" key={slot.id}><h3>Slot {slot.slotNumber}</h3><StatusTag status={slot.status} />
            <p>Updated {new Date(slot.lastUpdated).toLocaleString()}</p>
            {slot.currentOrderId && <p>Order {slot.currentOrderId}</p>}
            {slot.currentMissionId && <p>Mission {slot.currentMissionId}</p>}
            {canChange && manuallyManaged && <Button onClick={() => open({ kind: 'status', slot })}>{slot.status === 'EMPTY' ? 'Mark unavailable' : 'Restore availability'}</Button>}
            {isAdmin && <Button danger disabled={!manuallyManaged || linked || resource.data!.slots.length <= 1} onClick={() => open({ kind: 'remove', slot })}>Remove slot</Button>}
            {!manuallyManaged && <p>Managed by the delivery workflow.</p>}
          </article>
        })}</div>
      </section>
    </>}
    <Modal open={!!action} title={action?.kind === 'add' ? 'Add a slot' : action?.kind === 'remove' ? 'Remove this slot?' : 'Change slot availability'} footer={null} destroyOnHidden closable={!busy} mask={{ closable: !busy }} keyboard={!busy} onCancel={() => setAction(null)}>
      {error && <Alert type="error" title={error} showIcon />}
      <Form form={form} layout="vertical" onFinish={save} disabled={busy || reviewNeeded}>
        {action?.kind === 'add' && <Form.Item name="slotNumber" label="Slot number (optional)" extra="Leave empty to use the next number." rules={[{ type: 'integer', min: 1 }]}><InputNumber min={1} precision={0} /></Form.Item>}
        {action?.kind === 'status' && <>
          <p>Slot {action.slot.slotNumber} will change from {action.slot.status} to {action.slot.status === 'EMPTY' ? 'ERROR' : 'EMPTY'}.</p>
          <Form.Item name="reason" label="Reason" rules={[{ required: true, whitespace: true, min: 5, max: 500, transform: value => value?.trim() }]}><Input.TextArea rows={3} maxLength={500} /></Form.Item>
        </>}
        {action?.kind === 'remove' && <p>Remove slot {action.slot.slotNumber}? The remaining slots keep their current numbers.</p>}
        <Button type="primary" danger={action?.kind === 'remove'} htmlType="submit" loading={busy}>Confirm</Button>
      </Form>
    </Modal>
  </>
}

export function StationSlotsPage() {
  const { user } = useAuth()
  const stations = useResource(slotApi.stations)
  const [stationId, setStationId] = useState<string>()
  const choices = stations.data?.filter(station => user?.role.code !== 'STATION_OPERATOR' || station.operatorId === user.id)
  return <OperationsPage title="Station slots" subtitle="Manage slot inventory and maintenance availability.">
    <section className="operations-panel"><label htmlFor="slot-station">Station</label>
      <Select id="slot-station" aria-label="Station" style={{ width: '100%', marginTop: 8 }} value={stationId} onChange={setStationId} placeholder="Choose a station" loading={stations.loading}
        options={choices?.map(station => ({ value: station.id, label: station.name + ' · ' + station.stationCode }))} />
      <ResourceState loading={false} error={stations.error} retry={stations.reload} />
      {choices?.length === 0 && <p>No station is available for your account.</p>}
    </section>
    {stationId && <SlotInventory key={stationId} stationId={stationId} />}
  </OperationsPage>
}
