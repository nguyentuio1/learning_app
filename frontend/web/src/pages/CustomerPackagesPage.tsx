import { Button, Card, Empty, Image, Popconfirm, Space, Spin, Tag, Typography } from 'antd'
import { Link } from 'react-router-dom'
import { useCustomerPackages } from '../hooks/useCustomerPackages'
import { customerPackageApi } from '../services/customerPackageApi'
import { customerPackageIssue } from '../utils/customerPackageIssue'
import './customer-packages.css'

const { Title, Text } = Typography

export function CustomerPackagesPage() {
  const { data: packages, loading, issue, reload } = useCustomerPackages()
  async function remove(id: string) { try { await customerPackageApi.deletePackage(id); await reload() } catch (error) { window.alert(customerPackageIssue(error, 'Unable to delete this package.')) } }
  return <main className="customer-page">
    <header className="customer-heading"><div><span className="customer-eyebrow">PACKAGE DETAILS</span><Title level={1}>Packages</Title><Text type="secondary">Save package details so they are ready when you create a delivery order.</Text></div><Link className="customer-primary-link" to="/packages/new">Add package</Link></header>
    {issue ? <Card className="customer-card"><Empty description={issue}><Button onClick={() => void reload()}>Try again</Button></Empty></Card> : loading ? <Card className="customer-card customer-loading"><Spin tip="Loading packages…" /></Card> : packages.length === 0 ? <Card className="customer-card"><Empty description="You have not added a package yet."><Link className="customer-primary-link" to="/packages/new">Add your first package</Link></Empty></Card> : <section className="customer-package-grid">
      {packages.map(item => <Card key={item.id} className="customer-card customer-package-card" cover={item.packageImageUrl ? <Image preview={false} alt="" src={item.packageImageUrl} /> : undefined} title={<Space>{item.packageName}{item.isFragile && <Tag color="gold">Fragile</Tag>}</Space>} extra={<Link className="customer-edit-link" to={`/packages/${item.id}`}>Edit →</Link>}>
        <p className="customer-package-meta">{item.weightGram.toLocaleString()} g · {item.lengthCm} × {item.widthCm} × {item.heightCm} cm</p><div className="customer-card-actions"><Popconfirm title="Delete this package?" description="It can no longer be selected for new orders." okText="Delete" okButtonProps={{ danger: true }} onConfirm={() => void remove(item.id)}><Button danger size="small">Delete</Button></Popconfirm></div>
      </Card>)}
    </section>}
  </main>
}
