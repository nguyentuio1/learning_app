import { useCallback, useState } from 'react'
import { Alert, Button, Pagination } from 'antd'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { OrderUiProvider } from '../../components/orders/OrderUiProvider'
import { useAuth } from '../../hooks/useAuth'
import { useRequestController } from '../../hooks/useRequestController'
import { orderTime } from '../../utils/orderDisplay'
import { OperationsPage, ResourceState } from '../operations/OperationsUi'
import { AssistantComposer } from './AssistantComposer'
import { assistantApi, assistantHttpStatus } from './assistantApi'
import { useAssistantResource } from './useAssistantResource'
import './customer-assistant.css'

const sessionIdPattern = /^[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}$/i
const sendEnabled = import.meta.env.VITE_AI_ASSISTANT_SEND_ENABLED === 'true'
const assistantPath = '/delivery-assistant'

export function CustomerAssistantPage() {
  const { user } = useAuth()
  const { id } = useParams()

  return (
    <OrderUiProvider>
      <OperationsPage title="Delivery assistant" subtitle={sendEnabled
        ? 'Ask about a delivery or continue a saved conversation.'
        : 'View your saved delivery conversations.'}>
        {!sendEnabled && <Alert
          type="info"
          showIcon
          title="Sending messages is unavailable"
          className="assistant-read-only"
          description="You can view saved conversations while the policy for storing and deleting messages is being finalized."
        />}
        {id && !sessionIdPattern.test(id) ? (
          <Alert type="error" showIcon title="Invalid conversation" action={
            <Link className="assistant-link" to={assistantPath}>Back to conversations</Link>
          } />
        ) : (
          <AssistantWorkspace key={user?.id} id={id} />
        )}
      </OperationsPage>
    </OrderUiProvider>
  )
}

function AssistantWorkspace({ id }: { id?: string }) {
  const [accessDenied, setAccessDenied] = useState(false)
  const [checkingAccess, setCheckingAccess] = useState(false)
  const getController = useRequestController()
  const denyAccess = useCallback(() => setAccessDenied(true), [])

  async function checkAccess() {
    if (checkingAccess) return
    setCheckingAccess(true)
    const { signal } = getController()
    try {
      await assistantApi.sessions(1, signal)
      if (!signal.aborted) setAccessDenied(false)
    } catch {
      // Keep the workspace closed until the server confirms access.
    } finally {
      if (!signal.aborted) setCheckingAccess(false)
    }
  }

  if (accessDenied) {
    return <Alert type="error" showIcon title="Your account cannot access the delivery assistant."
      action={<Button loading={checkingAccess} onClick={() => void checkAccess()}>Check access</Button>} />
  }

  // Unmount the protected workspace on denial to discard its data and cancel pending reads.
  return <AssistantConversations id={id} onAccessDenied={denyAccess} />
}

function AssistantConversations({ id, onAccessDenied }: { id?: string; onAccessDenied: () => void }) {
  const navigate = useNavigate()
  const [page, setPage] = useState(1)
  const load = useCallback((signal: AbortSignal) => assistantApi.sessions(page, signal), [page])
  const sessions = useAssistantResource(load, onAccessDenied)

  function refreshRecentSessions() {
    setPage(1)
    sessions.reload()
  }

  function openAnsweredSession(sessionId: string) {
    refreshRecentSessions()
    if (id?.toLowerCase() !== sessionId.toLowerCase()) navigate(assistantPath + '/' + sessionId)
  }

  return (
    <div className={id ? 'assistant-layout assistant-layout-selected' : 'assistant-layout'}>
      <aside className="operations-panel assistant-sessions" aria-label="Saved conversations">
        <header className="assistant-section-header">
          <h2>Conversations</h2>
          <Button onClick={sessions.reload} loading={sessions.loading}>Refresh</Button>
        </header>
        <ResourceState loading={sessions.loading} error={sessions.error} retry={sessions.reload} />
        {sessions.data && !sessions.data.items.length && <p>No conversations have been saved.</p>}
        {!!sessions.data?.items.length && (
          <ul>
            {sessions.data.items.map(item => (
              <li key={item.id}>
                <Link to={assistantPath + '/' + item.id} aria-current={id === item.id ? 'page' : undefined}>
                  <strong>{item.sessionTitle || 'Delivery conversation'}</strong>
                  <span>{orderTime(item.createdAt)}</span>
                </Link>
              </li>
            ))}
          </ul>
        )}
        {sessions.data && sessions.data.totalCount > sessions.data.pageSize && (
          <Pagination current={page} total={sessions.data.totalCount} pageSize={sessions.data.pageSize}
            onChange={setPage} showSizeChanger={false} size="small" />
        )}
      </aside>
      <Conversation key={id ?? 'new'} id={id} sessionsReady={!!sessions.data} onAccessDenied={onAccessDenied}
        onAnswered={openAnsweredSession} onChecked={refreshRecentSessions} />
    </div>
  )
}

function Conversation({ id, sessionsReady, onAccessDenied, onAnswered, onChecked }: {
  id?: string
  sessionsReady: boolean
  onAccessDenied: () => void
  onAnswered: (sessionId: string) => void
  onChecked: () => void
}) {
  const [page, setPage] = useState<number | null>(null)
  const load = useCallback((signal: AbortSignal) =>
    id ? assistantApi.messages(id, page, signal) : Promise.resolve(undefined), [id, page])
  const history = useAssistantResource(load, onAccessDenied)
  const missing = assistantHttpStatus(history.error) === 404
  const messages = Array.from(new Map(history.data?.items.map(message => [message.id, message])).values())

  function refreshHistory() {
    if (id) setPage(null)
    history.reload()
  }

  function handleAnswer(sessionId: string) {
    if (id) refreshHistory()
    onAnswered(sessionId)
  }

  const title = id ? 'Conversation history' : sendEnabled ? 'New conversation' : 'Delivery conversations'
  return (
    <section className={'operations-panel assistant-conversation' + (!id && !sendEnabled ? ' assistant-empty' : '')} aria-label={title}>
      <header className="assistant-section-header">
        <div>
          {id && <Link className="assistant-link" to={assistantPath}>{sendEnabled ? 'Conversations' : 'Back to conversations'}</Link>}
          <h2>{title}</h2>
        </div>
        {id && <Button onClick={refreshHistory} loading={history.loading}>Refresh history</Button>}
      </header>
      {!id && !sendEnabled && <p>Choose a saved conversation to read its messages.</p>}
      {id && (missing ? (
        <Alert type="warning" showIcon title="This conversation is unavailable." />
      ) : <ResourceState loading={history.loading} error={history.error} retry={refreshHistory} />)}
      {history.data && !messages.length && <p>This page contains no messages.</p>}

      {!!messages.length && (
        <ol className="assistant-messages" aria-label="Messages">
          {messages.map(message => (
            <li key={message.id} className={message.senderType === 'USER' ? 'assistant-message-user' : ''}>
              <div>
                <div className="assistant-message-meta">
                  <strong>{message.senderType === 'USER' ? 'You' : 'SmartDrone assistant'}</strong>
                  <time dateTime={message.createdAt}>{orderTime(message.createdAt)}</time>
                </div>
                <p>{message.messageText}</p>
              </div>
            </li>
          ))}
        </ol>
      )}
      {history.data && history.data.totalCount > history.data.pageSize && (
        <Pagination current={history.data.page} total={history.data.totalCount} pageSize={history.data.pageSize}
          onChange={setPage} showSizeChanger={false} size="small" />
      )}
      {sendEnabled && !missing && <AssistantComposer
        sessionId={id}
        disabled={!sessionsReady || (!!id && !history.data)}
        onAccessDenied={onAccessDenied}
        onAnswered={handleAnswer}
        onChecked={() => {
          if (id) refreshHistory()
          onChecked()
        }}
      />}
    </section>
  )
}
