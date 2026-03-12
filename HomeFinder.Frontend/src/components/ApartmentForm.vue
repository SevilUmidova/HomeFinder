<script setup lang="ts">
import { nextTick, onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue'
import L from 'leaflet'
import api from '../api'
import type { ApartmentFormPayload } from '../types'

const props = defineProps<{
  initial: ApartmentFormPayload
  submitLabel: string
  busy?: boolean
}>()

const emit = defineEmits<{
  submit: [payload: ApartmentFormPayload, files: File[]]
}>()

const form = reactive<ApartmentFormPayload>({ ...props.initial })
const selectedFiles = ref<File[]>([])
let map: L.Map | null = null
let marker: L.Marker | null = null

watch(
  () => props.initial,
  (value) => Object.assign(form, value),
  { deep: true },
)

function initMap() {
  if (map) return

  const lat = Number(form.latitude || 41.311081)
  const lng = Number(form.longitude || 69.240562)

  map = L.map('listing-map').setView([lat, lng], 12)
  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    maxZoom: 19,
  }).addTo(map)

  marker = L.marker([lat, lng], { draggable: true }).addTo(map)

  map.on('click', async (event: L.LeafletMouseEvent) => {
    updateCoords(event.latlng.lat, event.latlng.lng)
    await reverseGeocode(event.latlng.lat, event.latlng.lng)
  })

  marker.on('dragend', async (event: any) => {
    const latlng = event.target.getLatLng()
    updateCoords(latlng.lat, latlng.lng)
    await reverseGeocode(latlng.lat, latlng.lng)
  })
}

function updateCoords(lat: number, lng: number) {
  form.latitude = lat.toFixed(6)
  form.longitude = lng.toFixed(6)
  marker?.setLatLng([lat, lng])
  map?.setView([lat, lng], 16)
}

async function searchAddress() {
  const query = [form.streetAddress, form.district, form.city].filter(Boolean).join(', ')
  if (!query.trim()) return

  const { data } = await api.get('/http/search', { params: { str: query } })
  if (data?.[0]) {
    updateCoords(Number(data[0].lat), Number(data[0].lon))
    form.streetAddress = data[0].address?.road || form.streetAddress
    form.district = data[0].address?.suburb || data[0].address?.neighbourhood || form.district
    form.city = data[0].address?.city || data[0].address?.town || form.city || 'Tashkent'
  }
}

async function reverseGeocode(lat: number, lng: number) {
  const { data } = await api.get('/http/reverse', { params: { lat, lon: lng } })
  form.streetAddress = data.address?.road || data.address?.pedestrian || data.address?.residential || form.streetAddress
  form.buildingNumber = data.address?.house_number || form.buildingNumber
  form.district = data.address?.suburb || data.address?.neighbourhood || data.address?.district || form.district
  form.city = data.address?.city || data.address?.town || data.address?.municipality || form.city || 'Tashkent'
  form.region = data.address?.state || data.address?.region || form.region
}

function handleFiles(event: Event) {
  const target = event.target as HTMLInputElement
  selectedFiles.value = Array.from(target.files || [])
}

function submitForm() {
  emit('submit', { ...form }, selectedFiles.value)
}

onMounted(async () => {
  await nextTick()
  initMap()
})

onBeforeUnmount(() => {
  map?.remove()
})
</script>

<template>
  <div class="panel">
    <div class="form-grid">
      <div class="form-group" style="grid-column: span 12;">
        <label>Description</label>
        <textarea v-model="form.description" class="textarea" />
      </div>

      <div class="form-group" style="grid-column: span 4;">
        <label>Price</label>
        <input v-model.number="form.price" type="number" class="input" />
      </div>
      <div class="form-group" style="grid-column: span 4;">
        <label>Size</label>
        <input v-model.number="form.size" type="number" class="input" />
      </div>
      <div class="form-group" style="grid-column: span 4;">
        <label>Rooms</label>
        <input v-model.number="form.rooms" type="number" class="input" />
      </div>

      <div class="form-group" style="grid-column: span 4;">
        <label>Street</label>
        <input v-model="form.streetAddress" class="input" />
      </div>
      <div class="form-group" style="grid-column: span 4;">
        <label>Building</label>
        <input v-model="form.buildingNumber" class="input" />
      </div>
      <div class="form-group" style="grid-column: span 4;">
        <label>Apartment</label>
        <input v-model="form.apartmentNumber" class="input" />
      </div>

      <div class="form-group" style="grid-column: span 4;">
        <label>District</label>
        <input v-model="form.district" class="input" />
      </div>
      <div class="form-group" style="grid-column: span 4;">
        <label>City</label>
        <input v-model="form.city" class="input" />
      </div>
      <div class="form-group" style="grid-column: span 4;">
        <label>Region</label>
        <input v-model="form.region" class="input" />
      </div>

      <div class="form-group" style="grid-column: span 6;">
        <label>Latitude</label>
        <input v-model="form.latitude" class="input" />
      </div>
      <div class="form-group" style="grid-column: span 6;">
        <label>Longitude</label>
        <input v-model="form.longitude" class="input" />
      </div>

      <div class="form-group" style="grid-column: span 12;">
        <div class="action-row">
          <button type="button" class="btn btn-outline" @click="searchAddress">Search address</button>
        </div>
      </div>

      <div class="form-group" style="grid-column: span 12;">
        <div class="map-shell">
          <div id="listing-map" class="map-canvas" style="height: 360px;" />
        </div>
      </div>

      <div class="form-group" style="grid-column: span 12;">
        <label>Photos</label>
        <input type="file" multiple accept=".jpg,.jpeg,.png" class="input" @change="handleFiles" />
      </div>
    </div>

    <div class="action-row" style="margin-top: 20px;">
      <button type="button" class="btn btn-primary" :disabled="busy" @click="submitForm">
        {{ submitLabel }}
      </button>
    </div>
  </div>
</template>
