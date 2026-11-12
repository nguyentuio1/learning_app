import { useEffect, useRef, useState } from 'react'
import axios from 'axios'
import { Alert, Button, Form, Input, Spin } from 'antd'
import { Link, Navigate } from 'react-router-dom'
import { useAuth } from '../hooks/useAuth'
import { LoginIllustration } from '../components/auth/LoginIllustration'
import './login.css'

interface RegisterValues {
  email: string
  password: string
  fullName: string
  phoneNumber: string
}

export function RegisterPage() {
  const auth = useAuth()
  const [form] = Form.useForm<RegisterValues>()
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [leaving, setLeaving] = useState(false)
  const [transitionDone, setTransitionDone] = useState(false)
  const submitting = useRef(false)
  useEffect(() => {
    if (!leaving) return
    const timer = window.setTimeout(() => setTransitionDone(true), 200)
    return () => window.clearTimeout(timer)
  }, [leaving])
  if (auth.status === 'loading') return <main className="page-shell"><Spin aria-label="Restoring session" /></main>
  if (auth.status === 'authenticated' && (!busy || transitionDone)) return <Navigate to="/orders" replace />

  async function submit(values: RegisterValues) {
    if (submitting.current) return
    submitting.current = true
    setBusy(true)
    setError('')
    try {
      await auth.register(values)
      if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) setTransitionDone(true)
      else setLeaving(true)
    } catch (cause) {
      submitting.current = false
      setBusy(false)
      form.setFieldValue('password', '')
      if (axios.isAxiosError(cause)) {
        const status = cause.response?.status
        const body: unknown = cause.response?.data
        const detail = typeof body === 'object' && body !== null && 'detail' in body
          ? body.detail
          : null
        setError(status === 409 && typeof detail === 'string'
          ? detail
          : status === 400 ? 'Check the registration details and try again.'
          : !cause.response ? 'Unable to reach the server. Please try again.'
          : 'Registration is temporarily unavailable. Please try again later.')
      } else setError(cause instanceof Error ? cause.message : 'Unable to register.')
    }
  }

  return <main className={`login-layout${leaving ? ' login-leaving' : ''}`} aria-busy={busy}>
    <LoginIllustration />
    <section className="login-column" aria-labelledby="register-title">
      <Form className="login-form register-form" form={form} layout="vertical" requiredMark={false} onFinish={submit} disabled={busy}>
        <span className="gold-mark" aria-hidden="true" />
        <h1 id="register-title">Create customer account</h1>
        <Form.Item name="fullName" label="Full name" rules={[{ required: true, whitespace: true, message: 'Enter your full name.' }, { max: 150, message: 'Use 150 characters or fewer.' }]}>
          <Input autoComplete="name" size="large" maxLength={150} />
        </Form.Item>
        <Form.Item name="email" label="Email" rules={[{ required: true, message: 'Enter your email.' }, { type: 'email', message: 'Enter a valid email.' }, { max: 255, message: 'Use 255 characters or fewer.' }]}>
          <Input autoComplete="email" type="email" size="large" maxLength={255} />
        </Form.Item>
        <Form.Item name="phoneNumber" label="Phone number" rules={[{ required: true, message: 'Enter your phone number.' }, { pattern: /^\d{9,11}$/, message: 'Enter 9 to 11 digits.' }]}>
          <Input autoComplete="tel" type="tel" size="large" maxLength={11} />
        </Form.Item>
        <Form.Item name="password" label="Password" rules={[
          { required: true, message: 'Enter your password.' },
          { min: 8, max: 100, message: 'Use 8 to 100 characters.' },
          { pattern: /^(?=.*[A-Za-z])(?=.*\d).+$/, message: 'Include at least one letter and one digit.' },
        ]}>
          <Input.Password autoComplete="new-password" size="large" maxLength={100} />
        </Form.Item>
        <div className="login-feedback" aria-live="polite">{error && <Alert showIcon type="error" title={error} />}</div>
        <Button className="sign-in" htmlType="submit" type="primary" size="large" block loading={busy}>Register</Button>
        <p className="account-switch">Already have an account? <Link to="/login">Log in</Link></p>
      </Form>
    </section>
  </main>
}
