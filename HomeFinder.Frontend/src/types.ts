export type UserRole = 'Tenant' | 'Landlord' | 'Admin' | null

export interface SessionUser {
  isAuthenticated: boolean
  role: UserRole
  userId: number | null
  adminId: number | null
  userName: string | null
  isPremium: boolean
}

export interface CatalogFilters {
  priceMin?: number | null
  priceMax?: number | null
  sizeMin?: number | null
  sizeMax?: number | null
  rooms?: number | null
  city?: string
  district?: string
  address?: string
  sortBy?: string
  alltext?: string
}

export interface PropertyItem {
  apartmentId: number
  description?: string | null
  price: number
  size: number
  rooms: number
  streetAddress?: string | null
  buildingNumber?: string | null
  district?: string | null
  city?: string | null
  latitude?: number | null
  longitude?: number | null
  photoPath?: string | null
  landlordName?: string | null
  phoneNumber?: string | null
  averageRating: number
  reviewCount: number
}

export interface ReviewItem {
  reviewId: number
  userName: string
  rating: number
  comment?: string | null
  createdAt?: string | null
}

export interface PropertyDetails {
  apartmentId: number
  description?: string | null
  price: number
  size: number
  rooms: number
  views: number
  streetAddress?: string | null
  buildingNumber?: string | null
  apartmentNumber?: string | null
  district?: string | null
  city?: string | null
  region?: string | null
  latitude?: number | null
  longitude?: number | null
  photoPaths: string[]
  landlordName?: string | null
  phoneNumber?: string | null
  averageRating: number
  reviewCount: number
  isFavorited: boolean
  reviews: ReviewItem[]
}

export interface AppointmentAddressOption {
  addressId: number
  streetAddress?: string | null
  buildingNumber?: string | null
  city?: string | null
  district?: string | null
}

export interface AppointmentItem {
  appointmentId: number
  apartmentId?: number | null
  apartmentTitle?: string | null
  address?: string | null
  city?: string | null
  district?: string | null
  dateTime?: string | null
  phoneNumber?: string | null
}

export interface ApartmentFormPayload {
  apartmentId?: number
  description: string
  price: number | null
  size: number | null
  rooms: number | null
  streetAddress: string
  buildingNumber: string
  apartmentNumber: string
  district: string
  city: string
  region: string
  latitude: string
  longitude: string
  photoPaths?: string[]
}

export interface MostViewedApartmentReportRow {
  apartmentId: number
  views: number
  price?: number | null
  district?: string | null
  city?: string | null
  streetAddress?: string | null
  buildingNumber?: string | null
  photoPath?: string | null
}

export interface MostViewedApartmentsReport {
  top: number
  dateFrom: string
  dateTo: string
  items: MostViewedApartmentReportRow[]
}

export interface MostViewedDistrictReportRow {
  district: string
  totalViews: number
  apartmentsCount: number
}

export interface MostViewedDistrictsReport {
  top: number
  dateFrom: string
  dateTo: string
  items: MostViewedDistrictReportRow[]
}
