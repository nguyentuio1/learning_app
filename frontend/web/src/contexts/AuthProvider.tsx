import { useEffect, useSyncExternalStore, type ReactNode } from 'react'
import { session } from '../services/authSession'
import { AuthContext } from './authContext'

export function AuthProvider({ children }: { children: ReactNode }) {
  const snapshot = useSyncExternalStore(session.subscribe, session.getSnapshot)
  useEffect(() => { void session.initialize() }, [])
  return <AuthContext.Provider value={{ ...snapshot, login: session.login, logout: session.logout, updateProfile: session.updateProfile }}>
    {children}
  </AuthContext.Provider>
}
