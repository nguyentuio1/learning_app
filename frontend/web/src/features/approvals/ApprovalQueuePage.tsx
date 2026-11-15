import { useCallback, useRef, useState } from 'react'
import axios from 'axios'
import { Alert, Button, Form, Input, Modal, Select, Table } from 'antd'
import { OperationsPage, ResourceState } from '../operations/OperationsUi'
import { useResource } from '../operations/useResource'
import { operationError, uncertainMutation, validationFields } from '../operations/feedback'
import { stationApi } from '../stations/stationApi'
import { orderDateBoundary, orderTime } from '../../utils/orderDisplay'
import { approvalApi, type PendingOrder, type QueueFilters } from './approvalApi'

const loadStations = (signal: AbortSignal) => stationApi.list(signal)
interface FilterValues { originStationId?: string; destinationStationId?: string; from?: string; through?: string }
function waitingSince(created: string) {
  const minutes = Math.max(0, Math.floor((Date.now() - Date.parse(created)) / 60000))
  return Number.isFinite(minutes) ? (minutes < 60 ? `${minutes} min` : `${Math.floor(minutes / 60)} h ${minutes % 60} min`) : 'Unavailable'
}
export function ApprovalQueuePage() {
  const [filters, setFilters] = useState<QueueFilters>({ page: 1, pageSize: 20 })
  const [filterForm] = Form.useForm<FilterValues>()
  const [decisionForm] = Form.useForm<{ reason: string }>()
  const load = useCallback((signal: AbortSignal) => approvalApi.queue(filters, signal), [filters])
  const queue = useResource(load)
  const stations = useResource(loadStations)
  const [selected, setSelected] = useState<{ order: PendingOrder; action: 'approve' | 'reject' }>()
  const [saving, setSaving] = useState(false)
  const submitting = useRef(false)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const stationOptions = stations.data?.map(station => ({ value: station.id, label: `${station.name} · ${station.stationCode}` }))
  function filter(values: FilterValues) {
    if (values.from && values.through && values.from > values.through) {
      filterForm.setFields([{ name: 'through', errors: ['End date must not be before start date.'] }]); return
    }
    setFilters({ page: 1, pageSize: filters.pageSize, originStationId: values.originStationId, destinationStationId: values.destinationStationId,
      fromDate: orderDateBoundary(values.from, false), toDate: orderDateBoundary(values.through, true) })
  }
  function choose(order: PendingOrder, action: 'approve' | 'reject') {
    setError(''); setNotice(''); decisionForm.resetFields(); setSelected({ order, action })
  }
  function refreshAfterDecision() {
    setFilters(previous => ({ ...previous, page: 1 }))
  }
  async function decide(values: { reason?: string }) {
    if (!selected || submitting.current) return
    submitting.current = true; setSaving(true); setError('')
    try {
      const result = selected.action === 'approve' ? await approvalApi.approve(selected.order.orderId) : await approvalApi.reject(selected.order.orderId, values.reason || '')
      setNotice(`${result.trackingNumber}: ${result.status.replaceAll('_', ' ')}. The queue has been refreshed.`)
      setSelected(undefined); refreshAfterDecision()
    } catch (cause) {
      decisionForm.setFields(validationFields(cause, ['reason'] as const))
      if (uncertainMutation(cause)) {
        setNotice('The decision could not be confirmed. Check the refreshed queue before making another decision.')
        setSelected(undefined); refreshAfterDecision()
      } else if (axios.isAxiosError(cause) && (cause.response?.status === 409 || cause.response?.status === 404)) {
        setNotice(typeof cause.response.data?.detail === 'string' ? cause.response.data.detail : operationError(cause))
        setSelected(undefined); refreshAfterDecision()
      } else { setError(operationError(cause)) }
    } finally { submitting.current = false; setSaving(false) }
  }
  return <OperationsPage title="Delivery approvals" subtitle="Review pending requests, oldest first. Approval does not schedule a departure or assign a drone."
    action={<Button onClick={queue.reload} disabled={saving || queue.loading}>Refresh queue</Button>}>
    {notice && <Alert type="info" showIcon title={notice} />}
    <section className="operations-table">
      <Form form={filterForm} layout="vertical" className="operations-filters" onFinish={filter} disabled={saving}>
        <Form.Item name="originStationId" label="Origin station"><Select allowClear showSearch optionFilterProp="label" placeholder="All origins" loading={stations.loading} options={stationOptions} /></Form.Item>
        <Form.Item name="destinationStationId" label="Destination station"><Select allowClear showSearch optionFilterProp="label" placeholder="All destinations" loading={stations.loading} options={stationOptions} /></Form.Item>
        <Form.Item name="from" label="Created from"><Input type="date" /></Form.Item>
        <Form.Item name="through" label="Created through"><Input type="date" /></Form.Item>
        <Button type="primary" htmlType="submit">Apply filters</Button>
        <Button onClick={() => { filterForm.resetFields(); setFilters({ page: 1, pageSize: 20 }) }}>Reset</Button>
      </Form>
      {!!stations.error && <Alert type="warning" showIcon title="Station filters could not be loaded." action={<Button onClick={stations.reload}>Retry stations</Button>} />}
      <ResourceState loading={queue.loading} error={queue.error} retry={queue.reload} />
      {queue.data && <Table<PendingOrder> rowKey="orderId" dataSource={queue.data.items} scroll={{ x: 1050 }}
        locale={{ emptyText: 'No pending requests match these filters.' }}
        pagination={{ current: filters.page, pageSize: filters.pageSize, total: queue.data.totalCount, showSizeChanger: true, pageSizeOptions: [10, 20, 50, 100],
          onChange: (page, pageSize) => setFilters(previous => ({ ...previous, page: pageSize === previous.pageSize ? page : 1, pageSize })) }} columns={[
          { title: 'Request', render: (_, order) => <><strong>{order.trackingNumber}</strong><small className="operations-small">{orderTime(order.createdAt)} · waiting {waitingSince(order.createdAt)}</small></> },
          { title: 'Customer', render: (_, order) => <>{order.customerName}<small className="operations-small">{order.customerPhone}</small></> },
          { title: 'Package', render: (_, order) => <>{order.packageName}<small className="operations-small">{order.weightGram} g · {order.lengthCm} × {order.widthCm} × {order.heightCm} cm{order.isFragile ? ' · Fragile' : ''}</small></> },
          { title: 'Route', render: (_, order) => <>{order.originStation.name} → {order.destinationStation.name}</> },
          { title: 'Decision', render: (_, order) => <div className="operations-actions"><Button type="primary" disabled={saving} onClick={() => choose(order, 'approve')}>Approve</Button><Button danger disabled={saving} onClick={() => choose(order, 'reject')}>Reject</Button></div> },
        ]} />}
    </section>
    <Modal open={!!selected} title={selected?.action === 'approve' ? 'Approve delivery request?' : 'Reject delivery request?'} footer={null}
      onCancel={() => { if (!saving) setSelected(undefined) }} closable={!saving} keyboard={!saving} mask={{ closable: !saving }} destroyOnHidden>
      <p><strong>{selected?.order.trackingNumber}</strong> · {selected?.order.originStation.name} → {selected?.order.destinationStation.name}</p>
      <p>{selected?.action === 'approve' ? 'The customer will be notified. Scheduling and drone assignment remain separate steps.' : 'This cancels the pending request and notifies the customer.'}</p>
      {error && <Alert type="error" showIcon title={error} />}
      <Form form={decisionForm} layout="vertical" onFinish={decide} disabled={saving}>
        {selected?.action === 'reject' && <Form.Item name="reason" label="Reason for rejection" rules={[{ required: true, whitespace: true, max: 500, message: 'Enter a reason of 1 to 500 characters.' }]}><Input.TextArea rows={4} maxLength={500} showCount /></Form.Item>}
        <div className="operations-actions"><Button onClick={() => setSelected(undefined)} disabled={saving}>Keep pending</Button><Button type="primary" danger={selected?.action === 'reject'} htmlType="submit" loading={saving}>Confirm {selected?.action === 'approve' ? 'approval' : 'rejection'}</Button></div>
      </Form>
    </Modal>
  </OperationsPage>
}
