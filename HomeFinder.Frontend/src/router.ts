import { createRouter, createWebHistory } from 'vue-router'

const routes = [
  { path: '/', name: 'home', component: () => import('./pages/HomePage.vue') },
  { path: '/login', name: 'login', component: () => import('./pages/LoginPage.vue') },
  { path: '/property/:id', name: 'property-details', component: () => import('./pages/ApartmentDetailsPage.vue') },
  { path: '/favorites', name: 'favorites', component: () => import('./pages/FavoritesPage.vue') },
  { path: '/appointments', name: 'appointments', component: () => import('./pages/AppointmentsPage.vue') },
  { path: '/schedule/:apartmentId', name: 'schedule', component: () => import('./pages/ScheduleAppointmentPage.vue') },
  { path: '/my-listings', name: 'my-listings', component: () => import('./pages/MyListingsPage.vue') },
  { path: '/my-listings/create', name: 'listing-create', component: () => import('./pages/EditListingPage.vue') },
  { path: '/my-listings/:id/edit', name: 'listing-edit', component: () => import('./pages/EditListingPage.vue') },
  { path: '/premium', name: 'premium', component: () => import('./pages/PremiumPage.vue') },
  { path: '/reports', name: 'reports', component: () => import('./pages/ReportsIndexPage.vue') },
  { path: '/reports/most-viewed-apartments', name: 'report-apartments', component: () => import('./pages/MostViewedApartmentsPage.vue') },
  { path: '/reports/most-viewed-districts', name: 'report-districts', component: () => import('./pages/MostViewedDistrictsPage.vue') },
  { path: '/admin', name: 'admin', component: () => import('./pages/AdminPage.vue') },
]

export const router = createRouter({
  history: createWebHistory('/spa/'),
  routes,
  scrollBehavior() {
    return { top: 0 }
  },
})
