import { useEffect, useState } from 'react'
import { customerAddressApi } from '../services/customerAddressApi'
import type { CustomerAddress } from '../types/customerAddress'
import { customerAddressIssue } from '../utils/customerAddressIssue'

export function useCustomerAddresses() {
  const [data, setData] = useState<CustomerAddress[]>([])
  const [loading, setLoading] = useState(true)
  const [issue, setIssue] = useState('')
  const [attempt, setAttempt] = useState(0)
  useEffect(() => {
    const controller = new AbortController()
    const request = customerAddressApi.listAddresses(controller.signal)
    request.then(items => { if (!controller.signal.aborted) setData(items) })
      .catch(error => { if (!controller.signal.aborted) setIssue(customerAddressIssue(error, 'Unable to load your addresses.')) })
      .finally(() => { if (!controller.signal.aborted) setLoading(false) })
    return () => controller.abort()
  }, [attempt])
  function reload() { setLoading(true); setIssue(''); setData([]); setAttempt(value => value + 1) }
  return { data, loading, issue, reload }
}
