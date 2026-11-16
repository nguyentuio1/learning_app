import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react'
import { Alert, Button, Input, Select } from 'antd'
import axios from 'axios'
import { customerOrderApi } from '../../services/customerOrderApi'
import { useRequestController } from '../../hooks/useRequestController'
import { assistantApi, assistantHttpStatus } from './assistantApi'
import { useAssistantResource } from './useAssistantResource'

type Notice = { type: 'error' | 'warning' | 'info'; title: string }

interface AssistantComposerProps {
  sessionId?: string
  disabled: boolean
  onAccessDenied: () => void
  onAnswered: (sessionId: string) => void
  onChecked: () => void
}

export function AssistantComposer({ sessionId, disabled, onAccessDenied, onAnswered, onChecked }: AssistantComposerProps) {
  const [draft, setDraft] = useState('')
  const [orderId, setOrderId] = useState<string>()
  const [working, setWorking] = useState(false)
  const [cooldown, setCooldown] = useState(false)
  const [recoveryNeeded, setRecoveryNeeded] = useState(false)
  const [notice, setNotice] = useState<Notice>()
  const [reply, setReply] = useState<string>()
  const pending = useRef(false)
  const retryTimer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined)
  const getController = useRequestController()
  useEffect(() => () => clearTimeout(retryTimer.current), [])

  const message = draft.trim()
  const length = Array.from(message).length
  const invalid = length > 2000 || /[\uD800-\uDFFF]/u.test(message)

  async function send(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (pending.current || disabled || cooldown || recoveryNeeded || invalid || !message) return
    pending.current = true
    setWorking(true)
    setNotice(undefined)
    setReply(undefined)
    const { signal } = getController()
    try {
      const result = await assistantApi.send({ sessionId: sessionId ?? null, orderId: orderId ?? null, message }, signal)
      if (signal.aborted) return
      setDraft('')
      if (result.outcome === 'ANSWERED') onAnswered(result.sessionId)
      else setReply(result.reply.messageText)
    } catch (error) {
      if (signal.aborted || axios.isCancel(error)) return
      const response = axios.isAxiosError(error) ? error.response : undefined
      switch (response?.status) {
        case 400:
          setNotice({ type: 'error', title: 'Check the selected order and message before sending again.' })
          break
        case 401:
        case 403:
          onAccessDenied()
          break
        case 404:
          if (sessionId) onChecked()
          if (orderId) setOrderId(undefined)
          setNotice({ type: 'warning', title: orderId
            ? 'The selected order may be unavailable. Choose an order again before retrying.'
            : 'The conversation is unavailable. Review your conversations before trying again.' })
          break
        case 409:
          onChecked()
          setNotice({ type: 'warning', title: 'The delivery information changed. Review the updated record before sending again.' })
          break
        case 429: {
          const seconds = Number(response.headers['retry-after'])
          const wait = Number.isFinite(seconds) && seconds > 0 ? seconds : 5
          setCooldown(true)
          retryTimer.current = setTimeout(() => setCooldown(false), Math.min(wait * 1000, 2_147_483_647))
          setNotice({ type: 'warning', title: 'The assistant is processing another request. Wait before trying again.' })
          break
        }
        case 503:
          setNotice({ type: 'warning', title: 'The assistant is temporarily unavailable.' })
          break
        default:
          setRecoveryNeeded(true)
          setNotice({ type: 'warning', title: 'We could not confirm the reply. Check your conversation before sending again.' })
      }
    } finally {
      pending.current = false
      if (!signal.aborted) setWorking(false)
    }
  }

  async function checkHistory() {
    if (pending.current) return
    pending.current = true
    setWorking(true)
    const { signal } = getController()
    try {
      if (sessionId) await assistantApi.messages(sessionId, null, signal)
      else await assistantApi.sessions(1, signal)
      if (signal.aborted) return
      onChecked()
      setRecoveryNeeded(false)
      setNotice({ type: 'info', title: sessionId
        ? 'Review the latest messages before sending again.'
        : 'Check the refreshed conversation list for your message before sending again.' })
    } catch (error) {
      if (signal.aborted || axios.isCancel(error)) return
      const status = assistantHttpStatus(error)
      if (status === 401 || status === 403) {
        onAccessDenied()
      } else if (status === 404 && sessionId) {
        onChecked()
      } else {
        setNotice({ type: 'warning', title: 'Could not check your conversations. Try checking again.' })
      }
    } finally {
      pending.current = false
      if (!signal.aborted) setWorking(false)
    }
  }

  return (
    <form className="assistant-composer" onSubmit={event => void send(event)}>
      {notice && <Alert
        type={notice.type}
        showIcon
        title={notice.title}
        action={recoveryNeeded && <Button loading={working} onClick={() => void checkHistory()}>Check history</Button>}
      />}
      {reply && <div className="assistant-reply" role="status">
        <strong>SmartDrone assistant</strong>
        <p>{reply}</p>
      </div>}
      <AssistantOrderSelect value={orderId} onChange={setOrderId} disabled={working || disabled} onAccessDenied={onAccessDenied} />
      <label htmlFor="assistant-message">Message</label>
      <Input.TextArea
        id="assistant-message"
        value={draft}
        onChange={event => setDraft(event.target.value)}
        placeholder="Ask about a delivery"
        autoSize={{ minRows: 2, maxRows: 5 }}
        readOnly={working}
        status={invalid ? 'error' : undefined}
        aria-invalid={invalid}
        aria-describedby="assistant-message-guidance"
      />
      <div className="assistant-composer-footer">
        <span id="assistant-message-guidance">{invalid ? 'Enter a valid message up to 2,000 characters.' : length.toLocaleString() + ' / 2,000'}</span>
        <Button
          type="primary"
          shape="circle"
          htmlType="submit"
          aria-label="Send message"
          title="Send message"
          icon={(
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor"
              strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
              <path d="M12 19V5M6 11l6-6 6 6" />
            </svg>
          )}
          loading={working && !recoveryNeeded}
          disabled={disabled || working || cooldown || recoveryNeeded || invalid || !message}
        />
      </div>
    </form>
  )
}

function AssistantOrderSelect({ value, onChange, disabled, onAccessDenied }: {
  value?: string
  onAccessDenied: () => void
  onChange: (value?: string) => void
  disabled: boolean
}) {
  const [keyword, setKeyword] = useState('')
  const [search, setSearch] = useState('')
  const [selected, setSelected] = useState<{ label: string; value: string }>()
  useEffect(() => {
    const timer = setTimeout(() => setSearch(keyword.trim()), 250)
    return () => clearTimeout(timer)
  }, [keyword])
  const loadOrders = useCallback((signal: AbortSignal) => customerOrderApi.list({
    page: 1, pageSize: 20, searchKeyword: search || undefined,
  }, signal), [search])
  const orders = useAssistantResource(loadOrders, onAccessDenied)
  const options = orders.data?.items.map(order => ({ value: order.id, label: order.trackingNumber })) ?? []
  if (selected && selected.value === value && !options.some(option => option.value === selected.value)) options.unshift(selected)

  return (
    <>
      <label htmlFor="assistant-order">Order (optional)</label>
      <Select
        id="assistant-order"
        value={value}
        options={options}
        placeholder="Find an order by tracking number"
        disabled={disabled}
        loading={orders.loading}
        allowClear
        showSearch={{ filterOption: false, onSearch: setKeyword }}
        notFoundContent={orders.error ? 'Orders could not be loaded.' : orders.loading ? 'Loading orders…' : 'No matching orders.'}
        onChange={(id?: string) => {
          setSelected(options.find(option => option.value === id))
          onChange(id)
        }}
      />
    </>
  )
}
