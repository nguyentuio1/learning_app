import { useEffect, useState, type ReactNode } from 'react'
import axios from 'axios'
import { Alert, Avatar, Button, Card, Form, Input, Typography, Upload } from 'antd'
import { profileApi } from '../../services/profileApi'
import type { ProfileValues, UserProfile } from '../../types/profile'
import { profileError, type ProfileError } from '../../utils/profileErrors'
import { useRequestController } from '../../hooks/useRequestController'
import './avatar-picker.css'

const { Text, Paragraph } = Typography
type ErrorHandler = (error: ProfileError) => void
type PasswordForm = { currentPassword: string; newPassword: string; confirmPassword: string }


function initials(name: string) {
  return name.trim().split(/\s+/).filter(Boolean).slice(0, 2).map(part => part[0]).join('').toUpperCase()
}

function Feedback({ error, success }: { error: string; success: string }) {
  return <div aria-live="polite" className="profile-feedback">
    {error && <Alert type="error" showIcon title={error} />}
    {success && <Alert type="success" showIcon title={success} />}
  </div>
}

export function ProfileDetails({ profile, onSaved, onAccessError, avatar }: {
  profile: UserProfile; onSaved: (profile: UserProfile) => void; onAccessError: ErrorHandler; avatar: ReactNode
}) {
  const [form] = Form.useForm<ProfileValues>()
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')
  const [success, setSuccess] = useState('')
  const startRequest = useRequestController()

  async function save(values: ProfileValues) {
    if (saving) return
    const request = startRequest()
    setSaving(true)
    setError('')
    setSuccess('')
    try {
      const updated = await profileApi.update(values, request.signal)
      if (request.signal.aborted) return
      onSaved(updated)
      form.setFieldsValue({ fullName: updated.fullName, phoneNumber: updated.phoneNumber })
      setSuccess('Profile saved.')
    } catch (cause) {
      if (request.signal.aborted || axios.isCancel(cause)) return
      const issue = profileError(cause, 'profile')
      if (issue.accessDenied) onAccessError(issue)
      else {
        form.setFields(['fullName', 'phoneNumber'].map(name => ({ name, errors: issue.fields[name] || [] })))
        setError(issue.message)
      }
    } finally {
      if (!request.signal.aborted) setSaving(false)
    }
  }

  return <Card title="Personal details">
    {avatar}
    <Form form={form} layout="vertical" requiredMark="optional" disabled={saving}
      initialValues={{ fullName: profile.fullName, phoneNumber: profile.phoneNumber }}
      onFinish={save} onValuesChange={() => { setError(''); setSuccess('') }} scrollToFirstError>
      <Form.Item name="fullName" label="Full name" rules={[
        { required: true, whitespace: true, message: 'Enter your full name.' },
        { max: 150, message: 'Full name must be at most 150 characters.' },
      ]}>
        <Input autoComplete="name" maxLength={150} />
      </Form.Item>
      <Form.Item name="phoneNumber" label="Phone number" rules={[
        { required: true, message: 'Enter your phone number.' },
        { pattern: /^\d{9,11}$/, message: 'Phone number must contain 9 to 11 digits.' },
      ]}>
        <Input type="tel" inputMode="numeric" autoComplete="tel" maxLength={11} />
      </Form.Item>
      <Feedback error={error} success={success} />
      <div className="profile-actions">
        <Button type="primary" htmlType="submit" loading={saving}>Save profile</Button>
        <Button htmlType="button" onClick={() => {
          form.setFieldsValue({ fullName: profile.fullName, phoneNumber: profile.phoneNumber })
          form.setFields([{ name: 'fullName', errors: [] }, { name: 'phoneNumber', errors: [] }])
          setError(''); setSuccess('')
        }}>Discard changes</Button>
      </div>
    </Form>
  </Card>
}

export function PasswordSettings({ onAccessError }: { onAccessError: ErrorHandler }) {
  const [form] = Form.useForm<PasswordForm>()
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')
  const [success, setSuccess] = useState('')
  const startRequest = useRequestController()

  async function save(values: PasswordForm) {
    if (saving) return
    const request = startRequest()
    setSaving(true); setError(''); setSuccess('')
    try {
      await profileApi.changePassword(values, request.signal)
      if (request.signal.aborted) return
      form.resetFields()
      setSuccess('Password changed. Sessions will require sign-in again when their current access tokens expire.')
    } catch (cause) {
      if (request.signal.aborted || axios.isCancel(cause)) return
      const issue = profileError(cause, 'password')
      if (issue.accessDenied) onAccessError(issue)
      else {
        form.setFields(['currentPassword', 'newPassword'].map(name => ({ name, errors: issue.fields[name] || [] })))
        setError(issue.message)
      }
    } finally {
      if (!request.signal.aborted) setSaving(false)
    }
  }

  return <Card title="Change password">
    <Paragraph type="secondary">Use 8–100 characters, including at least one letter and one number.</Paragraph>
    <Form form={form} layout="vertical" disabled={saving} requiredMark="optional" onFinish={save}
      onValuesChange={() => { setError(''); setSuccess('') }} scrollToFirstError>
      <Form.Item name="currentPassword" label="Current password" rules={[{ required: true, message: 'Enter your current password.' }]}>
        <Input.Password autoComplete="current-password" />
      </Form.Item>
      <div className="profile-password-grid"><Form.Item name="newPassword" label="New password" dependencies={['currentPassword']} rules={[
        { required: true, message: 'Enter a new password.' },
        { min: 8, max: 100, message: 'Password must be 8 to 100 characters.' },
        { pattern: /^(?=.*[A-Za-z])(?=.*\d).+$/, message: 'Include at least one letter and one number.' },
        ({ getFieldValue }) => ({ validator(_, value) {
          return !value || value !== getFieldValue('currentPassword')
            ? Promise.resolve() : Promise.reject(new Error('Choose a password different from your current one.'))
        } }),
      ]}>
        <Input.Password autoComplete="new-password" maxLength={100} />
      </Form.Item>
      <Form.Item name="confirmPassword" label="Confirm new password" dependencies={['newPassword']} rules={[
        { required: true, message: 'Confirm your new password.' },
        ({ getFieldValue }) => ({ validator(_, value) {
          return !value || value === getFieldValue('newPassword')
            ? Promise.resolve() : Promise.reject(new Error('Passwords do not match.'))
        } }),
      ]}>
        <Input.Password autoComplete="new-password" maxLength={100} />
      </Form.Item>
      </div><Feedback error={error} success={success} />
      <Button htmlType="submit" loading={saving}>Change password</Button>
    </Form>
  </Card>
}

export function AvatarSettings({ profile, onSaved, onAccessError }: {
  profile: UserProfile; onSaved: (url: string) => void; onAccessError: ErrorHandler
}) {
  const [selection, setSelection] = useState<{ file: File; url: string } | null>(null)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')
  const [success, setSuccess] = useState('')
  const startRequest = useRequestController()
  useEffect(() => () => { if (selection) URL.revokeObjectURL(selection.url) }, [selection])

  function select(file: File) {
    setError(''); setSuccess('')
    if (!['image/jpeg', 'image/png'].includes(file.type)) {
      setSelection(null)
      setError('Choose a JPEG or PNG image.')
    } else if (file.size === 0 || file.size > 5 * 1024 * 1024) {
      setSelection(null)
      setError('Choose a non-empty image no larger than 5 MB.')
    } else {
      setSelection({ file, url: URL.createObjectURL(file) })
    }
    return false
  }

  async function upload() {
    if (!selection || saving) return
    const request = startRequest()
    setSaving(true); setError(''); setSuccess('')
    try {
      const url = await profileApi.uploadAvatar(selection.file, request.signal)
      if (request.signal.aborted) return
      // MinIO replaces the same object URL; refresh both the profile and header image.
      const displayUrl = new URL(url, window.location.origin)
      displayUrl.searchParams.set('v', String(Date.now()))
      onSaved(displayUrl.href)
      setSelection(null)
      setSuccess('Profile photo saved.')
    } catch (cause) {
      if (request.signal.aborted || axios.isCancel(cause)) return
      const issue = profileError(cause, 'avatar')
      if (issue.accessDenied) onAccessError(issue)
      else setError(issue.fields.file?.join(' ') || issue.message)
    } finally {
      if (!request.signal.aborted) setSaving(false)
    }
  }

  return <section className="avatar-picker" aria-label="Profile photo">
    <div className="avatar-picker-portrait"><Avatar size={74} src={selection?.url || profile.avatarUrl || undefined} alt={`${profile.fullName}'s profile photo`}>
      {initials(profile.fullName)}
    </Avatar></div>
    <div className="avatar-picker-content">
      <span className="avatar-picker-label">Profile photo</span>
      <span className="avatar-picker-file">{selection?.file.name || (profile.avatarUrl ? 'Current profile photo' : 'No photo selected')}</span>
      <div className="avatar-picker-actions">
      <Upload accept="image/jpeg,image/png" maxCount={1} showUploadList={false} beforeUpload={select} disabled={saving}>
        <Button className="avatar-picker-choose" disabled={saving}>{profile.avatarUrl || selection ? 'Change photo' : 'Choose photo'}</Button>
      </Upload>
      </div><Text type="secondary">JPEG or PNG · Up to 5 MB</Text>
      {selection && <>
        <Text type="secondary">Preview only — save to update your profile.</Text>
        <div className="profile-actions">
          <Button type="primary" loading={saving} onClick={upload}>Save photo</Button>
          <Button disabled={saving} onClick={() => { setSelection(null); setError(''); setSuccess('') }}>Cancel</Button>
        </div>
      </>}
    <Feedback error={error} success={success} />
    </div>
  </section>
}
