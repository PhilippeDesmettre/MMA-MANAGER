<script setup>
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useAuth } from '../composables/useAuth'

const router = useRouter()
const { authHeaders, clearSession } = useAuth()

const partie       = ref(null)
const loading      = ref(true)
const showWarning  = ref(false)

const EPOQUES = {
  NoRules:   { label: 'Underground Era', years: '1985 – 1999', icon: '🔥' },
  GoldenAge: { label: 'Golden Age',      years: '2000 – 2012', icon: '🏆' },
  Modern:    { label: 'Modern MMA',      years: '2013 – Auj.', icon: '🧠' },
}

onMounted(async () => {
  try {
    const res = await fetch('http://localhost:5219/api/partie/current', {
      headers: authHeaders()
    })
    if (res.status === 401) { clearSession(); router.push('/auth'); return }
    if (res.ok) partie.value = await res.json()
  } catch { /* pas de partie */ } finally {
    loading.value = false
  }
})

function continuer() {
  router.push('/game')
}

function nouvellePartie() {
  if (partie.value) {
    showWarning.value = true
  } else {
    router.push('/create-trainer')
  }
}

function confirmerNouvellePartie() {
  showWarning.value = false
  router.push('/create-trainer')
}

function logout() {
  clearSession()
  router.push('/auth')
}
</script>

<template>
  <v-app>
    <v-main class="app-shell">
      <v-container class="home-wrap d-flex align-center justify-center">

        <div v-if="loading" class="text-center">
          <v-progress-circular indeterminate color="indigo" size="48" />
        </div>

        <div v-else class="home-panel">

          <!-- Header -->
          <div class="home-top-bar">
            <span></span>
            <v-btn variant="text" size="small" class="logout-btn" @click="logout">
              Déconnexion
            </v-btn>
          </div>

          <!-- Titre -->
          <div class="home-brand">
            <p class="hero-kicker mb-3">v0.1 — Early Access</p>
            <h1 class="home-title">MMA<br>Manager</h1>
            <p class="home-subtitle">
              Incarne un entraîneur, bâtis ton écurie, hisse tes combattants au sommet.
            </p>
          </div>

          <!-- Partie en cours -->
          <div v-if="partie" class="current-game-card mb-6">
            <div class="cg-label">Partie en cours</div>
            <div class="cg-trainer">
              {{ partie.entraineur.prenom }} {{ partie.entraineur.nom }}
            </div>
            <div class="cg-meta">
              <span class="cg-badge">{{ EPOQUES[partie.epoque]?.icon }} {{ EPOQUES[partie.epoque]?.label }}</span>
              <span class="cg-badge green">{{ partie.argent.toLocaleString('fr-FR') }} €</span>
            </div>
            <div class="cg-bg-name">{{ partie.entraineur.backgroundIcone }} {{ partie.entraineur.backgroundNom }}</div>
          </div>

          <!-- Actions -->
          <div class="home-actions">
            <v-btn
              v-if="partie"
              block
              size="x-large"
              rounded="pill"
              class="home-btn-primary mb-3"
              @click="continuer"
            >
              Continuer la partie
            </v-btn>

            <v-btn
              block
              size="large"
              rounded="pill"
              :variant="partie ? 'outlined' : 'elevated'"
              :class="partie ? 'home-btn-secondary' : 'home-btn-primary'"
              @click="nouvellePartie"
            >
              {{ partie ? 'Nouvelle partie' : 'Commencer une nouvelle partie' }}
            </v-btn>
          </div>
        </div>

      </v-container>
    </v-main>

    <!-- Dialog avertissement écrasement -->
    <v-dialog v-model="showWarning" max-width="460">
      <v-card class="warning-dialog" rounded="xl">
        <v-card-item>
          <v-card-title class="text-h6 text-center pt-2">⚠️ Attention</v-card-title>
        </v-card-item>
        <v-card-text class="text-center pb-2">
          Créer une nouvelle partie
          <strong>effacera définitivement</strong> ta partie en cours avec
          <strong>{{ partie?.entraineur?.prenom }} {{ partie?.entraineur?.nom }}</strong>.
          Cette action est irréversible.
        </v-card-text>
        <v-card-actions class="justify-center pb-4 gap-3">
          <v-btn variant="outlined" rounded="pill" @click="showWarning = false">
            Annuler
          </v-btn>
          <v-btn color="error" variant="elevated" rounded="pill" @click="confirmerNouvellePartie">
            Nouvelle partie
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

  </v-app>
</template>
