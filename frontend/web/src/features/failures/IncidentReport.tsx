import { useRef, useState } from 'react'
import { Alert, Button, Form, Input, Modal, Select } from 'antd'
import axios from 'axios'
import { useResource } from '../operations/useResource'
import { ResourceState } from '../operations/OperationsUi'
import { operationError, uncertainMutation, validationFields } from '../operations/feedback'
import { failureApi, type FailureType } from './failureApi'
import { failureTypeOptions } from './failureOptions'

const load = (signal: AbortSignal) => failureApi.active(signal)
interface Values { orderId: string; failureType: FailureType; reason: string }
export function IncidentReport({ onClose, onReported }: { onClose: () => void; onReported: (id: string) => void }) {
  const active = useResource(load)
  const [form] = Form.useForm<Values>()
  const [pending, setPending] = useState<Values>()
  const [saving, setSaving] = useState(false)
  const busy = useRef(false)
  const [error, setError] = useState('')
  const [uncertain, setUncertain] = useState(false)
  const [uncertainOrder, setUncertainOrder] = useState<string>()
  const flights = active.data?.filter(item => item.orderStatus === 'IN_TRANSIT')
  async function save() {
    if (!pending || busy.current || uncertain) return
    busy.current = true; setSaving(true); setError('')
    try { await failureApi.report(pending.orderId, pending.failureType, pending.reason); onReported(pending.orderId) }
    catch (cause) {
      setError(axios.isAxiosError(cause) && typeof cause.response?.data?.detail === 'string' ? cause.response.data.detail : operationError(cause))
      setUncertain(uncertainMutation(cause))
      if (uncertainMutation(cause)) setUncertainOrder(pending.orderId)
      form.setFields(validationFields(cause, ['failureType', 'reason'] as const))
      if (uncertainMutation(cause) || (axios.isAxiosError(cause) && [404, 409].includes(cause.response?.status || 0))) {
        active.reload(); form.setFieldValue('orderId', undefined)
      }
    } finally { busy.current = false; setSaving(false); setPending(undefined) }
  }
  return <section className="operations-panel"><h2>Report an in-flight incident</h2>
    <p>Choose a delivery and record a confirmed incident. An outdated position alone does not indicate a failed delivery.</p>
    {error && <Alert type="error" showIcon title={error} />}
    {uncertain && <Alert type="warning" showIcon title="The result could not be confirmed. Check this delivery before submitting another report."
      action={<Button onClick={() => { if (uncertainOrder) onReported(uncertainOrder) }}>Check delivery</Button>} />}
    <ResourceState loading={active.loading} error={active.error} retry={active.reload} empty={flights?.length === 0} />
    <Form form={form} layout="vertical" disabled={saving || uncertain} onFinish={values => {
      if (!flights?.some(item => item.orderId === values.orderId)) { form.setFields([{ name: 'orderId', errors: ['Refresh and select an in-transit delivery.'] }]); return }
      setPending(values)
    }}>
      <Form.Item name="orderId" label="In-transit delivery" rules={[{ required: true, message: 'Choose a delivery.' }]}><Select showSearch optionFilterProp="label" options={flights?.map(item => ({ value: item.orderId, label: `${item.trackingNumber} · ${item.droneSerialNumber}` }))} /></Form.Item>
      <Form.Item name="failureType" label="Failure type" rules={[{ required: true, message: 'Select a failure type.' }]}><Select options={failureTypeOptions.filter(item => !['COLLECTION_TIMEOUT', 'UNKNOWN'].includes(item.value))} /></Form.Item>
      <Form.Item name="reason" label="Incident reason" rules={[{ required: true, whitespace: true, max: 500, message: 'Enter a reason of 1 to 500 characters.' }]}><Input.TextArea rows={4} maxLength={500} showCount /></Form.Item>
      <div className="operations-actions"><Button type="primary" htmlType="submit" loading={saving} disabled={uncertain || !flights?.length}>Review incident</Button><Button onClick={onClose} disabled={saving}>Close report</Button></div>
    </Form>
    <Modal title="Record this failure?" open={!!pending} confirmLoading={saving} onOk={() => void save()} onCancel={() => { if (!saving) setPending(undefined) }}
      okButtonProps={{ disabled: uncertain }} cancelButtonProps={{ disabled: saving }} closable={!saving} keyboard={!saving} mask={{ closable: !saving }}>
      <p><strong>{flights?.find(item => item.orderId === pending?.orderId)?.trackingNumber}</strong></p><p>{pending?.failureType.replaceAll('_', ' ')}</p><p>{pending?.reason}</p>
      <p>This marks the delivery as failed and notifies the customer. It does not remotely control the drone.</p>
    </Modal>
  </section>
}
