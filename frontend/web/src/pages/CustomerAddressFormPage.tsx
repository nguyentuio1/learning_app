import { useEffect, useState } from 'react'
import { Alert, Button, Card, Checkbox, Form, Input, InputNumber, Space, Spin, Typography } from 'antd'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { customerAddressApi } from '../services/customerAddressApi'
import type { AddressValues } from '../types/customerAddress'
import { customerAddressIssue } from '../utils/customerAddressIssue'
import './customer-addresses.css'

const { Title, Text } = Typography
const empty: AddressValues = { label: '', contactName: '', contactPhone: '', addressLine: '', isDefault: false }

export function CustomerAddressFormPage() {
  const { id } = useParams()
  const editing = Boolean(id)
  const navigate = useNavigate()
  const [form] = Form.useForm<AddressValues>()
  const [loading, setLoading] = useState(editing)
  const [saving, setSaving] = useState(false)
  const [issue, setIssue] = useState('')
  useEffect(() => { if (!id) return; void (async () => { try { const address = await customerAddressApi.getAddress(id); form.setFieldsValue({ ...address, latitude: address.latitude ?? undefined, longitude: address.longitude ?? undefined }) } catch (error) { setIssue(customerAddressIssue(error, 'Unable to load this address.')) } finally { setLoading(false) } })() }, [form, id])
  async function submit(values: AddressValues) {
    setSaving(true); setIssue('')
    try { if (id) await customerAddressApi.updateAddress(id, values); else await customerAddressApi.createAddress(values); navigate('/addresses') } catch (error) { setIssue(customerAddressIssue(error, 'Unable to save this address.')) } finally { setSaving(false) }
  }
  return <main className="customer-page customer-form-page"><header className="customer-heading"><div><span className="customer-eyebrow">DELIVERY DETAILS</span><Title level={1}>{editing ? 'Edit address' : 'Add address'}</Title><Text type="secondary">Use a clear label so you can choose this address quickly when creating an order.</Text></div></header>
    <Card className="customer-card customer-form-card">{loading ? <Spin tip="Loading address…" /> : <Form form={form} layout="vertical" initialValues={empty} onFinish={submit}>
      {issue && <Alert className="customer-alert" type="error" showIcon message={issue} />}
      <div className="customer-form-grid"><Form.Item label="Label" name="label" rules={[{ required: true, message: 'Enter a label.' }, { max: 50 }]}><Input placeholder="Home, Office…" /></Form.Item><Form.Item label="Contact name" name="contactName" rules={[{ required: true, message: 'Enter a contact name.' }, { max: 150 }]}><Input /></Form.Item><Form.Item label="Contact phone" name="contactPhone" rules={[{ required: true, message: 'Enter a phone number.' }, { pattern: /^\d{9,11}$/, message: 'Use 9 to 11 digits.' }]}><Input inputMode="numeric" /></Form.Item><Form.Item label="Address" name="addressLine" rules={[{ required: true, message: 'Enter the delivery address.' }]}><Input.TextArea rows={3} /></Form.Item><Form.Item label="Latitude (optional)" name="latitude" rules={[{ type: 'number', min: -90, max: 90, message: 'Use a value from -90 to 90.' }]}><InputNumber className="customer-number" /></Form.Item><Form.Item label="Longitude (optional)" name="longitude" rules={[{ type: 'number', min: -180, max: 180, message: 'Use a value from -180 to 180.' }]}><InputNumber className="customer-number" /></Form.Item></div>
      <Form.Item name="isDefault" valuePropName="checked"><Checkbox>Set as my default delivery address</Checkbox></Form.Item><Space wrap><Button type="primary" htmlType="submit" loading={saving}>{editing ? 'Save changes' : 'Add address'}</Button><Link to="/addresses"><Button>Cancel</Button></Link></Space>
    </Form>}</Card>
  </main>
}
