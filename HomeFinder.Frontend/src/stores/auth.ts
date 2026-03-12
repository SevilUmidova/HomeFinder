import { defineStore } from 'pinia'
import api, { ensureCsrfToken } from '../api'
import type { SessionUser } from '../types'

const emptyUser = (): SessionUser => ({
  isAuthenticated: false,
  role: null,
  userId: null,
  adminId: null,
  userName: null,
  isPremium: false,
})

export const useAuthStore = defineStore('auth', {
  state: () => ({
    user: emptyUser(),
    initialized: false,
  }),
  getters: {
    isTenant: (state) => state.user.role === 'Tenant',
    isLandlord: (state) => state.user.role === 'Landlord',
    isAdmin: (state) => state.user.role === 'Admin',
  },
  actions: {
    async init() {
      await ensureCsrfToken()
      const { data } = await api.get<SessionUser>('/auth/me')
      this.user = data
      this.initialized = true
    },
    async refresh() {
      const { data } = await api.get<SessionUser>('/auth/me')
      this.user = data
    },
    async login(payload: { login: string; password: string; userType: string }) {
      await ensureCsrfToken()
      const { data } = await api.post('/auth/login', payload)
      await this.refresh()
      return data
    },
    async logout() {
      await ensureCsrfToken()
      await api.post('/auth/logout')
      this.user = emptyUser()
    },
  },
})
