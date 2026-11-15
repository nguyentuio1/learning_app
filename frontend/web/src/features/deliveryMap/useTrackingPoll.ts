import { useCallback, useEffect, useRef, useState } from 'react'
import axios from 'axios'

interface PollResult<T> {
  loader: (signal: AbortSignal) => Promise<T>
  data?: T
  error?: unknown
  refreshing: boolean
  receivedAt?: number
  blocked?: boolean
}

// Refresh snapshots on live signals and every five seconds to reevaluate freshness.
export function useTrackingPoll<T>(load: (signal: AbortSignal) => Promise<T>, intervalMs = 5000) {
  const [revision, setRevision] = useState(0)
  const [result, setResult] = useState<PollResult<T>>()
  const refreshRequest = useRef<() => void>(() => {})
  useEffect(() => {
    let disposed = false
    let stopped = false
    let controller: AbortController | undefined
    let timer: ReturnType<typeof setTimeout> | undefined
    let queued = false
    async function run() {
      if (disposed || stopped || controller || document.hidden) return
      const request = new AbortController()
      controller = request
      setResult(previous => ({ ...(previous?.loader === load ? previous : {}), loader: load, refreshing: true }))
      try {
        const data = await load(request.signal)
        if (!disposed && !request.signal.aborted) setResult({ loader: load, data, refreshing: false, receivedAt: Date.now() })
      } catch (error) {
        if (!disposed && !request.signal.aborted) {
          const status = axios.isAxiosError(error) ? error.response?.status : undefined
          stopped = status === 401 || status === 403 || status === 404
          setResult(previous => ({
            ...(previous?.loader === load && !stopped ? previous : {}),
            loader: load, error, refreshing: false, blocked: stopped,
          }))
        }
      } finally {
        if (controller === request) controller = undefined
        if (!disposed && !request.signal.aborted && !stopped && !document.hidden) {
          timer = setTimeout(run, queued ? 0 : intervalMs)
          queued = false
        }
      }
    }
    refreshRequest.current = () => {
      clearTimeout(timer)
      if (controller) queued = true
      else void run()
    }
    function visibility() {
      clearTimeout(timer)
      if (document.hidden) { controller?.abort(); controller = undefined }
      else void run()
    }
    void run()
    document.addEventListener('visibilitychange', visibility)
    return () => { disposed = true; refreshRequest.current = () => {}; clearTimeout(timer); controller?.abort(); document.removeEventListener('visibilitychange', visibility) }
  }, [load, revision, intervalMs])
  const reload = useCallback(() => setRevision(value => value + 1), [])
  const refresh = useCallback(() => refreshRequest.current(), [])
  const current = result?.loader === load ? result : undefined
  return { ...current, loading: !current?.data && !current?.error, reload, refresh }
}
