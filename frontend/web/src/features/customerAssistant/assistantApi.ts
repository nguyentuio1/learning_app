import axios from 'axios'
import { apiClient } from '../../services/apiClient'

export function assistantHttpStatus(error: unknown) {
  return axios.isAxiosError(error) ? error.response?.status : undefined
}

const pageSize = 20

interface AssistantPage<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
}

export interface AssistantSession {
  id: string
  sessionTitle: string | null
  createdAt: string
}

export interface AssistantMessage {
  id: string
  senderType: 'USER' | 'BOT'
  messageText: string
  createdAt: string
}

export interface AssistantSendRequest {
  sessionId: string | null
  orderId: string | null
  message: string
}

type AssistantReply = Omit<AssistantMessage, 'id' | 'senderType'> & { senderType: 'BOT' }

export type AssistantSendResult =
  | { outcome: 'ANSWERED'; sessionId: string; userMessageId: string; reply: AssistantReply & { id: string } }
  | { outcome: 'CLARIFICATION_REQUIRED' | 'HELP' | 'REFUSED'; sessionId: null; userMessageId: null; reply: AssistantReply & { id: null } }

async function readMessages(id: string, page: number, signal: AbortSignal) {
  const { data } = await apiClient.get<AssistantPage<AssistantMessage> & { sessionId: string }>(
    '/api/ai-assistant/sessions/' + encodeURIComponent(id) + '/messages',
    { params: { page, pageSize }, signal },
  )
  if (data.sessionId.toLowerCase() !== id.toLowerCase()) throw new Error('Unexpected conversation history')
  return data
}

// Sprint 7 API contract (#209), section 5.
export const assistantApi = {
  async sessions(page: number, signal: AbortSignal) {
    const { data } = await apiClient.get<AssistantPage<AssistantSession>>('/api/ai-assistant/sessions', {
      params: { page, pageSize },
      signal,
    })
    return data
  },
  async messages(id: string, page: number | null, signal: AbortSignal) {
    const data = await readMessages(id, page ?? 1, signal)
    const lastPage = Math.ceil(data.totalCount / data.pageSize)
    return page === null && lastPage > 1 ? readMessages(id, lastPage, signal) : data
  },
  async send(request: AssistantSendRequest, signal: AbortSignal) {
    const { data } = await apiClient.post<AssistantSendResult>('/api/ai-assistant/messages', request, {
      signal,
      timeout: 15_000,
    })
    if (typeof data.reply?.messageText !== 'string' || data.reply.senderType !== 'BOT') {
      throw new Error('Unexpected assistant reply')
    }
    if (data.outcome === 'ANSWERED' && (!data.sessionId || !data.userMessageId || !data.reply.id ||
      (request.sessionId && request.sessionId.toLowerCase() !== data.sessionId.toLowerCase()))) {
      throw new Error('Unexpected assistant conversation')
    }
    if (data.outcome !== 'ANSWERED' &&
      (!['CLARIFICATION_REQUIRED', 'HELP', 'REFUSED'].includes(data.outcome) ||
        data.sessionId !== null || data.userMessageId !== null || data.reply.id !== null)) {
      throw new Error('Unexpected temporary assistant reply')
    }
    return data
  },
}
