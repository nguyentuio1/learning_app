import { createContext } from 'react'
import type { session } from '../services/authSession'

type AuthContextValue = ReturnType<typeof session.getSnapshot> & {
  login: typeof session.login
  logout: typeof session.logout
  updateProfile: typeof session.updateProfile
}
export const AuthContext = createContext<AuthContextValue | null>(null)
