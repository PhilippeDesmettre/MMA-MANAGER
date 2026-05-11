<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useAuth } from '../composables/useAuth'

const router = useRouter()
const { authHeaders, clearSession } = useAuth()

const partie       = ref(null)
const parties      = ref([])
const loading      = ref(true)
const showWarning  = ref(false)
const showCharger  = ref(false)
const chargingId   = ref(null)

const EPOQUES = {
  NoRules:   { label: 'Underground Era', years: '1993 – 1999', icon: '🔥', color: '#ef4444' },
  GoldenAge: { label: 'Golden Age',      years: '2000 – 2012', icon: '🏆', color: '#f59e0b' },
  Modern:    { label: 'Modern MMA',      years: '2013 – Auj.', icon: '🧠', color: '#6366f1' },
}

const MOIS = ['Jan','Fév','Mar','Avr','Mai','Jun','Jul','Aoû','Sep','Oct','Nov','Déc']

const anciennes = computed(() =>
  parties.value.filter(p => !p.estActive)
)

const hasParties = computed(() => parties.value.length > 0)

onMounted(async () => {
  try {
    const [resCurrent, resAll] = await Promise.all([
      fetch('http://localhost:5219/api/partie/current', { headers: authHeaders() }),
      fetch('http://localhost:5219/api/partie/all',     { headers: authHeaders() }),
    ])
    if (resCurrent.status === 401) { clearSession(); router.push('/auth'); return }
    if (resCurrent.ok) partie.value = await resCurrent.json()
    if (resAll.ok)     parties.value = await resAll.json()
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

async function chargerPartie(partieID) {
  chargingId.value = partieID
  try {
    const res = await fetch(`http://localhost:5219/api/partie/${partieID}/charger`, {
      method: 'POST',
      headers: authHeaders(),
    })
    if (res.ok) {
      showCharger.value = false
      router.push('/game')
    }
  } finally {
    chargingId.value = null
  }
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
              v-if="anciennes.length > 0"
              block
              size="large"
              rounded="pill"
              variant="outlined"
              class="home-btn-secondary mb-3"
              @click="showCharger = true"
            >
              📂 Charger une ancienne partie
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
          Ta partie en cours sera sauvegardée et désactivée.
          Tu pourras la recharger plus tard depuis l'écran d'accueil.
        </v-card-text>
        <v-card-actions class="justify-center pb-4 gap-3">
          <v-btn variant="outlined" rounded="pill" @click="showWarning = false">
            Annuler
          </v-btn>
          <v-btn color="primary" variant="elevated" rounded="pill" @click="confirmerNouvellePartie">
            Nouvelle partie
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Dialog charger ancienne partie -->
    <v-dialog v-model="showCharger" max-width="560">
      <v-card class="warning-dialog" rounded="xl">
        <v-card-item>
          <v-card-title class="text-h6 text-center pt-2">📂 Charger une partie</v-card-title>
        </v-card-item>
        <v-card-text class="pb-2">
          <div class="old-parties-list">
            <div
              v-for="p in anciennes"
              :key="p.partieID"
              class="old-party-card"
              @click="chargerPartie(p.partieID)"
            >
              <div class="old-party-main">
                <span class="old-party-name">{{ p.entraineurPrenom }} {{ p.entraineurNom }}</span>
                <span class="old-party-epoque" :style="{ color: EPOQUES[p.epoque]?.color }">
                  {{ EPOQUES[p.epoque]?.icon }} {{ EPOQUES[p.epoque]?.label }}
                </span>
              </div>
              <div class="old-party-details">
                <span>{{ MOIS[(p.moisActuel ?? 1) - 1] }} {{ p.anneeActuelle }} · Tour {{ p.tourActuel }}</span>
                <span class="old-party-money">{{ p.argent?.toLocaleString('fr-FR') }} €</span>
              </div>
              <div class="old-party-bg">{{ p.backgroundIcone }} {{ p.backgroundNom }}</div>
              <v-progress-circular
                v-if="chargingId === p.partieID"
                indeterminate size="18" width="2" color="indigo"
                class="ml-2"
              />
            </div>
          </div>
          <div v-if="anciennes.length === 0" class="text-center py-4" style="color:#64748b">
            Aucune ancienne partie sauvegardée.
          </div>
        </v-card-text>
        <v-card-actions class="justify-center pb-4">
          <v-btn variant="outlined" rounded="pill" @click="showCharger = false">
            Fermer
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

  </v-app>
</template>

<style scoped>
.old-parties-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
  max-height: 360px;
  overflow-y: auto;
}
.old-party-card {
  padding: 12px 16px;
  border-radius: 12px;
  border: 1px solid rgba(255,255,255,.08);
  background: rgba(255,255,255,.04);
  cursor: pointer;
  transition: all .2s;
}
.old-party-card:hover {
  background: rgba(99,102,241,.12);
  border-color: rgba(99,102,241,.3);
}
.old-party-main {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 4px;
}
.old-party-name {
  font-weight: 700;
  font-size: .95rem;
  color: #e2e8f0;
}
.old-party-epoque {
  font-size: .8rem;
  font-weight: 600;
}
.old-party-details {
  display: flex;
  justify-content: space-between;
  font-size: .78rem;
  color: #64748b;
}
.old-party-money {
  color: #22c55e;
  font-weight: 600;
}
.old-party-bg {
  font-size: .72rem;
  color: #94a3b8;
  margin-top: 2px;
}
</style>
