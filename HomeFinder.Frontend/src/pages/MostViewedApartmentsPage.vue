<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import api from '../api'
import PropertyCard from '../components/PropertyCard.vue'
import ReportBarChart from '../components/ReportBarChart.vue'
import { useAuthStore } from '../stores/auth'
import type { MostViewedApartmentsReport } from '../types'

const auth = useAuthStore()
const router = useRouter()
const report = ref<MostViewedApartmentsReport | null>(null)

const filters = reactive({
  top: 5,
  dateFrom: '',
  dateTo: '',
  priceMin: '',
  priceMax: '',
  district: '',
})

const districtOptions = computed(() => {
  const values = report.value?.items
    .map((x) => x.district)
    .filter((x): x is string => Boolean(x))
  return Array.from(new Set(values || [])).sort()
})

const filteredItems = computed(() => {
  const items = report.value?.items || []
  return filters.district ? items.filter((x) => x.district === filters.district) : items
})

const chartLabels = computed(() => filteredItems.value.map((x) => `${x.apartmentId}${x.district ? ` • ${x.district}` : ''}`))
const chartValues = computed(() => filteredItems.value.map((x) => x.views))

async function load() {
  if (!(auth.isLandlord || auth.isAdmin)) {
    await router.push('/login')
    return
  }

  const { data } = await api.get<MostViewedApartmentsReport>('/reports/most-viewed-apartments', {
    params: {
      top: filters.top,
      dateFrom: filters.dateFrom || undefined,
      dateTo: filters.dateTo || undefined,
      priceMin: filters.priceMin || undefined,
      priceMax: filters.priceMax || undefined,
    },
  })

  report.value = data
}

onMounted(() => {
  const today = new Date()
  const lastMonth = new Date()
  lastMonth.setMonth(lastMonth.getMonth() - 1)
  filters.dateTo = today.toLocaleDateString('ru-RU')
  filters.dateFrom = lastMonth.toLocaleDateString('ru-RU')
  load()
})
</script>

<template>
  <div class="container">
    <div class="section-head">
      <div>
        <h1>Most viewed apartments</h1>
        <p class="muted">Отчёт по просмотрам квартир с фильтрами по периоду, району и цене.</p>
      </div>
    </div>

    <div class="panel" style="margin-bottom: 24px;">
      <div class="form-grid">
        <div class="form-group" style="grid-column: span 2;">
          <label>Top</label>
          <input v-model.number="filters.top" type="number" class="input" />
        </div>
        <div class="form-group" style="grid-column: span 2;">
          <label>Date from</label>
          <input v-model="filters.dateFrom" class="input" placeholder="dd.MM.yyyy" />
        </div>
        <div class="form-group" style="grid-column: span 2;">
          <label>Date to</label>
          <input v-model="filters.dateTo" class="input" placeholder="dd.MM.yyyy" />
        </div>
        <div class="form-group" style="grid-column: span 2;">
          <label>Price min</label>
          <input v-model="filters.priceMin" type="number" class="input" />
        </div>
        <div class="form-group" style="grid-column: span 2;">
          <label>Price max</label>
          <input v-model="filters.priceMax" type="number" class="input" />
        </div>
        <div class="form-group" style="grid-column: span 2;">
          <label>District</label>
          <select v-model="filters.district" class="select">
            <option value="">All districts</option>
            <option v-for="item in districtOptions" :key="item" :value="item">{{ item }}</option>
          </select>
        </div>
      </div>
      <div class="action-row" style="margin-top: 16px;">
        <button class="btn btn-primary" type="button" @click="load">Apply</button>
      </div>
    </div>

    <div v-if="report" class="data-grid">
      <ReportBarChart :labels="chartLabels" :values="chartValues" title="Views" />
      <div class="property-grid" style="grid-template-columns: repeat(2, minmax(0, 1fr));">
        <PropertyCard
          v-for="item in filteredItems"
          :key="item.apartmentId"
          :item="{
            apartmentId: item.apartmentId,
            description: '',
            price: item.price || 0,
            size: 0,
            rooms: 0,
            streetAddress: item.streetAddress,
            buildingNumber: item.buildingNumber,
            district: item.district,
            city: item.city,
            photoPath: item.photoPath,
            averageRating: 0,
            reviewCount: 0,
          }"
          @details="router.push(`/property/${$event}`)"
        >
          <template #actions>
            <span class="chip">Просмотров: {{ item.views }}</span>
          </template>
        </PropertyCard>
      </div>
    </div>
  </div>
</template>
