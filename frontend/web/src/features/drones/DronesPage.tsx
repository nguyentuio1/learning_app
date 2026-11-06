import { useCallback, useState } from 'react'
import { Button, Form, Input, Select, Table } from 'antd'
import { Link, useNavigate } from 'react-router-dom'
import { OperationsPage, Metrics, ResourceState, StatusTag } from '../operations/OperationsUi'
import { useResource } from '../operations/useResource'
import { droneApi, droneStatuses, type Drone, type DroneStatus } from './droneApi'

export function DronesPage() {
  const navigate = useNavigate()
  const [filters, setFilters] = useState<{ search?: string; stationId?: string; status?: DroneStatus }>({})
  const load = useCallback((signal: AbortSignal) => droneApi.list(signal, filters), [filters])
  const resource = useResource(load)
  const stations = useResource(droneApi.stations)
  return <OperationsPage title="Drone fleet" subtitle="Fleet records, operating limits and maintenance readiness." action={<Button type="primary" onClick={() => navigate('/drones/new')}>Add drone</Button>}>
    {resource.data && <Metrics items={[
      { label: 'Drones in view', value: resource.data.length },
      { label: 'Idle', value: resource.data.filter(drone => drone.status === 'IDLE').length },
      { label: 'Assigned / flying', value: resource.data.filter(drone => ['ASSIGNED', 'FLYING'].includes(drone.status)).length },
      { label: 'Maintenance', value: resource.data.filter(drone => drone.status === 'MAINTENANCE').length },
    ]} />}
    <ResourceState loading={false} error={stations.error} retry={stations.reload} />
    <section className="operations-table"><Form className="operations-filters" layout="vertical" onFinish={values => setFilters({ ...values, search: values.search?.trim() || undefined })}>
      <Form.Item name="search" label="Search"><Input allowClear placeholder="Serial number or model" /></Form.Item>
      <Form.Item name="status" label="Status"><Select allowClear placeholder="All statuses" options={droneStatuses.map(value => ({ value, label: value }))} /></Form.Item>
      <Form.Item name="stationId" label="Station"><Select allowClear placeholder="All stations" loading={stations.loading} options={stations.data?.map(station => ({ value: station.id, label: station.name }))} /></Form.Item>
      <Button type="primary" htmlType="submit">Apply filters</Button><Button onClick={resource.reload}>Refresh</Button>
    </Form>
      <ResourceState loading={resource.loading} error={resource.error} retry={resource.reload} />
      {resource.data && <Table<Drone> dataSource={resource.data} rowKey="id" pagination={false} scroll={{ x: 950 }} columns={[
        { title: 'Drone', dataIndex: 'serialNumber', render: (serial, drone) => <Link to={'/drones/' + drone.id}>{serial}<small className="operations-small">{drone.modelName}</small></Link> },
        { title: 'Station', dataIndex: 'currentStationName', render: value => value || 'Not at a station' },
        { title: 'Payload / range', render: (_, drone) => <>{drone.maxPayloadGram} g / {drone.maxFlightRangeKm} km</> },
        { title: 'Battery capacity', dataIndex: 'batteryCapacityMah', render: value => value.toLocaleString() + ' mAh' },
        { title: 'Status', dataIndex: 'status', render: status => <StatusTag status={status} /> },
        { title: 'Action', render: (_, drone) => <Link to={'/drones/' + drone.id}>Details →</Link> },
      ]} />}
    </section>
  </OperationsPage>
}
