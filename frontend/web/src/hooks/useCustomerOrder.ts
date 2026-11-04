import { useEffect, useState } from 'react'
import { customerOrderApi } from '../services/customerOrderApi'
import type { CustomerOrderDetail } from '../types/customerOrder'
import { orderFeedback } from '../utils/orderFeedback'

export function useCustomerOrder(id: string) {
  const [result, setResult] = useState<{ id: string; data?: CustomerOrderDetail; error?: string }>()
  const [attempt, setAttempt] = useState(0)
  useEffect(() => {
    const controller = new AbortController()
    customerOrderApi.detail(id, controller.signal)
      .then(data => { if (!controller.signal.aborted) setResult({ id, data }) })
      .catch(error => { if (!controller.signal.aborted) setResult({ id, error: orderFeedback(error, 'Unable to load this order.') }) })
    return () => controller.abort()
  }, [id, attempt])
  return {
    order: result?.id === id ? result.data : undefined,
    error: result?.id === id ? result.error : undefined,
    loading: !result || result.id !== id,
    refresh() { setResult(undefined); setAttempt(x => x + 1) },
  }
}
