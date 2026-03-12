<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import api from '../api'
import { useAuthStore } from '../stores/auth'
import type { AppointmentItem } from '../types'

const auth = useAuthStore()
const router = useRouter()
const items = ref<AppointmentItem[]>([])
const role = ref<string>('')

async function load() {
  if (!auth.user.isAuthenticated) {
    await router.push('/login')
    return
  }

  const { data } = await api.get('/appointments')
  items.value = data.items
  role.value = data.role
}

async function cancelAppointment(id: number) {
  await api.post(`/appointments/${id}/cancel`)
  await load()
}

onMounted(load)
</script>

<template>
  <div class="container">
    <div class="section-head">
      <div>
        <h1>Appointments</h1>
        <p class="muted">{{ role === 'Landlord' ? 'Входящие записи арендаторов.' : 'Ваши записи на просмотр.' }}</p>
      </div>
    </div>

    <div v-if="items.length" class="property-grid">
      <article v-for="item in items" :key="item.appointmentId" class="panel">
        <h3>{{ item.apartmentTitle || `Apartment #${item.apartmentId}` }}</h3>
        <p class="muted">{{ [item.address, item.district, item.city].filter(Boolean).join(', ') }}</p>
        <p><strong>{{ item.dateTime ? new Date(item.dateTime).toLocaleString() : 'Pending' }}</strong></p>
        <p class="muted">{{ item.phoneNumber }}</p>

        <div class="action-row">
          <button
            v-if="role !== 'Landlord'"
            class="btn btn-danger"
            type="button"
            @click="cancelAppointment(item.appointmentId)"
          >
            Cancel
          </button>
          <button
            v-if="item.apartmentId"
            class="btn btn-outline"
            type="button"
            @click="router.push(`/property/${item.apartmentId}`)"
          >
            Open property
          </button>
        </div>
      </article>
    </div>
    <div v-else class="panel empty-state">Нет активных записей.</div>
  </div>
</template>
