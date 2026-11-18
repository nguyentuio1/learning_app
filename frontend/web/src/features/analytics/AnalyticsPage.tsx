import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { Alert, Button, ConfigProvider, Form, Table, Tabs, Tag, type ThemeConfig } from 'antd'
import dayjs from 'dayjs'
import { CalendarDayPicker } from '../../components/CalendarPicker'
import { useAuth } from '../../hooks/useAuth'
import { OperationsPage, ResourceState } from '../operations/OperationsUi'
import { useResource } from '../operations/useResource'
import { validationFields } from '../operations/feedback'
import { analyticsApi, type DashboardResult, type PeriodQuery, type ReportingPeriod, type ReportRow } from './analyticsApi'
import type { InsightRouteState } from '../operations/deliveryInsight'
import { presetPeriod, reportingPeriodError, reportingPresets, reportingStartError, reportingTimeZone, type PresetDays } from './reportingPeriod'
import './analytics.css'

const lastReportingDate = dayjs('9999-12-30')
const analyticsTheme: ThemeConfig = {
  token: { colorBgElevated: '#fffffc', colorBgContainer: '#fffffc', colorLink: '#1e4f3c' },
  components: {
    Pagination: { itemActiveBg: '#e4eddb' },
  },
}

const number = (value: number | null) => value === null ? 'N/A' : value.toFixed(2)
function time(value: string | null) {
  return value && Number.isFinite(Date.parse(value)) ? new Intl.DateTimeFormat('en-GB', { timeZone: reportingTimeZone, dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value)) : 'Not available'
}
function Status({ value }: { value: string }) {
  const colors: Record<string, string> = { DELIVERED: 'green', FAILED: 'red', CANCELLED: 'default', IN_TRANSIT: 'cyan', ARRIVED: 'blue', PENDING: 'gold', APPROVED: 'gold', READY_FOR_TAKEOFF: 'orange' }
  return <Tag color={colors[value]}>{value.replaceAll('_', ' ')}</Tag>
}
function PeriodStamp({ period, asOf, definition }: { period: ReportingPeriod; asOf: string; definition: string }) {
  return <div className="analytics-period"><strong>{period.fromDate} → {period.toDate}</strong><span>{period.timeZone} · Created-date cohort</span><span>Snapshot: {time(asOf)} (UTC+07)</span><small>Definition: {definition}</small></div>
}

export function AnalyticsPage() {
  const { user } = useAuth()
  const [form] = Form.useForm<PeriodQuery>()
  const [params, setParams] = useSearchParams()
  const [defaultPeriod] = useState(() => presetPeriod(30))
  const [revision, setRevision] = useState(0)
  const fromDate = params.get('fromDate')
  const toDate = params.get('toDate')
  const customPeriod = fromDate && toDate && !reportingPeriodError(fromDate, toDate)
  const applied = useMemo(() => customPeriod ? { fromDate, toDate } : defaultPeriod, [customPeriod, defaultPeriod, fromDate, toDate])
  const activePreset = customPeriod ? Number(params.get('preset')) : 30
  const tab = params.get('tab') === 'reports' ? 'reports' : 'dashboard'
  const handleValidation = useCallback((error: unknown) => form.setFields(validationFields(error, ['fromDate', 'toDate'] as const)), [form])

  useEffect(() => form.setFieldsValue(applied), [applied, form])

  function apply(period: PeriodQuery, days?: PresetDays) {
    const next = new URLSearchParams({ fromDate: period.fromDate, toDate: period.toDate })
    if (days) next.set('preset', String(days))
    if (tab === 'reports') next.set('tab', tab)
    setParams(next)
    setRevision(value => value + 1)
  }

  function selectTab(key: string) {
    const next = new URLSearchParams(params)
    if (key === 'reports') next.set('tab', key)
    else next.delete('tab')
    setParams(next)
  }

  return <div className="analytics-feature"><OperationsPage title="Dashboard & reports" subtitle="Review delivery outcomes for a reporting period.">
    <ConfigProvider theme={analyticsTheme}>
      <Form form={form} layout="vertical" className="analytics-filter" initialValues={applied} onFinish={period => apply(period)}>
        <div className="analytics-presets" role="group" aria-label="Reporting period presets">
          {reportingPresets.map(({ days, label }) => (
            <Button key={days} htmlType="button" type={activePreset === days ? 'primary' : 'default'}
              aria-pressed={activePreset === days} onClick={() => apply(presetPeriod(days), days)}>
              {label}
            </Button>
          ))}
        </div>
        <Form.Item
          name="fromDate"
          label="From date"
          rules={[
            { required: true, message: 'Choose a start date.' },
            { validator: (_, value) => {
              const error = value ? reportingStartError(value) : undefined
              return error ? Promise.reject(new Error(error)) : Promise.resolve()
            } },
          ]}
        >
          <CalendarDayPicker maxDate={lastReportingDate} placeholder="Start date" />
        </Form.Item>
        <Form.Item
          name="toDate"
          label="To date"
          dependencies={['fromDate']}
          rules={[
            { required: true, message: 'Choose an end date.' },
            { validator: (_, value) => {
              const error = value ? reportingPeriodError(form.getFieldValue('fromDate'), value) : undefined
              return error ? Promise.reject(new Error(error)) : Promise.resolve()
            } },
          ]}
        >
          <CalendarDayPicker maxDate={lastReportingDate} placeholder="End date" />
        </Form.Item>
        <Button type="primary" htmlType="submit">Apply period</Button>
        <p>
          <strong>{customPeriod ? 'Applied period' : 'Default period · Last 30 days'}</strong>
          {' · '}{applied.fromDate} → {applied.toDate}
          <br />Inclusive calendar days | {reportingTimeZone} (UTC+07) | Up to 366 days
        </p>
      </Form>
      <Tabs activeKey={tab} onChange={selectTab} items={[{ key: 'dashboard', label: 'Dashboard' }, { key: 'reports', label: 'Delivery report' }]} />
      {tab === 'dashboard' ? (
        <Dashboard key={`${user?.id}:${revision}`} period={applied} onValidation={handleValidation} />
      ) : (
        <Report key={`${user?.id}:${revision}`} period={applied} onValidation={handleValidation} />
      )}
    </ConfigProvider>
  </OperationsPage></div>
}
function Dashboard({ period, onValidation }: { period: PeriodQuery; onValidation: (error: unknown) => void }) {
  const load = useCallback(async (signal: AbortSignal) => {
    try { return await analyticsApi.dashboard(period, signal) }
    catch (error) { if (!signal.aborted) onValidation(error); throw error }
  }, [period, onValidation])
  const resource = useResource(load)
  const result = resource.data
  return <>
    <div className="analytics-refresh"><Button onClick={resource.reload} loading={resource.loading}>Refresh dashboard</Button></div>
    <ResourceState loading={resource.loading} error={resource.error} retry={resource.reload}/>
    {result && <>
      <PeriodStamp period={result.period} asOf={result.asOf} definition={result.metricDefinition}/>
      {result.totalOrders === 0 && <Alert showIcon type="info" title="No orders in this reporting period" description="Counts are zero. Success rate and average delivery time are unavailable until eligible samples exist."/>}
      <div className="analytics-kpis"><Metric label="Total orders" value={String(result.totalOrders)} note="Distinct orders created in the applied period"/><Metric label="Success rate" value={result.successRate.percent === null ? 'N/A' : number(result.successRate.percent) + '%'} note={`${result.successRate.deliveredCount} delivered / ${result.successRate.eligibleCount} delivered or failed`}/><Metric label="Average delivery time" value={result.averageDelivery.minutes === null ? 'N/A' : number(result.averageDelivery.minutes) + ' min'} note={`${result.averageDelivery.sampleCount} valid samples · ${result.averageDelivery.excludedCount} excluded`}/></div>
      <div className="analytics-grid"><section className="analytics-card"><h2>Orders by status</h2><p>Current status at this snapshot</p><div className="analytics-bars">{result.ordersByStatus.map(row => <div key={row.status}><span>{row.status.replaceAll('_', ' ')}</span><div className="analytics-bar-track" aria-hidden="true"><i style={{ width: `${result.totalOrders ? row.count / result.totalOrders * 100 : 0}%` }}/></div><strong>{row.count}</strong></div>)}</div></section><section className="analytics-card"><h2>How to read these figures</h2><p>Success rate compares delivered orders with delivered and failed orders. Cancelled and unfinished orders are excluded.</p><p>Delivery duration runs from actual departure to recipient confirmation, using delivered orders with valid timestamps.</p><dl><div><dt>Generated</dt><dd>{time(result.generatedAt)} (UTC+07)</dd></div></dl><p className="analytics-muted">These figures describe current states of orders created in the selected period, rather than historical states at the period end.</p></section></div>
      <StationThroughput result={result}/>
    </>}
  </>
}
function Metric({ label, value, note }: { label: string; value: string; note: string }) {
  return <section className="analytics-card analytics-kpi"><span>{label}</span><strong>{value}</strong><small>{note}</small></section>
}
function StationThroughput({ result }: { result: DashboardResult }) {
  return <section className="analytics-card"><h2>Station throughput</h2><p>Distinct orders associated with each station. This measures traffic, not occupied-slot utilization.</p><Table rowKey="stationId" dataSource={result.stationUsage} pagination={false} scroll={{ x: 600 }} locale={{ emptyText: 'No station throughput in this period.' }} columns={[
    { title: 'Station', key: 'station', render: (_, row) => <><strong>{row.stationName}</strong><small className="analytics-muted block">{row.stationCode}</small></> },
    { title: 'Origin orders', dataIndex: 'originOrderCount' }, { title: 'Destination orders', dataIndex: 'destinationOrderCount' }, { title: 'Distinct orders', dataIndex: 'totalDistinctOrders' },
  ]}/><p className="analytics-muted">An order may count at both its origin and destination. Totals across stations need not equal the overall order count.</p></section>
}
function Report({ period, onValidation }: { period: PeriodQuery; onValidation: (error: unknown) => void }) {
  const [params, setParams] = useSearchParams()
  const requestedPage = Number(params.get('page'))
  const requestedSize = Number(params.get('pageSize'))
  const page = Number.isInteger(requestedPage) && requestedPage >= 1 && requestedPage <= 2_147_483_647 ? requestedPage : 1
  const pageSize = [10, 20, 50, 100].includes(requestedSize) ? requestedSize : 20

  function paginate(nextPage: number, nextSize: number) {
    const next = new URLSearchParams(params)
    next.set('page', String(nextSize === pageSize ? nextPage : 1))
    next.set('pageSize', String(nextSize))
    setParams(next)
  }

  function insightContext(row: ReportRow): InsightRouteState {
    const search = new URLSearchParams(params)
    search.set('fromDate', period.fromDate)
    search.set('toDate', period.toDate)
    return { trackingNumber: row.trackingNumber, reportSearch: '?' + search.toString() }
  }
  const load = useCallback(async (signal: AbortSignal) => {
    try { return await analyticsApi.report(period, page, pageSize, signal) }
    catch (error) { if (!signal.aborted) onValidation(error); throw error }
  }, [period, page, pageSize, onValidation])
  const resource = useResource(load), result = resource.data
  return <>
    <div className="analytics-refresh"><Button onClick={resource.reload} loading={resource.loading}>Refresh report</Button></div>
    <ResourceState loading={resource.loading} error={resource.error} retry={resource.reload}/>
    {result && <><PeriodStamp period={result.period} asOf={result.asOf} definition={result.metricDefinition}/><section className="analytics-card"><h2>Delivery report</h2><p>{result.totalCount} orders · newest created orders first</p><Table<ReportRow> rowKey="orderId" dataSource={result.items} scroll={{ x: 1120 }} locale={{ emptyText: <span className="analytics-empty">No orders in this reporting period.</span> }}
      pagination={{ current: result.page, pageSize: result.pageSize, total: result.totalCount, showSizeChanger: true, pageSizeOptions: [10, 20, 50, 100], onChange: paginate }}
      columns={[
        { title: 'Tracking number', key: 'trackingNumber', render: (_, row) => {
          const state = insightContext(row)
          return <>
            <strong>{row.trackingNumber}</strong>
            <div className="analytics-insight-links">
              <Link className="insight-link" aria-label={`View ETA for ${row.trackingNumber}`} to={`/delivery-eta/${row.orderId}`} state={state}>ETA</Link>
              <Link className="insight-link" aria-label={`View summary for ${row.trackingNumber}`} to={`/delivery-summary/${row.orderId}`} state={state}>Summary</Link>
            </div>
          </>
        } },
        { title: 'Status', dataIndex: 'status', render: value => <Status value={value}/> },
        { title: 'Route', key: 'route', render: (_, row) => <>{row.originStation.name}<span className="analytics-muted block">→ {row.destinationStation.name}</span></> },
        { title: 'Created (UTC+07)', dataIndex: 'createdAt', render: time }, { title: 'Departed (UTC+07)', dataIndex: 'actualDepartureTime', render: time },
        { title: 'Delivered (UTC+07)', dataIndex: 'actualDeliveryTime', render: time }, { title: 'Duration', dataIndex: 'durationMinutes', render: value => value === null ? 'N/A' : `${number(value)} min` },
      ]}/></section><p className="analytics-muted">Report rows and dashboard figures are separate read snapshots and may reflect later changes.</p></>}
  </>
}
