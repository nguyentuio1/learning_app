import { Alert, Button, Form, Input, Select, Table } from 'antd'
import { Link } from 'react-router-dom'
import dayjs from 'dayjs'
import { useResource } from '../operations/useResource'
import { ResourceState, StatusTag } from '../operations/OperationsUi'
import { stationApi } from '../stations/stationApi'
import { orderDateBoundary, orderTime } from '../../utils/orderDisplay'
import type { FailedOrder, FailureFilters } from './failureApi'
import { failureTypeOptions } from './failureOptions'

const loadStations = (signal: AbortSignal) => stationApi.list(signal)
interface Values extends Pick<FailureFilters, 'originStationId' | 'destinationStationId' | 'searchKeyword' | 'failureType' | 'resolutionStatus'> { from?: string; through?: string }
export function FailedOrders({ data, loading, error, reload, filters, onFilter }: {
  data?: { items: FailedOrder[]; totalCount: number }; loading: boolean; error?: unknown; reload: () => void
  filters: FailureFilters; onFilter: (filters: FailureFilters) => void
}) {
  const stations = useResource(loadStations)
  const [form] = Form.useForm<Values>()
  const options = stations.data?.map(s => ({ value: s.id, label: `${s.stationCode} · ${s.name}` }))
  function apply(values: Values) {
    if (values.from && values.through && values.from > values.through) {
      form.setFields([{ name: 'through', errors: ['End date must not be before start date.'] }]); return
    }
    onFilter({ page: 1, pageSize: filters.pageSize, originStationId: values.originStationId, destinationStationId: values.destinationStationId,
      searchKeyword: values.searchKeyword?.trim() || undefined, failureType: values.failureType, resolutionStatus: values.resolutionStatus || 'UNRESOLVED',
      fromDate: orderDateBoundary(values.from, false), toDate: orderDateBoundary(values.through, true) })
  }
  return <section className="operations-table">
    <Form layout="vertical" className="operations-filters" form={form} onFinish={apply} initialValues={{ ...filters,
      from: filters.fromDate ? dayjs(filters.fromDate).format('YYYY-MM-DD') : undefined,
      through: filters.toDate ? dayjs(filters.toDate).format('YYYY-MM-DD') : undefined }}>
      <Form.Item name="searchKeyword" label="Tracking number" rules={[{ max: 50 }]}><Input maxLength={50} allowClear placeholder="Search tracking number" /></Form.Item>
      <Form.Item name="resolutionStatus" label="Resolution"><Select options={[{ value: 'UNRESOLVED', label: 'Awaiting resolution' }, { value: 'RETURN_REQUESTED', label: 'Return requested' }, { value: 'RESOLVED', label: 'Resolved' }, { value: 'ALL', label: 'All resolutions' }]} /></Form.Item>
      <Form.Item name="failureType" label="Failure type"><Select allowClear placeholder="All failure types" options={failureTypeOptions} /></Form.Item>
      <Form.Item name="originStationId" label="Origin"><Select allowClear showSearch optionFilterProp="label" placeholder="All origins" options={options} loading={stations.loading} /></Form.Item>
      <Form.Item name="destinationStationId" label="Destination"><Select allowClear showSearch optionFilterProp="label" placeholder="All destinations" options={options} loading={stations.loading} /></Form.Item>
      <Form.Item name="from" label="Failed from"><Input type="date" /></Form.Item>
      <Form.Item name="through" label="Failed through"><Input type="date" /></Form.Item>
      <Button type="primary" htmlType="submit">Apply filters</Button>
      <Button onClick={() => { form.setFieldsValue({ searchKeyword: undefined, resolutionStatus: 'UNRESOLVED', failureType: undefined, originStationId: undefined, destinationStationId: undefined, from: undefined, through: undefined }); onFilter({ page: 1, pageSize: 20, resolutionStatus: 'UNRESOLVED' }) }}>Reset</Button>
    </Form>
    {!!stations.error && <Alert type="warning" title="Station filters could not be loaded." action={<Button onClick={stations.reload}>Retry stations</Button>} />}
    <ResourceState loading={loading} error={error} retry={reload} />
    {data && <Table<FailedOrder> rowKey="orderId" dataSource={data.items} scroll={{ x: 1100 }}
      locale={{ emptyText: 'No failed deliveries match these filters.' }}
      pagination={{ current: filters.page, pageSize: filters.pageSize, total: data.totalCount, showSizeChanger: true, pageSizeOptions: [10, 20, 50, 100],
        onChange: (page, pageSize) => onFilter({ ...filters, page: pageSize === filters.pageSize ? page : 1, pageSize }) }}
      columns={[
        { title: 'Delivery', render: (_, row) => <><Link to={'/dispatcher/failures/' + row.orderId}>{row.trackingNumber}</Link><small className="operations-small">{row.packageName}</small></> },
        { title: 'Route', render: (_, row) => <>{row.originStation.name} → {row.destinationStation.name}</> },
        { title: 'Latest failure', render: (_, row) => <>{failureTypeOptions.find(type => type.value === row.failureType)?.label || row.failureType}<small className="operations-small">{orderTime(row.failedAt)}</small><small className="operations-small">{row.failureReason || 'No reason recorded'}</small></> },
        { title: 'Order status', render: (_, row) => <StatusTag status={row.status} /> },
        { title: 'Resolution', render: (_, row) => <><StatusTag status={row.resolutionStatus} /><small className="operations-small">{row.lastResolutionAction?.replaceAll('_', ' ') || 'No action recorded'}</small></> },
        { title: 'Action', render: (_, row) => <Link to={'/dispatcher/failures/' + row.orderId}><Button>View delivery →</Button></Link> },
      ]} />}
  </section>
}
