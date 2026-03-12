<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import api from '../api'
import ApartmentForm from '../components/ApartmentForm.vue'
import { useAuthStore } from '../stores/auth'
import type { ApartmentFormPayload } from '../types'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const saving = ref(false)

const form = reactive<ApartmentFormPayload>({
  description: '',
  price: null,
  size: null,
  rooms: null,
  streetAddress: '',
  buildingNumber: '',
  apartmentNumber: '',
  district: '',
  city: 'Tashkent',
  region: '',
  latitude: '41.311081',
  longitude: '69.240562',
  photoPaths: [],
})

const isEdit = computed(() => Boolean(route.params.id))

async function load() {
  if (!auth.isLandlord) {
    await router.push('/login')
    return
  }

  if (!isEdit.value) return

  const { data } = await api.get(`/landlord/apartments/${route.params.id}`)
  Object.assign(form, {
    apartmentId: data.apartmentId,
    description: data.description || '',
    price: data.price,
    size: data.size,
    rooms: data.rooms,
    streetAddress: data.streetAddress || '',
    buildingNumber: data.buildingNumber || '',
    apartmentNumber: data.apartmentNumber || '',
    district: data.district || '',
    city: data.city || 'Tashkent',
    region: data.region || '',
    latitude: String(data.latitude || '41.311081'),
    longitude: String(data.longitude || '69.240562'),
    photoPaths: data.photoPaths || [],
  })
}

async function submit(payload: ApartmentFormPayload, files: File[]) {
  saving.value = true
  try {
    const body = new FormData()
    Object.entries(payload).forEach(([key, value]) => {
      if (value !== undefined && value !== null && key !== 'photoPaths' && key !== 'apartmentId') {
        body.append(key, String(value))
      }
    })

    files.forEach((file) => body.append('Photos', file))

    if (isEdit.value) {
      await api.post(`/landlord/apartments/${route.params.id}/update`, body)
    } else {
      await api.post('/landlord/apartments', body)
    }

    await router.push('/my-listings')
  } finally {
    saving.value = false
  }
}

onMounted(load)
</script>

<template>
  <div class="container">
    <div class="section-head">
      <div>
        <h1>{{ isEdit ? 'Edit listing' : 'Create listing' }}</h1>
        <p class="muted">Форма создания и редактирования квартиры с картой и фото.</p>
      </div>
    </div>

    <ApartmentForm
      :initial="form"
      :submit-label="isEdit ? 'Save changes' : 'Create listing'"
      :busy="saving"
      @submit="submit"
    />
  </div>
</template>
