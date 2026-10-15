import axios, { type InternalAxiosRequestConfig } from 'axios'
import { apiBaseURL, session } from './authSession'

type AuthConfig = InternalAxiosRequestConfig & { sessionGeneration?: number; authRetried?: boolean }

export const apiClient = axios.create({
  baseURL: apiBaseURL,
  timeout: 20000,
})

apiClient.interceptors.request.use(
  (config: AuthConfig) => {
    const current = session.getSnapshot()
    if (config.sessionGeneration !== undefined && config.sessionGeneration !== current.generation) {
      throw new axios.CanceledError('Session changed')
    }
    // This client is for our API only. Never send credentials to external URLs.
    if (!config.url?.startsWith('/api/') || (config.baseURL && config.baseURL !== apiBaseURL)) {
      throw new Error('Use the API client only for application API paths.')
    }
    config.sessionGeneration = current.generation
    if (current.accessToken) config.headers.set('Authorization', `Bearer ${current.accessToken}`)
    else config.headers.delete('Authorization')
    return config
  },
  (error: unknown) => Promise.reject(error),
)

apiClient.interceptors.response.use(
  (response) => {
    if ((response.config as AuthConfig).sessionGeneration !== session.getSnapshot().generation) {
      throw new axios.CanceledError('Session changed')
    }
    return response
  },
  async (error: unknown) => {
    if (!axios.isAxiosError(error) || !error.config) throw error
    const config = error.config as AuthConfig
    const current = session.getSnapshot()
    if (config.sessionGeneration !== current.generation) throw new axios.CanceledError('Session changed')
    const detail = error.response?.data?.detail
    const wrongPassword = config.url === '/api/users/me/password' && detail === 'Current password is incorrect.'
    if (error.response?.status !== 401 || wrongPassword || config.url?.startsWith('/api/auth/')) throw error
    if (config.authRetried || !current.refreshToken) {
      session.clear('Your session has expired. Please sign in again.')
      throw error
    }
    config.authRetried = true
    const sentToken = config.headers.get('Authorization')
    // A parallel request may already have refreshed the token while this one was in flight.
    if (!current.accessToken || sentToken === `Bearer ${current.accessToken}`) await session.refresh()
    if (config.signal?.aborted) throw new axios.CanceledError('Request cancelled')
    return apiClient.request(config)
  },
)
