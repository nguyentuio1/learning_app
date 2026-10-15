import axios from 'axios'
import type { AuthResponse, Session } from '../types/auth'

export const apiBaseURL = import.meta.env.DEV ? '/' : import.meta.env.VITE_API_BASE_URL
const transport = axios.create({ baseURL: apiBaseURL, timeout: 20000 })
const storageKey = 'smartdrone.refreshToken'
let state: Session = { status: 'loading', user: null, accessToken: null, refreshToken: null, generation: 0, message: '' }
const listeners = new Set<() => void>()
let refreshPending: Promise<string> | null = null
let refreshGeneration = -1
let bootstrapPending: Promise<void> | null = null

function emit(next: Session) { state = next; listeners.forEach(listener => listener()) }
function persist(token: string | null) {
  try {
    if (token) sessionStorage.setItem(storageKey, token)
    else sessionStorage.removeItem(storageKey)
  } catch { /* Storage can be disabled; the in-memory session remains usable. */ }
}
function storedToken() {
  try { return sessionStorage.getItem(storageKey) } catch { return null }
}
function requireConfiguration() {
  if (!apiBaseURL) throw new Error('The API address is not configured.')
}
function accept(response: AuthResponse, generation: number) {
  if (state.generation !== generation) throw new axios.CanceledError('Session changed')
  if (!response.accessToken || !response.refreshToken || !response.user?.id || !response.user.role?.code || response.user.status !== 'ACTIVE') {
    throw new Error('The server returned an invalid session.')
  }
  persist(response.refreshToken)
  emit({ status: 'authenticated', user: response.user, accessToken: response.accessToken,
    refreshToken: response.refreshToken, generation, message: '' })
}

export const session = {
  getSnapshot: () => state,
  subscribe(listener: () => void) { listeners.add(listener); return () => { listeners.delete(listener) } },
  clear(message = '') {
    persist(null)
    emit({ status: 'anonymous', user: null, accessToken: null, refreshToken: null, generation: state.generation + 1, message })
  },
  async login(email: string, password: string) {
    requireConfiguration()
    session.clear()
    const generation = state.generation
    const { data } = await transport.post<AuthResponse>('/api/auth/login', { email: email.trim(), password })
    accept(data, generation)
  },
  refresh(): Promise<string> {
    if (refreshPending && refreshGeneration === state.generation) return refreshPending
    const generation = state.generation
    refreshGeneration = generation
    const token = state.refreshToken
    if (!token) {
      session.clear('Your session has expired. Please sign in again.')
      return Promise.reject(new Error('No refresh token'))
    }
    refreshPending = (async () => {
      try {
        requireConfiguration()
        const { data } = await transport.post<AuthResponse>('/api/auth/refresh', { refreshToken: token })
        if (state.generation !== generation) {
          // A logout during rotation must not leave the newly issued token active.
          await transport.post('/api/auth/logout', { refreshToken: data.refreshToken },
            { headers: { Authorization: `Bearer ${data.accessToken}` } }).catch(() => undefined)
          throw new axios.CanceledError('Session changed')
        }
        accept(data, generation)
        return data.accessToken
      } catch (error) {
        if (state.generation === generation) session.clear('Your session could not be renewed. Please sign in again.')
        throw error
      } finally { if (refreshGeneration === generation) refreshPending = null }
    })()
    return refreshPending
  },
  initialize(): Promise<void> {
    if (bootstrapPending) return bootstrapPending
    bootstrapPending = (async () => {
      const token = storedToken()
      if (!token) { session.clear(); return }
      emit({ ...state, refreshToken: token })
      await session.refresh().catch(() => undefined)
    })()
    return bootstrapPending
  },
  async logout() {
    const previous = state
    session.clear()
    if (!previous.refreshToken || !previous.accessToken) return
    try {
      await transport.post('/api/auth/logout', { refreshToken: previous.refreshToken },
        { headers: { Authorization: `Bearer ${previous.accessToken}` } })
    } catch {
      if (state.status === 'anonymous' && state.generation === previous.generation + 1) {
        emit({ ...state, message: 'Signed out on this device. Server revocation could not be confirmed.' })
      }
    }
  },
}
