<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useAuth } from '../composables/useAuth'

const router = useRouter()
const { email, authHeaders, clearSession } = useAuth()

const partie  = ref(null)
const loading = ref(true)
const tab     = ref('dashboard')

const EPOQUES = {
  NoRules:   { label: 'Underground Era', years: '1985 – 1999', icon: '🔥', color: '#ef4444' },
  GoldenAge: { label: 'Golden Age',      years: '2000 – 2012', icon: '🏆', color: '#f59e0b' },
  Modern:    { label: 'Modern MMA',      years: '2013 – Auj.', icon: '🧠', color: '#6366f1' },
}

const TABS = [
  { key: 'dashboard',    icon: '🏠', label: 'Accueil',      available: true  },
  { key: 'fighters',     icon: '👊', label: 'Combattants',  available: false },
  { key: 'training',     icon: '🏋️', label: 'Entraînement', available: false },
  { key: 'fights',       icon: '📋', label: 'Combats',      available: false },
  { key: 'rankings',     icon: '📊', label: 'Classements',  available: false },
]

const epoqueInfo = computed(() =>
  partie.value ? EPOQUES[partie.value.epoque] ?? {} : {}
)

const argent = computed(() =>
  partie.value
    ? new Intl.NumberFormat('fr-FR', { style: 'currency', currency: 'EUR', maximumFractionDigits: 0 })
        .format(partie.value.argent)
    : '—'
)

function statColor(val) {
  if (val >= 75) return '#22c55e'
  if (val >= 55) return '#f59e0b'
  if (val >= 40) return '#f97316'
  return '#ef4444'
}

onMounted(async () => {
  try {
    const res = await fetch('http://localhost:5219/api/partie/current', {
      headers: authHeaders()
    })
    if (res.status === 401) { clearSession(); router.push('/auth'); return }
    if (res.status === 404) { router.push('/home'); return }
    if (res.ok) partie.value = await res.json()
  } catch {
    router.push('/home')
  } finally {
    loading.value = false
  }
})

function logout() {
  clearSession()
  router.push('/auth')
}
</script>

<template>
  <v-app>
    <v-main class="app-shell">

      <!-- ── Top bar ───────────────────────────────────────── -->
      <div class="game-topbar">
        <div class="game-topbar-left">
          <span class="game-logo">MMA Manager</span>
          <span v-if="partie" class="game-epoque-badge" :style="{ borderColor: epoqueInfo.color, color: epoqueInfo.color }">
            {{ epoqueInfo.icon }} {{ epoqueInfo.label }}
          </span>
        </div>
        <div class="game-topbar-right">
          <div v-if="partie" class="money-counter">
            💰 {{ argent }}
          </div>
          <span class="user-email">{{ email }}</span>
          <v-btn variant="text" size="small" class="logout-btn" @click="logout">
            Déconnexion
          </v-btn>
        </div>
      </div>

      <!-- ── Nav tabs ──────────────────────────────────────── -->
      <div class="game-nav">
        <button
          v-for="t in TABS"
          :key="t.key"
          class="game-nav-item"
          :class="{ active: tab === t.key, disabled: !t.available }"
          @click="t.available && (tab = t.key)"
        >
          <span class="nav-icon">{{ t.icon }}</span>
          <span class="nav-label">{{ t.label }}</span>
          <span v-if="!t.available" class="nav-soon">bientôt</span>
        </button>
      </div>

      <!-- ── Content ───────────────────────────────────────── -->
      <v-container class="game-content">

        <div v-if="loading" class="text-center py-12">
          <v-progress-circular indeterminate color="indigo" size="48" />
        </div>

        <!-- Dashboard -->
        <div v-else-if="tab === 'dashboard' && partie">
          <v-row>

            <!-- Carte entraîneur -->
            <v-col cols="12" md="5">
              <v-card class="test-card trainer-card" elevation="8" rounded="xl">
                <v-card-item>
                  <div class="card-label">Ton entraîneur</div>
                  <v-card-title class="text-h5">
                    {{ partie.entraineur.prenom }} {{ partie.entraineur.nom }}
                  </v-card-title>
                  <v-card-subtitle>
                    {{ partie.entraineur.backgroundIcone }} {{ partie.entraineur.backgroundNom }}
                  </v-card-subtitle>
                </v-card-item>
                <v-card-text>
                  <div class="trainer-meta mb-4">
                    <span class="meta-chip">🌍 {{ partie.entraineur.paysOrigineNom }}</span>
                    <span class="meta-chip">🏠 {{ partie.entraineur.paysResidenceNom }}</span>
                  </div>
                  <p class="bg-desc mb-4" style="font-size:.85rem">
                    {{ partie.entraineur.backgroundDescription }}
                  </p>

                  <!-- Stats -->
                  <div class="trainer-stats">
                    <div
                      v-for="stat in partie.entraineur.stats"
                      :key="stat.cle"
                      class="stat-row"
                    >
                      <span class="stat-label">{{ stat.nom }}</span>
                      <div class="stat-track">
                        <div
                          class="stat-fill"
                          :style="{ width: stat.valeur + '%', background: statColor(stat.valeur) }"
                        />
                      </div>
                      <span class="stat-val" :style="{ color: statColor(stat.valeur) }">
                        {{ stat.valeur }}
                      </span>
                    </div>
                  </div>
                </v-card-text>
              </v-card>
            </v-col>

            <!-- Infos partie + dashboard -->
            <v-col cols="12" md="7">

              <!-- Finances -->
              <v-card class="test-card mb-4" elevation="8" rounded="xl">
                <v-card-item>
                  <div class="card-label">Finances</div>
                  <v-card-title class="text-h5">Budget disponible</v-card-title>
                </v-card-item>
                <v-card-text>
                  <div class="budget-display">{{ argent }}</div>
                  <p style="color:#475569;font-size:.85rem;margin-top:8px">
                    Investis dans des combattants, des entraîneurs et des camps de préparation.
                  </p>
                </v-card-text>
              </v-card>

              <!-- Écurie (placeholder) -->
              <v-card class="test-card" elevation="8" rounded="xl">
                <v-card-item>
                  <div class="card-label">Écurie</div>
                  <v-card-title class="text-h5">Aucun combattant</v-card-title>
                </v-card-item>
                <v-card-text>
                  <div class="placeholder-block">
                    <span style="font-size:2.5rem">👊</span>
                    <p>Tu n'as pas encore de combattants. Visite l'onglet <strong>Combattants</strong> pour recruter ta première écurie.</p>
                    <v-btn
                      variant="outlined"
                      rounded="pill"
                      disabled
                      size="small"
                      style="margin-top:12px;border-color:rgba(99,102,241,.3);color:#6366f1"
                    >
                      Recruter — bientôt disponible
                    </v-btn>
                  </div>
                </v-card-text>
              </v-card>

            </v-col>
          </v-row>
        </div>

        <!-- Tabs "bientôt disponible" -->
        <div v-else-if="tab !== 'dashboard'" class="coming-soon">
          <div class="coming-icon">
            {{ TABS.find(t => t.key === tab)?.icon }}
          </div>
          <h2 class="coming-title">{{ TABS.find(t => t.key === tab)?.label }}</h2>
          <p class="coming-sub">Cette section est en cours de développement. Elle sera disponible prochainement.</p>
        </div>

      </v-container>
    </v-main>
  </v-app>
</template>
