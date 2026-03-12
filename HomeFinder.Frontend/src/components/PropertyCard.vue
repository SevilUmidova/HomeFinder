<script setup lang="ts">
import type { PropertyItem } from '../types'

defineProps<{
  item: PropertyItem
  compact?: boolean
}>()

const emit = defineEmits<{
  details: [id: number]
  action: [id: number]
}>()

function onDetails(id: number) {
  emit('details', id)
}
</script>

<template>
  <article class="property-card">
    <img
      v-if="item.photoPath"
      :src="item.photoPath"
      :alt="item.streetAddress || 'Apartment'"
      class="property-card__image"
    />
    <div v-else class="property-card__image" />

    <div class="property-card__content">
      <div class="property-card__price">{{ Number(item.price || 0).toLocaleString() }} UZS</div>
      <h3 style="margin: 14px 0 6px;">
        {{ item.streetAddress || 'Apartment' }} {{ item.buildingNumber || '' }}
      </h3>
      <div class="muted">
        {{ [item.district, item.city].filter(Boolean).join(', ') || 'Address not specified' }}
      </div>

      <div class="property-card__meta">
        <span class="chip">Size: {{ item.size }} m²</span>
        <span class="chip">Rooms: {{ item.rooms }}</span>
        <span class="chip">Rating: {{ item.averageRating?.toFixed?.(1) ?? item.averageRating }}</span>
      </div>

      <p class="muted" style="margin: 16px 0 18px;">
        {{ item.description?.length ? item.description.slice(0, 90) + (item.description.length > 90 ? '...' : '') : 'No description available.' }}
      </p>

      <div class="action-row">
        <button class="btn btn-primary" type="button" @click="onDetails(item.apartmentId)">View Details</button>
        <slot name="actions" />
      </div>
    </div>
  </article>
</template>
