import type { ReactNode } from 'react'
import { Alert, Button, ConfigProvider, Empty, Spin, Tag } from 'antd'
import { operationError } from './feedback'
import './operations.css'

export function OperationsPage({ title, subtitle, action, children }: { title: string; subtitle: string; action?: ReactNode; children: ReactNode }) {
  return <ConfigProvider theme={{ token: { colorPrimary: '#1e4f3c', colorText: '#203b30', colorBorder: '#cbd9c4', borderRadius: 8, controlHeight: 42 },
    components: { Table: { headerBg: '#e3eddc', rowHoverBg: '#f1f6eb', cellPaddingBlock: 18 }, Modal: { contentBg: '#fffffc' } } }}>
    <main className="operations-page">
      <header className="operations-heading"><div><h1>{title}</h1><p>{subtitle}</p></div>{action}</header>
      {children}
    </main>
  </ConfigProvider>
}

export function ResourceState({ loading, error, retry, empty = false }: { loading: boolean; error?: unknown; retry: () => void; empty?: boolean }) {
  if (loading) return <div className="operations-loading" role="status"><Spin /><span>Loading records…</span></div>
  if (error) return <Alert type="error" showIcon title={operationError(error)} action={<Button onClick={retry}>Try again</Button>} />
  if (empty) return <Empty description="No records match this view." />
  return null
}

const colors: Record<string, string> = { OPERATIONAL: 'green', IDLE: 'green', EMPTY: 'green', OCCUPIED: 'cyan', RESERVED: 'gold', ASSIGNED: 'gold', FLYING: 'cyan', CHARGING: 'blue', MAINTENANCE: 'orange', OFFLINE: 'default', ERROR: 'red' }
export function StatusTag({ status }: { status: string }) {
  return <Tag color={colors[status]}>{status.replaceAll('_', ' ')}</Tag>
}

export function Metrics({ items }: { items: { label: string; value: ReactNode }[] }) {
  return <dl className="operations-metrics">{items.map(item => <div key={item.label}><dt>{item.label}</dt><dd>{item.value}</dd></div>)}</dl>
}
