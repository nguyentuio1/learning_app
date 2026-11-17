import { apiClient } from '../../services/apiClient'

// Sprint 7 API contract (#209), section 4. This request has no body.
export type DeliverySummary =
  | { orderId: string; outcome: 'GENERATED'; text: string; generatedAt: string }
  | { orderId: string; outcome: 'NO_HISTORY'; text: null; generatedAt: null }

export async function generateDeliverySummary(id: string, signal: AbortSignal) {
  const { data } = await apiClient.post<DeliverySummary>(
    '/api/delivery-orders/' + encodeURIComponent(id) + '/ai-summary',
    undefined,
    { signal, timeout: 15_000 },
  )
  if (data.orderId.toLowerCase() !== id.toLowerCase()) throw new Error('Unexpected delivery summary')
  return data
}
