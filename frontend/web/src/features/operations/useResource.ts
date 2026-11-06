import { useCallback, useEffect, useState } from 'react'

export function useResource<T>(load: (signal: AbortSignal) => Promise<T>) {
  const [revision, setRevision] = useState(0)
  const [result, setResult] = useState<{ loader: typeof load; revision: number; data?: T; error?: unknown }>()
  useEffect(() => {
    const controller = new AbortController()
    load(controller.signal)
      .then(data => { if (!controller.signal.aborted) setResult({ loader: load, revision, data }) })
      .catch(error => { if (!controller.signal.aborted) setResult({ loader: load, revision, error }) })
    return () => controller.abort()
  }, [load, revision])
  const current = result?.loader === load && result.revision === revision ? result : undefined
  const reload = useCallback(() => setRevision(value => value + 1), [])
  return { data: current?.data, error: current?.error, loading: !current, reload }
}
