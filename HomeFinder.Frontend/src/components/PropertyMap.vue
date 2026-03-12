<script setup lang="ts">
import { onBeforeUnmount, onMounted, watch } from 'vue'
import L from 'leaflet'
import 'leaflet-draw'
import type { PropertyItem } from '../types'

const LeafletAny = L as any

const props = defineProps<{
  items: PropertyItem[]
}>()

const emit = defineEmits<{
  areaSelected: [polygon: { lat: number; lng: number }[]]
  areaCleared: []
}>()

let map: L.Map | null = null
let markersLayer: L.LayerGroup | null = null
let drawnItems: L.FeatureGroup | null = null

function renderMarkers() {
  if (!map || !markersLayer) return

  markersLayer.clearLayers()

  const valid = props.items.filter((x) => x.latitude && x.longitude)
  valid.forEach((item) => {
    const marker = L.marker([Number(item.latitude), Number(item.longitude)])
    marker.bindPopup(`
      <strong>${item.streetAddress ?? 'Apartment'} ${item.buildingNumber ?? ''}</strong><br/>
      <span>${Number(item.price || 0).toLocaleString()} UZS</span>
    `)
    markersLayer?.addLayer(marker)
  })

  if (valid.length > 0) {
    const bounds = L.latLngBounds(valid.map((x) => [Number(x.latitude), Number(x.longitude)] as [number, number]))
    map.fitBounds(bounds.pad(0.12))
  }
}

onMounted(() => {
  map = L.map('property-map').setView([41.3111, 69.2797], 12)

  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    maxZoom: 19,
  }).addTo(map)

  markersLayer = L.layerGroup().addTo(map)
  drawnItems = new L.FeatureGroup()
  map.addLayer(drawnItems)

  const drawControl = new LeafletAny.Control.Draw({
    edit: { featureGroup: drawnItems },
    draw: {
      polygon: true,
      rectangle: true,
      marker: false,
      polyline: false,
      circle: false,
      circlemarker: false,
    },
  })

  map.addControl(drawControl)

  map.on(LeafletAny.Draw.Event.CREATED, (event: any) => {
    drawnItems?.clearLayers()
    const layer = event.layer
    drawnItems?.addLayer(layer)

    let latLngs = layer.getLatLngs()
    if (Array.isArray(latLngs[0])) {
      latLngs = latLngs[0]
    }

    emit('areaSelected', latLngs.map((point: L.LatLng) => ({ lat: point.lat, lng: point.lng })))
  })

  renderMarkers()
})

watch(() => props.items, () => renderMarkers(), { deep: true })

function clearArea() {
  drawnItems?.clearLayers()
  emit('areaCleared')
}

defineExpose({ clearArea })

onBeforeUnmount(() => {
  map?.remove()
})
</script>

<template>
  <div class="map-shell">
    <div id="property-map" class="map-canvas" />
  </div>
</template>
