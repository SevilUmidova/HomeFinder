<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import api from '../api'
import PropertyCard from '../components/PropertyCard.vue'
import { useAuthStore } from '../stores/auth'
import type { PropertyItem } from '../types'

const auth = useAuthStore()
const router = useRouter()
const items = ref<PropertyItem[]>([])

async function load() {
  if (!auth.isAdmin) {
    await router.push('/login')
    return
  }

  const { data } = await api.get('/admin/apartments')
  items.value = data.items
}

async function remove(id: number) {
  await api.post(`/admin/apartments/${id}/delete`)
  await load()
}

onMounted(load)
</script>

<template>
  <div class="container">
    <div class="section-head">
      <div>
        <h1>Admin Panel</h1>
        <p class="muted">Управление всеми квартирами в системе.</p>
      </div>
    </div>

    <div v-if="items.length" class="property-grid">
      <PropertyCard
        v-for="item in items"
        :key="item.apartmentId"
        :item="item"
        @details="router.push(`/property/${$event}`)"
      >
        <template #actions>
          <button class="btn btn-danger" type="button" @click="remove(item.apartmentId)">Delete</button>
        </template>
      </PropertyCard>
    </div>
    <div v-else class="panel empty-state">Квартиры не найдены.</div>
  </div>
</template>
