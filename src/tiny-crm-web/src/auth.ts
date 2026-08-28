import { reactive } from 'vue'
import { api, ApiError } from './api/client'

export interface User { id: number; username: string; displayName: string }

export const state = reactive<{ user: User | null; ready: boolean }>({ user: null, ready: false })

export function useAuth() {
  return {
    state,
    async refresh() {
      try {
        state.user = await api<User>('/api/auth/me')
      } catch {
        state.user = null
      } finally {
        state.ready = true
      }
    },
    async login(username: string, password: string): Promise<string | null> {
      try {
        state.user = await api<User>('/api/auth/login', {
          method: 'POST',
          body: JSON.stringify({ username, password }),
        })
        return null
      } catch (e) {
        if (e instanceof ApiError && e.status === 401) return 'Invalid username or password.'
        return 'Sign-in failed. Please try again.'
      }
    },
    async logout() {
      await api<void>('/api/auth/logout', { method: 'POST' })
      state.user = null
    },
  }
}
