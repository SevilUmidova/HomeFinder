<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import api from '../api'
import ReportBarChart from '../components/ReportBarChart.vue'
import { useAuthStore } from '../stores/auth'
import type { MostViewedDistrictsReport } from '../types'

const auth = useAuthStore()
const router = useRouter()
const report = ref<MostViewedDistrictsReport | null>(null)

const filters = reactive({
  top: 5,
  dateFrom: '',
  dateTo: '',
})

const labels = computed(() => report.value?.items.map((x) => x.district) || [])
const values = computed(() => report.value?.items.map((x) => x.totalViews) || [])

async function load() {
  if (!(auth.isLandlord || auth.isAdmin)) {
    await router.push('/login')
    return
  }

  const { data } = await api.get<MostViewedDistrictsReport>('/reports/most-viewed-districts', {
    params: {
      top: filters.top,
      dateFrom: filters.dateFrom || undefined,
      dateTo: filters.dateTo || undefined,
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
        <h1>Most viewed districts</h1>
        <p class="muted">Суммарная популярность районов по просмотрам квартир.</p>
      </div>
    </div>

    <div class="panel" style="margin-bottom: 24px;">
      <div class="form-grid">
        <div class="form-group" style="grid-column: span 4;">
          <label>Top</label>
          <input v-model.number="filters.top" type="number" class="input" />
        </div>
        <div class="form-group" style="grid-column: span 4;">
          <label>Date from</label>
          <input v-model="filters.dateFrom" class="input" placeholder="dd.MM.yyyy" />
        </div>
        <div class="form-group" style="grid-column: span 4;">
          <label>Date to</label>
          <input v-model="filters.dateTo" class="input" placeholder="dd.MM.yyyy" />
        </div>
      </div>
      <div class="action-row" style="margin-top: 16px;">
        <button class="btn btn-primary" type="button" @click="load">Apply</button>
      </div>
    </div>

    <div v-if="report" class="data-grid">
      <ReportBarChart :labels="labels" :values="values" title="Total views" />
      <div class="panel">
        <div v-for="item in report.items" :key="item.district" class="review-item">
          <strong>{{ item.district }}</strong>
          <div class="muted">Просмотров: {{ item.totalViews }} · Квартир: {{ item.apartmentsCount }}</div>
        </div>
      </div>
    </div>
  </div>
</template>
