import { Button, Card, Empty, Popconfirm, Space, Spin, Tag, Typography } from 'antd'
import { Link } from 'react-router-dom'
import { useCustomerAddresses } from '../hooks/useCustomerAddresses'
import { customerAddressApi } from '../services/customerAddressApi'
import { customerAddressIssue } from '../utils/customerAddressIssue'
import './customer-addresses.css'

const { Title, Text } = Typography

export function CustomerAddressesPage() {
  const { data: addresses, loading, issue, reload } = useCustomerAddresses()
  async function setDefault(id: string) {
    try { await customerAddressApi.setDefaultAddress(id); await reload() } catch (error) { window.alert(customerAddressIssue(error, 'Unable to set the default address.')) }
  }
  async function remove(id: string) {
    try { await customerAddressApi.deleteAddress(id); await reload() } catch (error) { window.alert(customerAddressIssue(error, 'Unable to delete this address.')) }
  }
  return <main className="customer-page">
    <header className="customer-heading"><div><span className="customer-eyebrow">DELIVERY DETAILS</span><Title level={1}>Addresses</Title><Text type="secondary">Manage the delivery addresses you use for your orders.</Text></div><Link className="customer-primary-link" to="/addresses/new">Add address</Link></header>
    {issue ? <Card className="customer-card"><Empty description={issue}><Button onClick={() => void reload()}>Try again</Button></Empty></Card> : loading ? <Card className="customer-card customer-loading"><Spin tip="Loading addresses…" /></Card> : addresses.length === 0 ? <Card className="customer-card"><Empty description="You have not added a delivery address yet."><Link className="customer-primary-link" to="/addresses/new">Add your first address</Link></Empty></Card> : <section className="customer-address-grid">
      {addresses.map(address => <Card key={address.id} className="customer-card customer-address-card" title={<Space>{address.label}{address.isDefault && <Tag color="green">Default</Tag>}</Space>} extra={<Link to={`/addresses/${address.id}`}>Edit</Link>}>
        <p><strong>{address.contactName}</strong><br />{address.contactPhone}</p><p>{address.addressLine}</p>
        <div className="customer-card-actions"><Button size="small" disabled={address.isDefault} onClick={() => void setDefault(address.id)}>Set as default</Button><Popconfirm title="Delete this address?" description="It can no longer be selected for new orders." okText="Delete" okButtonProps={{ danger: true }} onConfirm={() => void remove(address.id)}><Button danger size="small">Delete</Button></Popconfirm></div>
      </Card>)}
    </section>}
  </main>
}
