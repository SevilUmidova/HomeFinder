<script setup lang="ts">
import { useRouter } from 'vue-router'
import api from '../api'
import { useAuthStore } from '../stores/auth'

const auth = useAuthStore()
const router = useRouter()

async function subscribe() {
  if (!auth.isLandlord) {
    await router.push('/login')
    return
  }

  const { data } = await api.post('/payment/create-premium-checkout')
  window.location.href = data.checkoutUrl
}
</script>

<template>
  <div class="container">
    <div class="auth-wrap">
      <div class="auth-card">
        <h1>Premium subscription</h1>
        <p class="muted">
          Активируй Premium, чтобы публиковать больше объявлений и пользоваться полным landlord‑функционалом.
        </p>
        <div class="action-row" style="margin-top: 18px;">
          <button class="btn btn-primary" type="button" @click="subscribe">Subscribe with Stripe</button>
        </div>
      </div>
    </div>
  </div>
</template>
