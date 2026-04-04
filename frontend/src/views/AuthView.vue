<script setup>
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuth } from '../composables/useAuth'

const apiBase = 'http://localhost:5219/api/auth'
const router  = useRouter()
const { setSession } = useAuth()

const tab      = ref('login')
const email    = ref('')
const password = ref('')
const loading  = ref(false)
const error    = ref('')
const showPass = ref(false)

async function submit() {
  error.value   = ''
  loading.value = true

  const endpoint = tab.value === 'login' ? 'login' : 'register'

  try {
    const res = await fetch(`${apiBase}/${endpoint}`, {
      method:  'POST',
      headers: { 'Content-Type': 'application/json' },
      body:    JSON.stringify({ email: email.value, password: password.value })
    })

    if (!res.ok) {
      const msg = await res.text()
      error.value = msg || 'Une erreur est survenue.'
      return
    }

    const data = await res.json()
    setSession(data.token, data.email)
    router.push('/home')

  } catch {
    error.value = 'Impossible de contacter le serveur.'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <v-app>
    <v-main class="app-shell">
      <v-container class="auth-wrap d-flex align-center justify-center">
        <div class="auth-panel">

          <!-- Logo / titre -->
          <div class="auth-brand">
            <p class="hero-kicker mb-4">MMA Manager</p>
            <h1 class="auth-title">
              {{ tab === 'login' ? 'Connexion' : 'Créer un compte' }}
            </h1>
            <p class="auth-subtitle">
              {{ tab === 'login'
                  ? 'Content de te revoir. Connecte-toi pour continuer.'
                  : 'Rejoins l\'univers MMA Manager.' }}
            </p>
          </div>

          <!-- Tabs -->
          <v-tabs
            v-model="tab"
            class="auth-tabs mb-6"
            color="indigo-lighten-2"
            density="compact"
          >
            <v-tab value="login">Connexion</v-tab>
            <v-tab value="register">Inscription</v-tab>
          </v-tabs>

          <!-- Form -->
          <v-form @submit.prevent="submit">
            <v-text-field
              v-model="email"
              label="Email"
              type="email"
              variant="outlined"
              rounded="lg"
              class="mb-3 auth-field"
              autocomplete="email"
              required
            />

            <v-text-field
              v-model="password"
              label="Mot de passe"
              :type="showPass ? 'text' : 'password'"
              variant="outlined"
              rounded="lg"
              class="mb-5 auth-field"
              :append-inner-icon="showPass ? 'mdi-eye-off' : 'mdi-eye'"
              @click:append-inner="showPass = !showPass"
              autocomplete="current-password"
              required
            />

            <v-alert
              v-if="error"
              type="error"
              variant="tonal"
              density="compact"
              class="mb-4"
              rounded="lg"
            >
              {{ error }}
            </v-alert>

            <v-btn
              type="submit"
              block
              size="large"
              rounded="pill"
              class="auth-btn"
              :loading="loading"
            >
              {{ tab === 'login' ? 'Se connecter' : 'Créer mon compte' }}
            </v-btn>
          </v-form>

          <!-- Switch tab link -->
          <p class="auth-switch mt-5">
            <template v-if="tab === 'login'">
              Pas encore de compte ?
              <a href="#" @click.prevent="tab = 'register'">S'inscrire</a>
            </template>
            <template v-else>
              Déjà un compte ?
              <a href="#" @click.prevent="tab = 'login'">Se connecter</a>
            </template>
          </p>

        </div>
      </v-container>
    </v-main>
  </v-app>
</template>
