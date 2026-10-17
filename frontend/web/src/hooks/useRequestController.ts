import { useEffect, useRef } from 'react'

export function useRequestController() {
  const controller = useRef<AbortController | null>(null)
  useEffect(() => () => controller.current?.abort(), [])
  return () => {
    controller.current?.abort()
    controller.current = new AbortController()
    return controller.current
  }
}
