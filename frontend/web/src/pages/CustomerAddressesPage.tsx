import { useRef, useState } from 'react'
import { Alert, Button, Card, Empty, Popconfirm, Space, Spin, Tag, Typography } from 'antd'
import { Link } from 'react-router-dom'
import { useCustomerAddresses } from '../hooks/useCustomerAddresses'
import { customerAddressApi } from '../services/customerAddressApi'
import { customerAddressIssue } from '../utils/customerAddressIssue'
import './customer-addresses.css'

const { Title, Text } = Typography

export function CustomerAddressesPage() {
  const { data: addresses, loading, issue, reload } = useCustomerAddresses()
  const [actionIssue, setActionIssue] = useState('')
  const [busyId, setBusyId] = useState<string | null>(null)
  const actionPending = useRef(false)
  async function runAction(id: string, action: () => Promise<unknown>, fallback: string) {
    if (actionPending.current) return
    actionPending.current = true
    setBusyId(id); setActionIssue('')
    try { await action(); await reload() }
    catch (error) { setActionIssue(customerAddressIssue(error, fallback)) }
    finally { actionPending.current = false; setBusyId(null) }
  }
  function setDefault(id: string) {
    return runAction(id, () => customerAddressApi.setDefaultAddress(id), 'Unable to set the default address.')
  }
  function remove(id: string) {
    return runAction(id, () => customerAddressApi.deleteAddress(id), 'Unable to delete this address.')
  }
  return <main className="customer-page">
    <header className="customer-heading"><div><span className="customer-eyebrow">DELIVERY DETAILS</span><Title level={1}>Addresses</Title><Text type="secondary">Manage the delivery addresses you use for your orders.</Text></div><Link className="customer-primary-link" to="/addresses/new">Add address</Link></header>
    {actionIssue && <Alert className="customer-alert" type="error" showIcon title={actionIssue} />}
    {issue ? <Card className="customer-card"><Empty description={issue}><Button onClick={() => void reload()}>Try again</Button></Empty></Card> : loading ? <Card className="customer-card customer-loading"><Spin tip="Loading addresses…" /></Card> : addresses.length === 0 ? <Card className="customer-card"><Empty description="You have not added a delivery address yet."><Link className="customer-primary-link" to="/addresses/new">Add your first address</Link></Empty></Card> : <section className="customer-address-grid">
      {addresses.map(address => <Card key={address.id} className="customer-card customer-address-card" title={<Space>{address.label}{address.isDefault && <Tag color="green">Default</Tag>}</Space>} extra={<Link className="customer-edit-link" to={`/addresses/${address.id}`}>Edit →</Link>}>
        <p><strong>{address.contactName}</strong><br />{address.contactPhone}</p><p>{address.addressLine}</p>
        <div className="customer-card-actions"><Button size="small" disabled={address.isDefault || busyId !== null} loading={busyId === address.id} onClick={() => void setDefault(address.id)}>Set as default</Button><Popconfirm title="Delete this address?" description="It can no longer be selected for new orders." okText="Delete" okButtonProps={{ danger: true }} disabled={busyId !== null} onConfirm={() => remove(address.id)}><Button danger size="small" disabled={busyId !== null}>Delete</Button></Popconfirm></div>
      </Card>)}
    </section>}
  </main>
}
