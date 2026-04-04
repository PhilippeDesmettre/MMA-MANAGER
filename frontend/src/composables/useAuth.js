import { ref, computed } from 'vue'

const TOKEN_KEY = 'mma_token'
const EMAIL_KEY = 'mma_email'

const token = ref(localStorage.getItem(TOKEN_KEY) ?? '')
const email = ref(localStorage.getItem(EMAIL_KEY) ?? '')

export function useAuth() {
  const isAuthenticated = computed(() => !!token.value)

  function setSession(t, e) {
    token.value = t
    email.value = e
    localStorage.setItem(TOKEN_KEY, t)
    localStorage.setItem(EMAIL_KEY, e)
  }

  function clearSession() {
    token.value = ''
    email.value = ''
    localStorage.removeItem(TOKEN_KEY)
    localStorage.removeItem(EMAIL_KEY)
  }

  function authHeaders() {
    return { Authorization: `Bearer ${token.value}` }
  }

  return { token, email, isAuthenticated, setSession, clearSession, authHeaders }
}
