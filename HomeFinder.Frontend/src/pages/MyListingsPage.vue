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
const canAddApartment = ref(false)

async function load() {
  if (!auth.isLandlord) {
    await router.push('/login')
    return
  }

  const { data } = await api.get('/landlord/apartments')
  items.value = data.items
  canAddApartment.value = data.canAddApartment
}

async function remove(id: number) {
  await api.post(`/landlord/apartments/${id}/delete`)
  await load()
}

onMounted(load)
</script>

<template>
  <div class="container">
    <div class="section-head">
      <div>
        <h1>My Listings</h1>
        <p class="muted">Управление объявлениями владельца.</p>
      </div>
      <button
        class="btn btn-primary"
        type="button"
        :disabled="!canAddApartment"
        @click="router.push('/my-listings/create')"
      >
        Add listing
      </button>
    </div>

    <div v-if="!canAddApartment" class="panel" style="margin-bottom: 20px;">
      Для публикации более одной квартиры нужен Premium.
    </div>

    <div v-if="items.length" class="property-grid">
      <PropertyCard
        v-for="item in items"
        :key="item.apartmentId"
        :item="item"
        @details="router.push(`/property/${$event}`)"
      >
        <template #actions>
          <button class="btn btn-outline" type="button" @click="router.push(`/my-listings/${item.apartmentId}/edit`)">Edit</button>
          <button class="btn btn-danger" type="button" @click="remove(item.apartmentId)">Delete</button>
        </template>
      </PropertyCard>
    </div>
    <div v-else class="panel empty-state">У вас пока нет опубликованных квартир.</div>
  </div>
</template>
