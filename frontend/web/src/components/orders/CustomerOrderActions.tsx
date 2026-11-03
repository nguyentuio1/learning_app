import { useRef, useState } from 'react'
import axios from 'axios'
import { Alert, Button, Form, Input, Modal } from 'antd'
import { Link } from 'react-router-dom'
import type { CustomerOrderDetail } from '../../types/customerOrder'
import { orderMutationApi } from '../../services/orderMutationApi'
import { orderFeedback } from '../../utils/orderFeedback'

export function CustomerOrderActions({ order, onRefresh }: { order: CustomerOrderDetail; onRefresh: (message: string) => void }) {
  const [open, setOpen] = useState(false)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')
  const [form] = Form.useForm<{ cancellationReason: string }>()
  const submitting = useRef(false)
  const canCancel = order.status === 'PENDING' || order.status === 'APPROVED'
  async function cancel({ cancellationReason }: { cancellationReason: string }) {
    if (submitting.current || !canCancel) return
    submitting.current = true; setSaving(true); setError('')
    try {
      await orderMutationApi.cancel(order.id, cancellationReason)
      onRefresh('Order cancelled successfully.')
    } catch (cause) {
      if (axios.isAxiosError(cause) && (cause.response?.status === 409 || !cause.response || cause.response.status >= 500)) {
        onRefresh(cause.response?.status === 409 ? 'The order status changed. Review its latest details before taking another action.' : 'We could not confirm the cancellation. Review the latest status before trying again.')
      } else setError(orderFeedback(cause, 'Unable to cancel this order.'))
    } finally { submitting.current = false; setSaving(false) }
  }
  return <>
    <div className="order-actions">
      {order.status === 'PENDING' && <Link className="order-primary-link" to={`/orders/${encodeURIComponent(order.id)}/edit`}>Edit order</Link>}
      {canCancel && <Button danger onClick={() => { setOpen(true); setError('') }}>Cancel order</Button>}
    </div>
    {!canCancel && <p className="order-help">This order can no longer be edited or cancelled.</p>}
    {order.status === 'APPROVED' && <p className="order-help">Approved orders can be cancelled, but their details can no longer be edited.</p>}
    <Modal title="Cancel this delivery?" open={open} footer={null} onCancel={() => { if (!saving) setOpen(false) }} closable={!saving} mask={{ closable: !saving }} keyboard={!saving} destroyOnHidden>
      <p>Cancel <strong>{order.trackingNumber}</strong>? This action cannot be undone.</p>
      <Form form={form} layout="vertical" onFinish={cancel} disabled={saving}>
        {error && <Alert type="error" showIcon title={error} />}
        <Form.Item name="cancellationReason" label="Reason for cancellation" rules={[{ required: true, whitespace: true, max: 500, message: 'Enter a reason of up to 500 characters.' }]}>
          <Input.TextArea rows={4} maxLength={500} showCount autoFocus />
        </Form.Item>
        <div className="order-actions"><Button onClick={() => setOpen(false)} disabled={saving}>Keep order</Button><Button danger type="primary" htmlType="submit" loading={saving}>Confirm cancellation</Button></div>
      </Form>
    </Modal>
  </>
}
