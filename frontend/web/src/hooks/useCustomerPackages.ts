import { useEffect, useState } from 'react'
import { customerPackageApi } from '../services/customerPackageApi'
import type { CustomerPackage } from '../types/customerPackage'
import { customerPackageIssue } from '../utils/customerPackageIssue'

export function useCustomerPackages() {
  const [data, setData] = useState<CustomerPackage[]>([])
  const [loading, setLoading] = useState(true)
  const [issue, setIssue] = useState('')
  const [attempt, setAttempt] = useState(0)
  useEffect(() => {
    const controller = new AbortController()
    const request = customerPackageApi.listPackages(controller.signal)
    request.then(items => { if (!controller.signal.aborted) setData(items) })
      .catch(error => { if (!controller.signal.aborted) setIssue(customerPackageIssue(error, 'Unable to load your packages.')) })
      .finally(() => { if (!controller.signal.aborted) setLoading(false) })
    return () => controller.abort()
  }, [attempt])
  function reload() { setLoading(true); setIssue(''); setData([]); setAttempt(value => value + 1) }
  return { data, loading, issue, reload }
}
