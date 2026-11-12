import { useCallback, useState } from 'react'
import { Button, Form, Input, Select, Table } from 'antd'
import { Link, useNavigate } from 'react-router-dom'
import { useAuth } from '../../hooks/useAuth'
import { OperationsPage, Metrics, ResourceState, StatusTag } from '../operations/OperationsUi'
import { useResource } from '../operations/useResource'
import { stationStatuses, type Station, type StationStatus } from '../operations/stationTypes'
import { stationApi } from './stationApi'

export function StationsPage() {
  const { user } = useAuth()
  const navigate = useNavigate()
  const [filters, setFilters] = useState<{ search?: string; status?: StationStatus }>({})
  const load = useCallback((signal: AbortSignal) => stationApi.list(signal, filters), [filters])
  const resource = useResource(load)
  return <OperationsPage title="Landing stations" subtitle="Station configuration and slot inventory."
    action={user?.role.code === 'ADMIN' && <Button type="primary" onClick={() => navigate('/stations/new')}>Add station</Button>}>
    {resource.data && <Metrics items={[
      { label: 'Stations in view', value: resource.data.length },
      { label: 'Operational', value: resource.data.filter(station => station.status === 'OPERATIONAL').length },
      { label: 'Empty slots', value: resource.data.every(station => Number.isFinite(station.emptySlots)) ? resource.data.reduce((total, station) => total + station.emptySlots, 0) : '—' },
    ]} />}
    <section className="operations-table">
      <Form className="operations-filters" layout="vertical" onFinish={values => setFilters({ search: values.search?.trim() || undefined, status: values.status })}>
        <Form.Item name="search" label="Search"><Input placeholder="Station name or code" allowClear /></Form.Item>
        <Form.Item name="status" label="Status"><Select allowClear placeholder="All statuses" options={stationStatuses.map(value => ({ value, label: value }))} /></Form.Item>
        <Button type="primary" htmlType="submit">Apply filters</Button><Button onClick={resource.reload}>Refresh</Button>
      </Form>
      <ResourceState loading={resource.loading} error={resource.error} retry={resource.reload} />
      {resource.data && <Table<Station> rowKey="id" dataSource={resource.data} pagination={false} scroll={{ x: 780 }} columns={[
        { title: 'Station', dataIndex: 'name', render: (name, station) => <Link to={'/stations/' + station.id}>{name}<small className="operations-small">{station.stationCode}</small></Link> },
        { title: 'Address', dataIndex: 'address' },
        { title: 'Slots / empty', render: (_, station) => <>{station.totalSlots ?? '—'} / {station.emptySlots ?? '—'}</> },
        { title: 'Status', dataIndex: 'status', render: status => <StatusTag status={status} /> },
        { title: 'Action', render: (_, station) => <Link to={'/stations/' + station.id}>View station →</Link> },
      ]} />}
    </section>
  </OperationsPage>
}
