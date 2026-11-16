import { useCallback } from 'react'
import { useResource } from '../operations/useResource'
import { assistantHttpStatus } from './assistantApi'

export function useAssistantResource<T>(load: (signal: AbortSignal) => Promise<T>, onAccessDenied: () => void) {
  const loadWithAccessCheck = useCallback(async (signal: AbortSignal) => {
    try {
      return await load(signal)
    } catch (error) {
      const status = assistantHttpStatus(error)
      if (!signal.aborted && (status === 401 || status === 403)) onAccessDenied()
      throw error
    }
  }, [load, onAccessDenied])
  return useResource(loadWithAccessCheck)
}
