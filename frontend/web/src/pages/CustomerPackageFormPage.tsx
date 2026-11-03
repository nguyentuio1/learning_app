import { useEffect, useState } from 'react'
import { Alert, Button, Card, Checkbox, Form, Input, InputNumber, Space, Spin, Typography, Upload } from 'antd'
import type { UploadFile } from 'antd'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { customerPackageApi } from '../services/customerPackageApi'
import type { PackageValues } from '../types/customerPackage'
import { customerPackageIssue } from '../utils/customerPackageIssue'
import './customer-packages.css'

const { Title, Text } = Typography
const empty: PackageValues = { packageName: '', weightGram: 0, lengthCm: 0, widthCm: 0, heightCm: 0, isFragile: false }

export function CustomerPackageFormPage() {
  const { id } = useParams()
  const editing = Boolean(id)
  const navigate = useNavigate()
  const [form] = Form.useForm<PackageValues>()
  const [loading, setLoading] = useState(editing)
  const [saving, setSaving] = useState(false)
  const [issue, setIssue] = useState('')
  const [fileList, setFileList] = useState<UploadFile[]>([])
  useEffect(() => { if (!id) return; void (async () => { try { form.setFieldsValue(await customerPackageApi.getPackage(id)) } catch (error) { setIssue(customerPackageIssue(error, 'Unable to load this package.')) } finally { setLoading(false) } })() }, [form, id])
  async function submit(values: PackageValues) {
    setSaving(true); setIssue('')
    try { const saved = id ? await customerPackageApi.updatePackage(id, values) : await customerPackageApi.createPackage(values); const file = fileList[0]?.originFileObj; if (file) await customerPackageApi.uploadPackageImage(saved.id, file); navigate('/packages') } catch (error) { setIssue(customerPackageIssue(error, 'Unable to save this package.')) } finally { setSaving(false) }
  }
  return <main className="customer-page customer-form-page"><header className="customer-heading"><div><span className="customer-eyebrow">PACKAGE DETAILS</span><Title level={1}>{editing ? 'Edit package' : 'Add package'}</Title><Text type="secondary">Enter dimensions and weight accurately so a suitable drone can be assigned.</Text></div></header>
    <Card className="customer-card customer-form-card">{loading ? <Spin tip="Loading package…" /> : <Form form={form} layout="vertical" initialValues={empty} onFinish={submit}>
      {issue && <Alert className="customer-alert" type="error" showIcon message={issue} />}
      <div className="customer-form-grid"><Form.Item label="Package name" name="packageName" rules={[{ required: true, message: 'Enter a package name.' }, { max: 200 }]}><Input placeholder="e.g. Documents" /></Form.Item><Form.Item label="Weight (g)" name="weightGram" rules={[{ required: true, type: 'number', min: 1, message: 'Enter a weight above 0.' }]}><InputNumber className="customer-number" min={1} /></Form.Item><Form.Item label="Length (cm)" name="lengthCm" rules={[{ required: true, type: 'number', min: 0.1, message: 'Enter a length above 0.' }]}><InputNumber className="customer-number" min={0.1} /></Form.Item><Form.Item label="Width (cm)" name="widthCm" rules={[{ required: true, type: 'number', min: 0.1, message: 'Enter a width above 0.' }]}><InputNumber className="customer-number" min={0.1} /></Form.Item><Form.Item label="Height (cm)" name="heightCm" rules={[{ required: true, type: 'number', min: 0.1, message: 'Enter a height above 0.' }]}><InputNumber className="customer-number" min={0.1} /></Form.Item></div>
      <Form.Item name="isFragile" valuePropName="checked"><Checkbox>Handle this package as fragile</Checkbox></Form.Item><Form.Item label="Package photo (optional)" extra="JPEG or PNG, maximum 5 MB."><Upload.Dragger accept="image/jpeg,image/png" maxCount={1} beforeUpload={(file) => { if (file.size > 5 * 1024 * 1024) { setIssue('Choose a JPEG or PNG image no larger than 5 MB.'); return Upload.LIST_IGNORE } return false }} fileList={fileList} onChange={({ fileList: next }) => setFileList(next)} onRemove={() => { setFileList([]); return true }}><p className="ant-upload-drag-icon">↑</p><p className="ant-upload-text">Drop a photo here or choose a file</p><p className="ant-upload-hint">It is uploaded only after the package is saved.</p></Upload.Dragger></Form.Item>
      <Space wrap><Button type="primary" htmlType="submit" loading={saving}>{editing ? 'Save changes' : 'Add package'}</Button><Link to="/packages"><Button>Cancel</Button></Link></Space>
    </Form>}</Card>
  </main>
}
