import { useEffect, useState } from 'react'
import axios from 'axios'
import { userManagementApi } from '../services/userManagementApi'
import { userManagementError, type UserManagementError } from '../utils/userManagement'
import type { ManagedUser } from '../types/userManagement'

export function useManagedUsers() {
  const [data, setData] = useState<ManagedUser[]>([])
  const [loading, setLoading] = useState(true)
  const [issue, setIssue] = useState<UserManagementError | null>(null)
  const [attempt, setAttempt] = useState(0)
  useEffect(() => {
    const controller = new AbortController()
    userManagementApi.list(controller.signal).then(users => {
      if (!controller.signal.aborted) setData(users)
    }).catch(error => {
      if (!controller.signal.aborted && !axios.isCancel(error)) setIssue(userManagementError(error))
    }).finally(() => { if (!controller.signal.aborted) setLoading(false) })
    return () => controller.abort()
  }, [attempt])
  function reload() { setLoading(true); setIssue(null); setData([]); setAttempt(value => value + 1) }
  return { data, loading, issue, reload }
}
