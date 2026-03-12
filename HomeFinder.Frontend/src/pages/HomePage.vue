<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import api from '../api'
import PropertyCard from '../components/PropertyCard.vue'
import PropertyMap from '../components/PropertyMap.vue'
import type { CatalogFilters, PropertyItem } from '../types'

const router = useRouter()
const mapRef = ref<InstanceType<typeof PropertyMap> | null>(null)
const items = ref<PropertyItem[]>([])
const loading = ref(false)
const areaFiltered = ref(false)

const filters = reactive<CatalogFilters>({
  priceMin: null,
  priceMax: null,
  sizeMin: null,
  sizeMax: null,
  rooms: null,
  city: '',
  district: '',
  address: '',
  sortBy: 'rating',
  alltext: '',
})

const hasItems = computed(() => items.value.length > 0)

async function loadCatalog() {
  loading.value = true
  try {
    const { data } = await api.get('/catalog', { params: filters })
    items.value = data.items
    areaFiltered.value = false
  } finally {
    loading.value = false
  }
}

async function filterByArea(polygon: { lat: number; lng: number }[]) {
  loading.value = true
  try {
    const { data } = await api.post('/catalog/area', { polygon })
    items.value = data.items
    areaFiltered.value = true
  } finally {
    loading.value = false
  }
}

function resetArea() {
  areaFiltered.value = false
  loadCatalog()
}

function clearFilters() {
  Object.assign(filters, {
    priceMin: null,
    priceMax: null,
    sizeMin: null,
    sizeMax: null,
    rooms: null,
    city: '',
    district: '',
    address: '',
    sortBy: 'rating',
    alltext: '',
  })
  loadCatalog()
}

onMounted(loadCatalog)
</script>

<template>
  <div class="container">
    <section class="hero">
      <div class="hero__content">
        <h1>Find the right apartment with map search, live filters and smart reports.</h1>
        <p>
          Полный SPA-каталог недвижимости: фильтрация, карта, отзывы, избранное, кабинеты и аналитика в едином современном интерфейсе.
        </p>
      </div>
      <div class="glass-card panel">
        <div class="form-grid">
          <div class="form-group" style="grid-column: span 6;">
            <label>Price from</label>
            <input v-model.number="filters.priceMin" type="number" class="input" />
          </div>
          <div class="form-group" style="grid-column: span 6;">
            <label>Price to</label>
            <input v-model.number="filters.priceMax" type="number" class="input" />
          </div>
          <div class="form-group" style="grid-column: span 6;">
            <label>City</label>
            <input v-model="filters.city" class="input" />
          </div>
          <div class="form-group" style="grid-column: span 6;">
            <label>District</label>
            <input v-model="filters.district" class="input" />
          </div>
          <div class="form-group" style="grid-column: span 12;">
            <label>Search</label>
            <input v-model="filters.alltext" class="input" placeholder="street, owner, text..." />
          </div>
        </div>

        <div class="action-row" style="margin-top: 18px;">
          <button class="btn btn-primary" type="button" @click="loadCatalog">Search</button>
          <button class="btn btn-secondary" type="button" @click="clearFilters">Clear</button>
        </div>
      </div>
    </section>

    <section class="page-main" style="padding: 28px 0 0;">
      <div class="section-head">
        <div>
          <h2>Map selection</h2>
          <p class="muted">Нарисуй прямоугольник или полигон на карте. В списке останутся только найденные квартиры.</p>
        </div>
        <button v-if="areaFiltered" class="btn btn-outline" type="button" @click="mapRef?.clearArea()">Clear area</button>
      </div>
      <PropertyMap ref="mapRef" :items="items" @area-selected="filterByArea" @area-cleared="resetArea" />
    </section>

    <section class="page-section">
      <div class="section-head">
        <div>
          <h2>Available properties</h2>
          <p class="muted">{{ loading ? 'Loading...' : `Найдено объектов: ${items.length}` }}</p>
        </div>
      </div>

      <div v-if="!loading && hasItems" class="property-grid">
        <PropertyCard
          v-for="item in items"
          :key="item.apartmentId"
          :item="item"
          @details="router.push(`/property/${$event}`)"
        />
      </div>
      <div v-else-if="loading" class="panel empty-state">Загрузка каталога...</div>
      <div v-else class="panel empty-state">Квартиры по заданным условиям не найдены.</div>
    </section>
  </div>
</template>
