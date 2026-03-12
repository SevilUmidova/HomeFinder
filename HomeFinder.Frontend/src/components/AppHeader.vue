<script setup lang="ts">
import { computed } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth'

const auth = useAuthStore()
const router = useRouter()

const links = computed(() => {
  const base = [{ to: '/', label: 'Catalog' }]

  if (auth.isTenant) {
    base.push({ to: '/favorites', label: 'Favorites' })
    base.push({ to: '/appointments', label: 'Appointments' })
  }

  if (auth.isLandlord) {
    base.push({ to: '/my-listings', label: 'My Listings' })
    base.push({ to: '/appointments', label: 'Appointments' })
    base.push({ to: '/reports', label: 'Reports' })
    base.push({ to: '/premium', label: 'Premium' })
  }

  if (auth.isAdmin) {
    base.push({ to: '/admin', label: 'Admin' })
    base.push({ to: '/reports', label: 'Reports' })
  }

  return base
})

async function logout() {
  await auth.logout()
  await router.push('/login')
}
</script>

<template>
  <header class="site-header">
    <div class="container site-header__row">
      <RouterLink to="/" class="brand">
        <span class="brand__badge">HF</span>
        <span>Home Finder</span>
      </RouterLink>

      <nav class="nav-links">
        <RouterLink
          v-for="link in links"
          :key="link.to"
          :to="link.to"
          class="nav-link"
        >
          {{ link.label }}
        </RouterLink>

        <span v-if="auth.user.isPremium" class="premium-pill">Premium</span>

        <template v-if="auth.user.isAuthenticated">
          <button class="nav-link nav-link--danger nav-link-button" type="button" @click="logout">
            Logout
          </button>
        </template>
        <RouterLink v-else to="/login" class="btn btn-primary">Sign In</RouterLink>
      </nav>
    </div>
  </header>
</template>
