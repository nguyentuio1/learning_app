import { useEffect, useRef, useState } from 'react'
import axios from 'axios'
import { Alert, Button, Form, Input, Spin } from 'antd'
import { Navigate, useLocation } from 'react-router-dom'
import { useAuth } from '../hooks/useAuth'
import { LoginIllustration } from '../components/auth/LoginIllustration'
import './login.css'

export function LoginPage() {
  const auth = useAuth()
  const location = useLocation()
  const [form] = Form.useForm<{ email: string; password: string }>()
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
  if (auth.status === 'authenticated' && (!busy || transitionDone)) {
    // Accept only implemented internal destinations, never arbitrary redirect URLs.
    const usesMobile = auth.user?.role.code === 'CUSTOMER' || auth.user?.role.code === 'STATION_OPERATOR'
    const destination = usesMobile || location.state?.from === '/' ? '/' : '/profile'
    return <Navigate to={destination} replace />
  }
  async function submit(values: { email: string; password: string }) {
    if (submitting.current) return
    submitting.current = true
    setBusy(true); setError('')
    try {
      await auth.login(values.email, values.password)
      if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) setTransitionDone(true)
      else setLeaving(true)
    }
    catch (cause) {
      submitting.current = false
      setBusy(false)
      form.setFieldValue('password', '')
      if (axios.isAxiosError(cause)) {
        const status = cause.response?.status
        setError(status === 401 ? 'Invalid email or password.' : status === 403 ? 'This account is not active. Contact your administrator.'
          : status === 400 ? 'Check your email and password.' : !cause.response ? 'Unable to reach the server. Please try again.'
            : 'Sign-in is temporarily unavailable. Please try again later.')
      } else setError(cause instanceof Error ? cause.message : 'Unable to sign in.')
    }
  }
  return <main className={`login-layout${leaving ? ' login-leaving' : ''}`} aria-busy={busy}>
    <LoginIllustration />
    <section className="login-column" aria-labelledby="login-title">
      <Form className="login-form" form={form} layout="vertical" requiredMark={false} onFinish={submit} disabled={busy}>
        <span className="gold-mark" aria-hidden="true" />
        <h1 id="login-title">Sign in</h1>
        <Form.Item name="email" label="Email" rules={[{ required: true, message: 'Enter your email.' }, { type: 'email', message: 'Enter a valid email.' }, { max: 255 }]}>
          <Input autoComplete="username" type="email" size="large" maxLength={255} placeholder="name@company.com" />
        </Form.Item>
        <Form.Item name="password" label="Password" rules={[{ required: true, message: 'Enter your password.' }]}>
          <Input.Password autoComplete="current-password" size="large" placeholder="Enter your password" />
        </Form.Item>
        <div className="login-feedback" aria-live="polite">{(error || auth.message) && <Alert showIcon type={error ? 'error' : 'info'} title={error || auth.message} />}</div>
        <Button className="sign-in" htmlType="submit" type="primary" size="large" block loading={busy}>Sign in</Button>
      </Form>
    </section>
  </main>
}
