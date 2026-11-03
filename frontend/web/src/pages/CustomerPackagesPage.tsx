import { useRef, useState } from 'react'
import { Alert, Button, Card, Empty, Image, Popconfirm, Space, Spin, Tag, Typography } from 'antd'
import { Link } from 'react-router-dom'
import { useCustomerPackages } from '../hooks/useCustomerPackages'
import { customerPackageApi } from '../services/customerPackageApi'
import { customerPackageIssue } from '../utils/customerPackageIssue'
import './customer-packages.css'

const { Title, Text } = Typography

export function CustomerPackagesPage() {
  const { data: packages, loading, issue, reload } = useCustomerPackages()
  const [actionIssue, setActionIssue] = useState('')
  const [busyId, setBusyId] = useState<string | null>(null)
  const actionPending = useRef(false)
  async function runAction(id: string, action: () => Promise<unknown>, fallback: string) {
    if (actionPending.current) return
    actionPending.current = true
    setBusyId(id); setActionIssue('')
    try { await action(); await reload() }
    catch (error) { setActionIssue(customerPackageIssue(error, fallback)) }
    finally { actionPending.current = false; setBusyId(null) }
  }
  function remove(id: string) {
    return runAction(id, () => customerPackageApi.deletePackage(id), 'Unable to delete this package.')
  }
  return <main className="customer-page">
    <header className="customer-heading"><div><span className="customer-eyebrow">PACKAGE DETAILS</span><Title level={1}>Packages</Title><Text type="secondary">Save package details so they are ready when you create a delivery order.</Text></div><Link className="customer-primary-link" to="/packages/new">Add package</Link></header>
    {actionIssue && <Alert className="customer-alert" type="error" showIcon title={actionIssue} />}
    {issue ? <Card className="customer-card"><Empty description={issue}><Button onClick={() => void reload()}>Try again</Button></Empty></Card> : loading ? <Card className="customer-card customer-loading"><Spin tip="Loading packages…" /></Card> : packages.length === 0 ? <Card className="customer-card"><Empty description="You have not added a package yet."><Link className="customer-primary-link" to="/packages/new">Add your first package</Link></Empty></Card> : <section className="customer-package-grid">
      {packages.map(item => <Card key={item.id} className="customer-card customer-package-card" cover={item.packageImageUrl ? <Image preview={false} alt="" src={item.packageImageUrl} /> : undefined} title={<Space>{item.packageName}{item.isFragile && <Tag color="gold">Fragile</Tag>}</Space>} extra={<Link className="customer-edit-link" to={`/packages/${item.id}`}>Edit →</Link>}>
        <p className="customer-package-meta">{item.weightGram.toLocaleString()} g · {item.lengthCm} × {item.widthCm} × {item.heightCm} cm</p><div className="customer-card-actions"><Popconfirm title="Delete this package?" description="It can no longer be selected for new orders." okText="Delete" okButtonProps={{ danger: true }} disabled={busyId !== null} onConfirm={() => remove(item.id)}><Button danger size="small" disabled={busyId !== null} loading={busyId === item.id}>Delete</Button></Popconfirm></div>
      </Card>)}
    </section>}
  </main>
}
