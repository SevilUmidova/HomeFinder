<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import api from '../api'
import { useAuthStore } from '../stores/auth'
import type { PropertyDetails } from '../types'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const apartment = ref<PropertyDetails | null>(null)
const loading = ref(false)
const review = reactive({ rating: 5, comment: '' })

const canReview = computed(() => auth.isTenant && apartment.value)

async function load() {
  loading.value = true
  try {
    const { data } = await api.get<PropertyDetails>(`/apartments/${route.params.id}`)
    apartment.value = data
  } finally {
    loading.value = false
  }
}

async function toggleFavorite() {
  if (!auth.isTenant || !apartment.value) {
    await router.push('/login')
    return
  }

  const { data } = await api.post('/favorites/toggle', { apartmentId: apartment.value.apartmentId })
  apartment.value.isFavorited = data.favorited
}

async function saveReview() {
  if (!apartment.value) return
  await api.post('/reviews', {
    apartmentId: apartment.value.apartmentId,
    rating: review.rating,
    comment: review.comment,
  })
  review.comment = ''
  await load()
}

onMounted(load)
</script>

<template>
  <div class="container">
    <div v-if="loading" class="panel empty-state">Загрузка квартиры...</div>
    <template v-else-if="apartment">
      <div class="section-head">
        <div>
          <h1>{{ apartment.streetAddress }} {{ apartment.buildingNumber }}</h1>
          <p class="muted">{{ [apartment.district, apartment.city, apartment.region].filter(Boolean).join(', ') }}</p>
        </div>
        <div class="action-row">
          <button class="btn btn-outline" type="button" @click="router.push('/')">Back</button>
          <button class="btn btn-danger" type="button" @click="toggleFavorite">
            {{ apartment.isFavorited ? 'Remove from favorites' : 'Add to favorites' }}
          </button>
          <button class="btn btn-primary" type="button" @click="router.push(`/schedule/${apartment.apartmentId}`)">Book appointment</button>
        </div>
      </div>

      <section class="split-details">
        <div class="gallery-grid panel">
          <img v-for="photo in apartment.photoPaths" :key="photo" :src="photo" alt="Apartment photo" />
          <div v-if="!apartment.photoPaths.length" class="gallery-placeholder" />
        </div>

        <div class="panel">
          <div class="stats-grid">
            <div class="stat-tile">
              <span class="muted">Price</span>
              <strong>{{ Number(apartment.price).toLocaleString() }} UZS</strong>
            </div>
            <div class="stat-tile">
              <span class="muted">Rooms</span>
              <strong>{{ apartment.rooms }}</strong>
            </div>
            <div class="stat-tile">
              <span class="muted">Size</span>
              <strong>{{ apartment.size }} m²</strong>
            </div>
            <div class="stat-tile">
              <span class="muted">Views</span>
              <strong>{{ apartment.views }}</strong>
            </div>
          </div>

          <div style="margin-top: 24px;">
            <h3>Description</h3>
            <p class="muted">{{ apartment.description || 'No description' }}</p>
          </div>

          <div style="margin-top: 24px;">
            <h3>Owner</h3>
            <p class="muted">{{ apartment.landlordName }}</p>
            <p class="muted">{{ apartment.phoneNumber }}</p>
          </div>
        </div>
      </section>

      <section class="panel" style="margin-top: 24px;">
        <div class="section-head">
          <div>
            <h2>Reviews</h2>
            <p class="muted">Rating: {{ apartment.averageRating.toFixed(1) }} · {{ apartment.reviewCount }} reviews</p>
          </div>
        </div>

        <div v-if="canReview" class="form-grid" style="margin-bottom: 24px;">
          <div class="form-group" style="grid-column: span 3;">
            <label>Rating</label>
            <select v-model.number="review.rating" class="select">
              <option :value="5">5</option>
              <option :value="4">4</option>
              <option :value="3">3</option>
              <option :value="2">2</option>
              <option :value="1">1</option>
            </select>
          </div>
          <div class="form-group" style="grid-column: span 9;">
            <label>Comment</label>
            <textarea v-model="review.comment" class="textarea" />
          </div>
          <div style="grid-column: span 12;">
            <button class="btn btn-primary" type="button" @click="saveReview">Save review</button>
          </div>
        </div>

        <div v-if="apartment.reviews.length">
          <article v-for="item in apartment.reviews" :key="item.reviewId" class="review-item">
            <strong>{{ item.userName }}</strong>
            <div class="muted">Rating: {{ item.rating }} · {{ item.createdAt ? new Date(item.createdAt).toLocaleDateString() : '' }}</div>
            <p>{{ item.comment }}</p>
          </article>
        </div>
        <div v-else class="empty-state">Пока нет отзывов.</div>
      </section>
    </template>
  </div>
</template>
