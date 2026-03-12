<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import api from '../api'
import PropertyCard from '../components/PropertyCard.vue'
import { useAuthStore } from '../stores/auth'
import type { PropertyItem } from '../types'

const router = useRouter()
const auth = useAuthStore()
const items = ref<PropertyItem[]>([])

async function load() {
  if (!auth.isTenant) {
    await router.push('/login')
    return
  }
  const { data } = await api.get('/favorites')
  items.value = data.items
}

async function remove(id: number) {
  await api.post('/favorites/toggle', { apartmentId: id })
  await load()
}

onMounted(load)
</script>

<template>
  <div class="container">
    <div class="section-head">
      <div>
        <h1>My Favorites</h1>
        <p class="muted">Сохранённые квартиры арендатора.</p>
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
          <button class="btn btn-danger" type="button" @click="remove(item.apartmentId)">Remove</button>
        </template>
      </PropertyCard>
    </div>
    <div v-else class="panel empty-state">В избранном пока нет квартир.</div>
  </div>
</template>
