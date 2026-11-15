import { Alert, Button, Form, Input, Select, Table } from 'antd'
import { Link } from 'react-router-dom'
import dayjs from 'dayjs'
import { useResource } from '../operations/useResource'
import { ResourceState, StatusTag } from '../operations/OperationsUi'
import { stationApi } from '../stations/stationApi'
import { orderDateBoundary, orderTime } from '../../utils/orderDisplay'
import type { ApprovedOrder, QueueFilters } from './schedulingApi'

const loadStations = (signal: AbortSignal) => stationApi.list(signal)
interface Values extends Pick<QueueFilters, 'originStationId' | 'destinationStationId' | 'searchKeyword' | 'assignmentStatus'> {
  from?: string; through?: string; departureFrom?: string; departureThrough?: string
}
export function ApprovedOrders({ data, loading, error, reload, filters, onFilter }: {
  data?: { items: ApprovedOrder[]; totalCount: number }; loading: boolean; error?: unknown; reload: () => void
  filters: QueueFilters; onFilter: (filters: QueueFilters) => void
}) {
  const stations = useResource(loadStations)
  const [form] = Form.useForm<Values>()
  const options = stations.data?.map(s => ({ value: s.id, label: `${s.stationCode} · ${s.name}` }))
  function apply(values: Values) {
    if (values.from && values.through && values.from > values.through) {
      form.setFields([{ name: 'through', errors: ['End date must not be before start date.'] }]); return
    }
    if (values.departureFrom && values.departureThrough && values.departureFrom > values.departureThrough) {
      form.setFields([{ name: 'departureThrough', errors: ['End date must not be before start date.'] }]); return
    }
    onFilter({ page: 1, pageSize: filters.pageSize, originStationId: values.originStationId, destinationStationId: values.destinationStationId,
      searchKeyword: values.searchKeyword?.trim() || undefined, assignmentStatus: values.assignmentStatus,
      fromDate: orderDateBoundary(values.from, false), toDate: orderDateBoundary(values.through, true),
      scheduledFrom: orderDateBoundary(values.departureFrom, false), scheduledTo: orderDateBoundary(values.departureThrough, true) })
  }
  return <section className="operations-table">
    <Form layout="vertical" className="operations-filters" form={form} onFinish={apply} initialValues={{ ...filters,
      from: filters.fromDate ? dayjs(filters.fromDate).format('YYYY-MM-DD') : undefined,
      through: filters.toDate ? dayjs(filters.toDate).format('YYYY-MM-DD') : undefined,
      departureFrom: filters.scheduledFrom ? dayjs(filters.scheduledFrom).format('YYYY-MM-DD') : undefined,
      departureThrough: filters.scheduledTo ? dayjs(filters.scheduledTo).format('YYYY-MM-DD') : undefined }}>
      <Form.Item name="searchKeyword" label="Tracking number" rules={[{ max: 50 }]}><Input maxLength={50} allowClear placeholder="Search tracking number" /></Form.Item>
      <Form.Item name="assignmentStatus" label="Drone assignment"><Select allowClear placeholder="All assignments" options={[{ value: 'UNASSIGNED', label: 'Not assigned' }, { value: 'ASSIGNED', label: 'Assigned' }]} /></Form.Item>
      <Form.Item name="originStationId" label="Origin"><Select allowClear showSearch optionFilterProp="label" placeholder="All origins" options={options} loading={stations.loading} /></Form.Item>
      <Form.Item name="destinationStationId" label="Destination"><Select allowClear showSearch optionFilterProp="label" placeholder="All destinations" options={options} loading={stations.loading} /></Form.Item>
      <Form.Item name="from" label="Created from"><Input type="date" /></Form.Item>
      <Form.Item name="through" label="Created through"><Input type="date" /></Form.Item>
      <Form.Item name="departureFrom" label="Departure from"><Input type="date" /></Form.Item>
      <Form.Item name="departureThrough" label="Departure through"><Input type="date" /></Form.Item>
      <Button type="primary" htmlType="submit">Apply filters</Button>
      <Button onClick={() => { form.setFieldsValue({ searchKeyword: undefined, assignmentStatus: undefined, originStationId: undefined, destinationStationId: undefined, from: undefined, through: undefined, departureFrom: undefined, departureThrough: undefined }); onFilter({ page: 1, pageSize: 20 }) }}>Reset</Button>
    </Form>
    {!!stations.error && <Alert type="warning" title="Station filters could not be loaded." action={<Button onClick={stations.reload}>Retry stations</Button>} />}
    <ResourceState loading={loading} error={error} retry={reload} />
    {data && <Table<ApprovedOrder> rowKey="orderId" dataSource={data.items} scroll={{ x: 1050 }}
      locale={{ emptyText: 'No approved deliveries match these filters.' }}
      pagination={{ current: filters.page, pageSize: filters.pageSize, total: data.totalCount, showSizeChanger: true, pageSizeOptions: [10, 20, 50, 100],
        onChange: (page, pageSize) => onFilter({ ...filters, page: pageSize === filters.pageSize ? page : 1, pageSize }) }}
      columns={[
        { title: 'Delivery', render: (_, row) => <><Link to={'/dispatcher/scheduling/' + row.orderId}>{row.trackingNumber}</Link><small className="operations-small">Created {orderTime(row.createdAt)}</small></> },
        { title: 'Package', render: (_, row) => <>{row.packageName}<small className="operations-small">{row.weightGram} g</small></> },
        { title: 'Route', render: (_, row) => <>{row.originStation.name} → {row.destinationStation.name}</> },
        { title: 'Departure / arrival', render: (_, row) => <>{row.scheduledDepartureTime ? orderTime(row.scheduledDepartureTime) : 'Not scheduled'}<small className="operations-small">ETA: {row.estimatedDeliveryTime ? orderTime(row.estimatedDeliveryTime) : 'Not available'}</small></> },
        { title: 'Assignment', render: (_, row) => <><StatusTag status={row.assignmentStatus} /><small className="operations-small">{row.droneSerialNumber || 'No drone assigned'}</small></> },
        { title: 'Action', render: (_, row) => <Link to={'/dispatcher/scheduling/' + row.orderId}><Button>View delivery →</Button></Link> },
      ]} />}
  </section>
}
