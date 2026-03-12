<script setup lang="ts">
import { computed } from 'vue'
import { RouterView } from 'vue-router'
import { useAuthStore } from './stores/auth'
import AppHeader from './components/AppHeader.vue'
import AppFooter from './components/AppFooter.vue'

const auth = useAuthStore()
const loading = computed(() => !auth.initialized)
</script>

<template>
  <div class="page-shell">
    <AppHeader />
    <main class="page-main">
      <div v-if="loading" class="container panel empty-state">Загрузка приложения...</div>
      <RouterView v-else />
    </main>
    <AppFooter />
  </div>
</template>
