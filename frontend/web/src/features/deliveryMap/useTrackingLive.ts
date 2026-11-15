import { useEffect, useState } from 'react'
import { HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr'
import { apiBaseURL, session } from '../../services/authSession'

type LiveState = 'connecting' | 'live' | 'reconnecting' | 'polling' | 'paused'

export function useTrackingLive(id: string, refresh: () => void, enabled: boolean, onLiveChange: (live: boolean) => void) {
  const [state, setState] = useState<LiveState>('connecting')
  useEffect(() => {
    if (!enabled || !apiBaseURL) return
    let disposed = false
    let timer: ReturnType<typeof setTimeout> | undefined
    let starting = false
    let refreshedToken = false
    const initialGeneration = session.getSnapshot().generation
    const connection = new HubConnectionBuilder()
      .withUrl(`${apiBaseURL.replace(/\/$/, '')}/hubs/tracking`, {
        accessTokenFactory: () => {
          const current = session.getSnapshot()
          if (current.generation !== initialGeneration || !current.accessToken) throw new Error('Session changed')
          return current.accessToken
        },
      })
      .withAutomaticReconnect([0, 2000, 10000, 30000])
      .configureLogging(LogLevel.None)
      .build()
    const status = (value: LiveState) => { setState(value); onLiveChange(value === 'live') }
    const active = () => !disposed && !document.hidden && session.getSnapshot().generation === initialGeneration
    async function subscribe() {
      if (!active()) return
      await connection.invoke('Subscribe', id)
      if (active()) { status('live'); refresh() }
    }
    function scheduleStart() {
      clearTimeout(timer)
      if (active()) timer = setTimeout(() => { void start() }, 5000)
    }
    async function start() {
      if (!active() || starting || connection.state !== HubConnectionState.Disconnected) return
      starting = true
      try {
        await connection.start()
        if (active()) await subscribe()
        else await connection.stop()
      } catch (error) {
        if (active()) {
          status('polling'); refresh()
          if (!refreshedToken && typeof error === 'object' && error && 'statusCode' in error && error.statusCode === 401) {
            refreshedToken = true
            try { await session.refresh() } catch { /* HTTP auth flow owns expired-session handling. */ }
          }
          await connection.stop()
          scheduleStart()
        }
      } finally { starting = false }
    }
    // PositionUpdated has no order ID. A fresh connection is owned by each selection;
    // events only trigger an authoritative HTTP read, never overwrite another mission.
    const update = () => { if (active()) refresh() }
    connection.on('PositionUpdated', update)
    connection.on('StatusChanged', update)
    connection.onreconnecting(() => { if (active()) { status('reconnecting'); refresh() } })
    connection.onreconnected(async () => {
      try { await subscribe() }
      catch { if (active()) { status('polling'); refresh(); await connection.stop(); scheduleStart() } }
    })
    connection.onclose(() => { if (active()) { status('polling'); refresh(); scheduleStart() } })
    function visibility() {
      clearTimeout(timer)
      if (document.hidden) { status('paused'); void connection.stop() }
      else { status('connecting'); refresh(); void start(); scheduleStart() }
    }
    void start()
    document.addEventListener('visibilitychange', visibility)
    return () => {
      disposed = true; clearTimeout(timer)
      document.removeEventListener('visibilitychange', visibility)
      connection.off('PositionUpdated', update); connection.off('StatusChanged', update)
      // Closing the connection also removes its order subscription on the server.
      void connection.stop()
    }
  }, [id, refresh, enabled, onLiveChange])
  return enabled ? state : 'polling'
}
