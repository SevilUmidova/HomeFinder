import axios from 'axios'

const api = axios.create({
  baseURL: '/api',
  withCredentials: true,
})

let csrfToken: string | null = null

export async function ensureCsrfToken() {
  if (csrfToken) return csrfToken

  const response = await api.get('/security/csrf')
  csrfToken = response.data.requestToken
  api.defaults.headers.common.RequestVerificationToken = csrfToken
  return csrfToken
}

export function setCsrfToken(token: string) {
  csrfToken = token
  api.defaults.headers.common.RequestVerificationToken = token
}

api.interceptors.response.use(
  (response) => response,
  async (error) => {
    if (error?.response?.status === 400 && !csrfToken) {
      await ensureCsrfToken()
      return api.request(error.config)
    }
    return Promise.reject(error)
  },
)

export default api
