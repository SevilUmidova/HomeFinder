<script setup lang="ts">
import { reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth'

const router = useRouter()
const auth = useAuthStore()
const loading = ref(false)
const error = ref('')

const form = reactive({
  login: '',
  password: '',
  userType: 'tenant',
})

async function submit() {
  loading.value = true
  error.value = ''
  try {
    const result = await auth.login(form)
    await router.push(result.redirectTo || '/')
  } catch (err: any) {
    error.value = err?.response?.data?.message || 'Не удалось выполнить вход.'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="container auth-wrap">
    <div class="auth-card">
      <div class="section-head" style="margin-bottom: 8px;">
        <div>
          <h1>Welcome back</h1>
          <p class="muted">Вход в tenant, landlord и admin кабинеты.</p>
        </div>
      </div>

      <div v-if="error" class="panel" style="margin-bottom: 16px; border-color: #fecaca; color: #b91c1c;">
        {{ error }}
      </div>

      <div class="form-grid">
        <div class="form-group" style="grid-column: span 12;">
          <label>User type</label>
          <select v-model="form.userType" class="select">
            <option value="tenant">Tenant</option>
            <option value="landlord">Landlord</option>
            <option value="admin">Admin</option>
          </select>
        </div>
        <div class="form-group" style="grid-column: span 12;">
          <label>Login</label>
          <input v-model="form.login" class="input" />
        </div>
        <div class="form-group" style="grid-column: span 12;">
          <label>Password</label>
          <input v-model="form.password" type="password" class="input" />
        </div>
      </div>

      <div class="action-row" style="margin-top: 18px;">
        <button class="btn btn-primary" type="button" :disabled="loading" @click="submit">
          {{ loading ? 'Signing in...' : 'Sign In' }}
        </button>
      </div>
    </div>
  </div>
</template>
