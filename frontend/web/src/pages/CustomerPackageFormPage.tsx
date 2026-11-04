import { useEffect, useRef, useState } from 'react'
import { Alert, Button, Card, Checkbox, ConfigProvider, Form, Image, Input, InputNumber, Space, Spin, Typography, Upload } from 'antd'
import type { UploadFile } from 'antd'
import { useNavigate, useParams } from 'react-router-dom'
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
  const savedId = useRef(id)
  const [imageUrl, setImageUrl] = useState<string | null>(null)
  const submitting = useRef(false)
  const [loadFailed, setLoadFailed] = useState(false)
  const [uploadPending, setUploadPending] = useState(false)
  useEffect(() => {
    if (!id) return
    let active = true
    void customerPackageApi.getPackage(id).then(item => {
      if (!active) return
      form.setFieldsValue(item)
      setImageUrl(item.packageImageUrl)
    }).catch(error => {
      if (!active) return
      setLoadFailed(true)
      setIssue(customerPackageIssue(error, 'Unable to load this package.'))
    }).finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [form, id])
  async function submit(values: PackageValues) {
    if (submitting.current || loadFailed) return
    submitting.current = true
    setSaving(true); setIssue('')
    try {
      if (!uploadPending) {
        const payload = { ...values, packageImageUrl: imageUrl }
        const saved = savedId.current
          ? await customerPackageApi.updatePackage(savedId.current, payload)
          : await customerPackageApi.createPackage(payload)
        savedId.current = saved.id
        setImageUrl(saved.packageImageUrl)
      }
      const file = fileList[0]?.originFileObj
      if (file && savedId.current) {
        setUploadPending(true)
        try { await customerPackageApi.uploadPackageImage(savedId.current, file) }
        catch (error) {
          setIssue(`Package details were saved. ${customerPackageIssue(error, 'Photo upload failed.')} Retry the photo upload or continue without it.`)
          return
        }
      }
      navigate('/packages')
    } catch (error) { setIssue(customerPackageIssue(error, 'Unable to save this package.')) }
    finally { submitting.current = false; setSaving(false) }
  }
  return <main className="customer-page customer-form-page"><header className="customer-heading"><div><span className="customer-eyebrow">PACKAGE DETAILS</span><Title level={1}>{editing ? 'Edit package' : 'Add package'}</Title><Text type="secondary">Enter dimensions and weight accurately so a suitable drone can be assigned.</Text></div></header>
    <Card className="customer-card customer-form-card">{loading ? <Spin tip="Loading package…" /> : <Form form={form} layout="vertical" initialValues={empty} onFinish={submit}>
      {issue && <Alert className="customer-alert" type="error" showIcon title={issue} />}
      <ConfigProvider componentDisabled={saving || uploadPending || loadFailed}>
      <fieldset disabled={saving || uploadPending || loadFailed} className="customer-fields">
      {imageUrl && <div className="customer-current-photo"><Image src={imageUrl} alt="Current package photo" width={120} preview={false} /><Text type="secondary">Your current photo is kept unless a new upload succeeds.</Text></div>}
      <div className="customer-form-grid"><Form.Item label="Package name" name="packageName" rules={[{ required: true, message: 'Enter a package name.' }, { max: 200 }, { whitespace: true, message: 'Enter a package name.' }]}><Input placeholder="e.g. Documents" /></Form.Item><Form.Item label="Weight (g)" name="weightGram" rules={[{ required: true, type: 'number', min: 0.01, max: 3000, message: 'Use 0.01 to 3000 grams.' }]}><InputNumber className="customer-number" step={0.01} precision={2} /></Form.Item><Form.Item label="Length (cm)" name="lengthCm" rules={[{ required: true, type: 'number', min: 0.01, max: 30, message: 'Use 0.01 to 30 cm.' }]}><InputNumber className="customer-number" step={0.01} precision={2} /></Form.Item><Form.Item label="Width (cm)" name="widthCm" rules={[{ required: true, type: 'number', min: 0.01, max: 20, message: 'Use 0.01 to 20 cm.' }]}><InputNumber className="customer-number" step={0.01} precision={2} /></Form.Item><Form.Item label="Height (cm)" name="heightCm" rules={[{ required: true, type: 'number', min: 0.01, max: 15, message: 'Use 0.01 to 15 cm.' }]}><InputNumber className="customer-number" step={0.01} precision={2} /></Form.Item></div>
      <Form.Item name="isFragile" valuePropName="checked"><Checkbox>Handle this package as fragile</Checkbox></Form.Item><Form.Item label="Package photo (optional)" extra="JPEG or PNG, maximum 5 MB."><Upload.Dragger accept="image/jpeg,image/png" maxCount={1} beforeUpload={(file) => { if (!['image/jpeg', 'image/png'].includes(file.type) || file.size === 0 || file.size > 5 * 1024 * 1024) { setIssue('Choose a JPEG or PNG image no larger than 5 MB.'); return Upload.LIST_IGNORE } return false }} fileList={fileList} onChange={({ fileList: next }) => setFileList(next)} onRemove={() => { setFileList([]); return true }}><p className="ant-upload-drag-icon">↑</p><p className="ant-upload-text">Drop a photo here or choose a file</p><p className="ant-upload-hint">It is uploaded only after the package is saved.</p></Upload.Dragger></Form.Item>
      </fieldset>
      </ConfigProvider>
      <Space wrap><Button type="primary" htmlType="submit" disabled={loadFailed} loading={saving}>{uploadPending ? 'Retry photo upload' : editing ? 'Save changes' : 'Add package'}</Button><Button disabled={saving} onClick={() => navigate('/packages')}>{uploadPending ? 'Continue without new photo' : 'Cancel'}</Button></Space>
    </Form>}</Card>
  </main>
}
