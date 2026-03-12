<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import api from '../api'
import { useAuthStore } from '../stores/auth'
import type { AppointmentAddressOption } from '../types'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()

const addresses = ref<AppointmentAddressOption[]>([])
const addressId = ref<number | null>(null)
const dateTime = ref('')
const loading = ref(false)
const error = ref('')

async function load() {
  if (!auth.isTenant) {
    await router.push('/login')
    return
  }

  const { data } = await api.get(`/appointments/options/${route.params.apartmentId}`)
  addresses.value = data.addresses
  addressId.value = addresses.value[0]?.addressId ?? null
}

async function submit() {
  loading.value = true
  error.value = ''
  try {
    await api.post('/appointments', {
      apartmentId: Number(route.params.apartmentId),
      addressId: addressId.value,
      dateTime: dateTime.value,
    })
    await router.push('/appointments')
  } catch (err: any) {
    error.value = err?.response?.data?.message || 'Не удалось записаться.'
  } finally {
    loading.value = false
  }
}

onMounted(load)
</script>

<template>
  <div class="container">
    <div class="section-head">
      <div>
        <h1>Book Appointment</h1>
        <p class="muted">Выбери адрес и удобное время для просмотра.</p>
      </div>
    </div>

    <div class="panel" style="max-width: 720px;">
      <div v-if="error" class="muted" style="color: #b91c1c; margin-bottom: 12px;">{{ error }}</div>
      <div class="form-grid">
        <div class="form-group" style="grid-column: span 12;">
          <label>Address</label>
          <select v-model.number="addressId" class="select">
            <option v-for="item in addresses" :key="item.addressId" :value="item.addressId">
              {{ item.streetAddress }} {{ item.buildingNumber }} · {{ item.city }}
            </option>
          </select>
        </div>
        <div class="form-group" style="grid-column: span 12;">
          <label>Date and time</label>
          <input v-model="dateTime" type="datetime-local" class="input" />
        </div>
      </div>
      <div class="action-row" style="margin-top: 18px;">
        <button class="btn btn-primary" type="button" :disabled="loading" @click="submit">
          {{ loading ? 'Saving...' : 'Book appointment' }}
        </button>
      </div>
    </div>
  </div>
</template>
