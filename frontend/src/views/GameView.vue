<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useAuth } from '../composables/useAuth'
import FightersTab from '../components/FightersTab.vue'
import TrainingTab from '../components/TrainingTab.vue'

const router = useRouter()
const { email, authHeaders, clearSession } = useAuth()

const partie        = ref(null)
const loading       = ref(true)
const tab           = ref('dashboard')
const ecurieCount   = ref(0)

// Tour
const avancement       = ref(false)
const tourResultat     = ref(null)  // TourResultatDto | null
const dialogResultat   = ref(false)

const EPOQUES = {
  NoRules:   { label: 'Underground Era', years: '1985 – 1999', icon: '🔥', color: '#ef4444' },
  GoldenAge: { label: 'Golden Age',      years: '2000 – 2012', icon: '🏆', color: '#f59e0b' },
  Modern:    { label: 'Modern MMA',      years: '2013 – Auj.', icon: '🧠', color: '#6366f1' },
}

const TABS = [
  { key: 'dashboard',    icon: '🏠', label: 'Accueil',      available: true  },
  { key: 'fighters',     icon: '👊', label: 'Combattants',  available: true  },
  { key: 'training',     icon: '🏋️', label: 'Entraînement', available: true  },
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

const dateJeu = computed(() => {
  if (!partie.value) return ''
  const mois = [
    'Jan','Fév','Mar','Avr','Mai','Jun',
    'Jul','Aoû','Sep','Oct','Nov','Déc',
  ]
  const m = (partie.value.moisActuel ?? 1) - 1
  return `${mois[m]} ${partie.value.anneeActuelle ?? 1985}`
})

function statColor(val) {
  if (val >= 75) return '#22c55e'
  if (val >= 55) return '#f59e0b'
  if (val >= 40) return '#f97316'
  return '#ef4444'
}

async function chargerPartie() {
  const res = await fetch('http://localhost:5219/api/partie/current', {
    headers: authHeaders()
  })
  if (res.status === 401) { clearSession(); router.push('/auth'); return }
  if (res.status === 404) { router.push('/home'); return }
  if (res.ok) partie.value = await res.json()
}

onMounted(async () => {
  try {
    await chargerPartie()

    const ecurieRes = await fetch('http://localhost:5219/api/combattants/ecurie', {
      headers: authHeaders()
    })
    if (ecurieRes.ok) {
      const data = await ecurieRes.json()
      ecurieCount.value = data.length
    }
  } catch {
    router.push('/home')
  } finally {
    loading.value = false
  }
})

async function avancerTour() {
  if (avancement.value) return
  avancement.value = true
  try {
    const res = await fetch('http://localhost:5219/api/entrainements/avancer-tour', {
      method: 'POST',
      headers: authHeaders(),
    })
    if (res.ok) {
      tourResultat.value   = await res.json()
      dialogResultat.value = true
      // Recharger les données de la partie (nouveau mois/tour)
      await chargerPartie()
    }
  } finally {
    avancement.value = false
  }
}

function fermerResultat() {
  dialogResultat.value = false
  tourResultat.value   = null
}

function logout() {
  clearSession()
  router.push('/auth')
}

const MOIS_FR = [
  'Janvier','Février','Mars','Avril','Mai','Juin',
  'Juillet','Août','Septembre','Octobre','Novembre','Décembre',
]
function moisLabel(n) { return MOIS_FR[(n ?? 1) - 1] }
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
          <span v-if="partie" class="game-date-badge">
            📅 {{ dateJeu }} · Tour {{ partie.tourActuel }}
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

              <!-- Écurie -->
              <v-card class="test-card" elevation="8" rounded="xl">
                <v-card-item>
                  <div class="card-label">Écurie</div>
                  <v-card-title class="text-h5">
                    {{ ecurieCount === 0 ? 'Aucun combattant' : `${ecurieCount} combattant${ecurieCount > 1 ? 's' : ''}` }}
                  </v-card-title>
                </v-card-item>
                <v-card-text>
                  <div v-if="ecurieCount === 0" class="placeholder-block">
                    <span style="font-size:2.5rem">👊</span>
                    <p>Tu n'as pas encore de combattants. Visite l'onglet <strong>Combattants</strong> pour recruter ta première écurie.</p>
                    <v-btn
                      variant="outlined"
                      rounded="pill"
                      size="small"
                      style="margin-top:12px;border-color:rgba(99,102,241,.5);color:#6366f1"
                      @click="tab = 'fighters'"
                    >
                      Recruter un combattant
                    </v-btn>
                  </div>
                  <div v-else class="placeholder-block">
                    <span style="font-size:2.5rem">👊</span>
                    <p>{{ ecurieCount }} combattant{{ ecurieCount > 1 ? 's' : '' }} dans ton écurie.</p>
                    <v-btn
                      variant="outlined"
                      rounded="pill"
                      size="small"
                      style="margin-top:12px;border-color:rgba(99,102,241,.5);color:#6366f1"
                      @click="tab = 'fighters'"
                    >
                      Gérer l'écurie
                    </v-btn>
                  </div>
                </v-card-text>
              </v-card>

            </v-col>
          </v-row>
        </div>

        <!-- Tab Combattants -->
        <div v-else-if="tab === 'fighters'">
          <FightersTab
            :auth-headers="authHeaders"
            @ecurie-updated="ecurieCount = $event"
          />
        </div>

        <!-- Tab Entraînement -->
        <div v-else-if="tab === 'training' && partie">
          <TrainingTab
            :auth-headers="authHeaders"
            :partie="partie"
          />
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

      <!-- ── FAB "Mois suivant" ─────────────────────────────── -->
      <div class="fab-zone">
        <button class="fab-next-turn" :disabled="avancement" @click="avancerTour">
          <span v-if="avancement" class="fab-spinner">⟳</span>
          <span v-else class="fab-icon">▶</span>
          <span class="fab-label">{{ avancement ? 'En cours…' : 'Mois suivant' }}</span>
        </button>
      </div>

    </v-main>

    <!-- ── Dialog résultats du tour ──────────────────────────── -->
    <v-dialog v-model="dialogResultat" max-width="600" persistent>
      <v-card class="resultat-dialog" rounded="xl" elevation="24">
        <v-card-title class="resultat-title">
          <span>📊 Résultats — Tour {{ tourResultat?.tourJoue }}</span>
          <span class="resultat-date">{{ moisLabel(tourResultat?.nouveauMois) }} {{ tourResultat?.nouvelleAnnee }}</span>
        </v-card-title>

        <v-card-text class="resultat-body">
          <div v-if="tourResultat?.resultats?.length === 0" class="resultat-empty">
            Aucun entraînement planifié ce tour.
          </div>
          <div v-else>
            <div
              v-for="r in tourResultat?.resultats"
              :key="r.combattantID"
              class="fighter-resultat"
            >
              <div class="fighter-resultat-header">
                <span class="fighter-resultat-name">{{ r.prenom }} {{ r.nomFamille }}</span>
                <span class="fighter-resultat-type">{{ r.typeEntrainement }}</span>
              </div>
              <div class="gains-list">
                <div v-for="g in r.gains" :key="g.stat" class="gain-row">
                  <span class="gain-stat">{{ g.stat }}</span>
                  <span class="gain-value">+{{ g.gain }}</span>
                </div>
                <div v-if="r.gains.length === 0" class="gain-row">
                  <span class="gain-stat" style="color:#64748b">Déjà au maximum</span>
                </div>
              </div>
            </div>
          </div>
        </v-card-text>

        <v-card-actions class="resultat-actions">
          <v-btn
            variant="flat"
            color="indigo"
            rounded="pill"
            @click="fermerResultat"
          >
            Continuer
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

  </v-app>
</template>

<style scoped>
/* ── FAB ──────────────────────────────────────────────────── */
.fab-zone {
  position: fixed;
  bottom: 28px;
  right: 28px;
  z-index: 100;
}
.fab-next-turn {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 14px 22px;
  border-radius: 50px;
  background: linear-gradient(135deg, #6366f1, #818cf8);
  color: white;
  font-size: .95rem;
  font-weight: 700;
  border: none;
  cursor: pointer;
  box-shadow: 0 6px 24px rgba(99,102,241,.45);
  transition: all .2s;
  letter-spacing: .02em;
}
.fab-next-turn:hover:not(:disabled) {
  transform: translateY(-2px);
  box-shadow: 0 8px 32px rgba(99,102,241,.6);
}
.fab-next-turn:disabled {
  opacity: .6;
  cursor: not-allowed;
}
.fab-icon { font-size: .8rem; }
.fab-spinner {
  display: inline-block;
  animation: spin 1s linear infinite;
  font-size: 1rem;
}
@keyframes spin { to { transform: rotate(360deg); } }

/* ── Date badge dans topbar ──────────────────────────────── */
.game-date-badge {
  font-size: .78rem;
  color: #94a3b8;
  padding: 3px 10px;
  border-radius: 20px;
  border: 1px solid rgba(148,163,184,.2);
  background: rgba(148,163,184,.06);
  margin-left: 6px;
}

/* ── Dialog résultats ─────────────────────────────────────── */
.resultat-dialog {
  background: #0f1729 !important;
  border: 1px solid rgba(99,102,241,.25);
}
.resultat-title {
  display: flex;
  flex-direction: column;
  gap: 4px;
  padding: 20px 24px 8px;
  font-size: 1.05rem;
  font-weight: 700;
  color: #e2e8f0;
}
.resultat-date {
  font-size: .8rem;
  font-weight: 400;
  color: #6366f1;
}
.resultat-body {
  padding: 8px 24px 16px;
}
.resultat-empty {
  color: #475569;
  font-size: .9rem;
  text-align: center;
  padding: 24px 0;
}
.fighter-resultat {
  margin-bottom: 16px;
  padding: 12px 14px;
  border-radius: 12px;
  background: rgba(255,255,255,.04);
  border: 1px solid rgba(255,255,255,.06);
}
.fighter-resultat-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 8px;
}
.fighter-resultat-name {
  font-weight: 600;
  font-size: .9rem;
  color: #e2e8f0;
}
.fighter-resultat-type {
  font-size: .75rem;
  padding: 2px 10px;
  border-radius: 20px;
  background: rgba(99,102,241,.2);
  color: #a5b4fc;
}
.gains-list {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}
.gain-row {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 4px 10px;
  border-radius: 8px;
  background: rgba(34,197,94,.08);
  border: 1px solid rgba(34,197,94,.15);
}
.gain-stat {
  font-size: .75rem;
  color: #94a3b8;
}
.gain-value {
  font-size: .8rem;
  font-weight: 700;
  color: #22c55e;
}
.resultat-actions {
  padding: 8px 24px 20px;
  justify-content: flex-end;
}
</style>
