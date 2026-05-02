<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useAuth } from '../composables/useAuth'
import FightersTab      from '../components/FightersTab.vue'
import TrainingTab      from '../components/TrainingTab.vue'
import CombatPlanifieTab from '../components/CombatPlanifieTab.vue'
import FinancesTab      from '../components/FinancesTab.vue'
import StaffTab        from '../components/StaffTab.vue'

const router = useRouter()
const { email, authHeaders, clearSession } = useAuth()

const partie        = ref(null)
const loading       = ref(true)
const tab           = ref('dashboard')
const ecurieCount   = ref(0)

// Tour
const avancement          = ref(false)
const tourResultat        = ref(null)   // TourResultatDto | null
const dialogResultat      = ref(false)
const dialogTransition    = ref(false)  // dialog changement d'ère
const dialogCombats       = ref(false)  // dialog résultats combats
const dialogPrestige      = ref(false)  // dialog prestige de l'écurie
const dialogGameOver      = ref(false)  // dialog game over

const EPOQUES = {
  NoRules:   { label: 'Underground Era', years: '1985 – 1999', icon: '🔥', color: '#ef4444' },
  GoldenAge: { label: 'Golden Age',      years: '2000 – 2012', icon: '🏆', color: '#f59e0b' },
  Modern:    { label: 'Modern MMA',      years: '2013 – Auj.', icon: '🧠', color: '#6366f1' },
}

const TABS = [
  { key: 'dashboard',    icon: '🏠', label: 'Accueil',          available: true  },
  { key: 'fighters',     icon: '👊', label: 'Combattants',      available: true  },
  { key: 'training',     icon: '🏋️', label: 'Entraînement',     available: true  },
  { key: 'fights',       icon: '📋', label: 'Planifier combat', available: true  },
  { key: 'finances',     icon: '💰', label: 'Finances',         available: true  },
  { key: 'staff',        icon: '👥', label: 'Staff',            available: true  },
  { key: 'rankings',     icon: '📊', label: 'Classements',      available: false },
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
      tourResultat.value = await res.json()
      await chargerPartie()
      // Ordre : 1) combats  2) transition d'ère  3) entraînements  4) game over
      if (tourResultat.value.combatsResultats?.length > 0) {
        dialogCombats.value = true
      } else if (tourResultat.value.transitionEpoque) {
        dialogTransition.value = true
      } else if (tourResultat.value.resultats?.length > 0) {
        dialogResultat.value = true
      } else if (tourResultat.value.estGameOver) {
        dialogGameOver.value = true
      }
    }
  } finally {
    avancement.value = false
  }
}

function fermerCombats() {
  dialogCombats.value = false
  if (tourResultat.value?.transitionEpoque) {
    dialogTransition.value = true
  } else if (tourResultat.value?.resultats?.length > 0) {
    dialogResultat.value = true
  } else if (tourResultat.value?.prestigeAugmente) {
    dialogPrestige.value = true
  } else if (tourResultat.value?.estGameOver) {
    dialogGameOver.value = true
  } else {
    tourResultat.value = null
  }
}

function fermerTransition() {
  dialogTransition.value = false
  if (tourResultat.value?.resultats?.length > 0) {
    dialogResultat.value = true
  } else if (tourResultat.value?.prestigeAugmente) {
    dialogPrestige.value = true
  } else if (tourResultat.value?.estGameOver) {
    dialogGameOver.value = true
  } else {
    tourResultat.value = null
  }
}

function fermerResultat() {
  dialogResultat.value = false
  if (tourResultat.value?.prestigeAugmente) {
    dialogPrestige.value = true
  } else if (tourResultat.value?.estGameOver) {
    dialogGameOver.value = true
  } else {
    tourResultat.value = null
  }
}

function fermerPrestige() {
  dialogPrestige.value = false
  if (tourResultat.value?.estGameOver) {
    dialogGameOver.value = true
  } else {
    tourResultat.value = null
  }
}

function allerAccueil() {
  dialogGameOver.value = false
  tourResultat.value   = null
  router.push('/home')
}

function combatResultIcon(c) {
  if (c.estNul)      return '🟡'
  if (c.estVictoire) return '🟢'
  return '🔴'
}

function combatResultLabel(c) {
  if (c.estNul)      return 'NUL'
  if (c.estVictoire) return 'VICTOIRE'
  return 'DÉFAITE'
}

function combatResultColor(c) {
  if (c.estNul)      return '#f59e0b'
  if (c.estVictoire) return '#22c55e'
  return '#ef4444'
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

function typeEntrainementIcon(type) {
  const icons = { Striking: '🥊', Lutte: '🤼', Grappling: '⛩️', Conditionnement: '🏋️', Mental: '🧠' }
  return icons[type] ?? '🏃'
}
function formatMoney(val) {
  return Number(val ?? 0).toLocaleString('fr-FR')
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
          <span v-if="partie" class="game-date-badge">
            📅 {{ dateJeu }} · Tour {{ partie.tourActuel }}
          </span>
          <span v-if="partie" class="game-prestige-badge">
            {{ '⭐'.repeat(partie.prestigeEcurie) }}
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
            :partie="partie"
            @ecurie-updated="ecurieCount = $event"
            @argent-updated="partie.argent = $event"
          />
        </div>

        <!-- Tab Entraînement -->
        <div v-else-if="tab === 'training' && partie">
          <TrainingTab
            :auth-headers="authHeaders"
            :partie="partie"
          />
        </div>

        <!-- Tab Planifier combat -->
        <div v-else-if="tab === 'fights' && partie">
          <CombatPlanifieTab
            :auth-headers="authHeaders"
            :partie="partie"
          />
        </div>

        <!-- Tab Finances -->
        <div v-else-if="tab === 'finances'">
          <FinancesTab :auth-headers="authHeaders" />
        </div>

        <!-- Tab Staff -->
        <div v-else-if="tab === 'staff'">
          <StaffTab :auth-headers="authHeaders" />
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

    <!-- ── Dialog résultats combats ────────────────────────────── -->
    <v-dialog v-model="dialogCombats" max-width="820" persistent>
      <v-card class="resultat-dialog" rounded="xl" elevation="24">
        <v-card-title class="resultat-title">
          <span>🥊 Combats — Tour {{ tourResultat?.tourJoue }}</span>
          <span class="resultat-date">{{ moisLabel(tourResultat?.nouveauMois) }} {{ tourResultat?.nouvelleAnnee }}</span>
        </v-card-title>

        <v-card-text class="resultat-body">
          <div
            v-for="c in tourResultat?.combatsResultats"
            :key="c.combattantID"
            class="combat-resultat"
            :style="{ borderColor: combatResultColor(c) + '44' }"
          >
            <!-- Bandeau résultat -->
            <div class="combat-banner" :style="{ background: combatResultColor(c) + '22' }">
              <span class="combat-banner-icon">{{ combatResultIcon(c) }}</span>
              <span class="combat-banner-label" :style="{ color: combatResultColor(c) }">
                {{ combatResultLabel(c) }}
              </span>
              <span class="combat-org">{{ c.organisation }}</span>
            </div>
            <!-- Matchup -->
            <div class="combat-matchup">
              <span class="combat-fighter">{{ c.combattantNom }}</span>
              <span class="combat-vs">VS</span>
              <span class="combat-fighter">{{ c.adversaireNom }}</span>
            </div>
            <!-- Détail -->
            <div class="combat-details">
              <span class="combat-method">{{ c.methodeVictoire }}</span>
              <span class="combat-detail-text">{{ c.details }}</span>
            </div>
            <!-- Rounds -->
            <div class="combat-rounds" v-if="c.rounds?.length">
              <div
                v-for="r in c.rounds"
                :key="r.numeroRound"
                class="round-row"
                :class="{ 'round-finish': r.estFinish }"
              >
                <div class="round-top">
                  <span class="round-num">R{{ r.numeroRound }}</span>
                  <span class="round-winner"
                    :class="{
                      'round-win':  r.gagnantRound === 'Combattant',
                      'round-loss': r.gagnantRound === 'Adversaire',
                      'round-draw': r.gagnantRound === 'Egal'
                    }">
                    {{ r.gagnantRound === 'Combattant' ? '✅' : r.gagnantRound === 'Adversaire' ? '❌' : '➖' }}
                    {{ r.scoreCombattant }}–{{ r.scoreAdversaire }}
                  </span>
                  <span class="round-finish-badge" v-if="r.estFinish">🏁 {{ r.methodeFinish }}</span>
                </div>
                <div class="round-actions">{{ r.actionsPrincipales }}</div>
              </div>
            </div>
            <!-- Bourse -->
            <div class="combat-bourse" v-if="c.bourseGagnee">
              <span class="bourse-label">💰 Bourse :</span>
              <span class="bourse-amount" :class="c.estVictoire ? 'bourse-win' : c.estNul ? 'bourse-draw' : 'bourse-loss'">
                +{{ formatMoney(c.bourseGagnee) }} €
              </span>
            </div>
            <!-- Blessure -->
            <div v-if="c.blessureZone" class="combat-injury">
              🏥 Blessure : {{ c.blessureZone }} — Indisponible {{ c.semainesIndispo }} tour{{ c.semainesIndispo > 1 ? 's' : '' }}
            </div>
          </div>

          <!-- Résumé financier -->
          <div class="finance-summary">
            <div class="finance-title">💰 Bilan financier du mois</div>
            <div class="finance-row finance-pos" v-if="tourResultat?.combatsResultats?.some(c => c.bourseGagnee)">
              <span>Bourses de combat</span>
              <span class="finance-green">+{{ formatMoney(tourResultat.combatsResultats.reduce((s, c) => s + (c.bourseGagnee || 0), 0)) }} €</span>
            </div>
            <div class="finance-row">
              <span>Dépenses (salaires + loyer)</span>
              <span class="finance-neg">−{{ formatMoney(tourResultat?.depensesTotales) }} €</span>
            </div>
            <div class="finance-row finance-balance">
              <span>Nouveau solde</span>
              <span>{{ formatMoney(tourResultat?.soldeApres) }} €</span>
            </div>
          </div>
        </v-card-text>

        <v-card-actions class="resultat-actions">
          <v-btn variant="flat" color="indigo" rounded="pill" @click="fermerCombats">
            Continuer
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- ── Dialog transition d'ère ─────────────────────────────── -->
    <v-dialog v-model="dialogTransition" max-width="560" persistent>
      <v-card class="transition-dialog" rounded="xl" elevation="24">
        <div class="transition-header">
          <span class="transition-big-icon">
            {{ tourResultat?.nouvelleEpoque === 'GoldenAge' ? '🏆' : '🧠' }}
          </span>
          <div class="transition-label">Nouvelle ère</div>
          <div class="transition-era">
            {{ tourResultat?.nouvelleEpoque === 'GoldenAge' ? 'Golden Age' : 'Modern MMA' }}
          </div>
          <div class="transition-year">{{ tourResultat?.nouvelleAnnee }}</div>
        </div>
        <v-card-text class="transition-body">
          <p
            v-for="(line, i) in (tourResultat?.messageTransition ?? '').split('\n').filter(Boolean)"
            :key="i"
            class="transition-line"
            :class="{ 'transition-line-main': i === 0 }"
          >{{ line }}</p>
        </v-card-text>
        <v-card-actions style="justify-content:center;padding-bottom:20px">
          <v-btn variant="flat" color="indigo" rounded="pill" size="large" @click="fermerTransition">
            Entrer dans la nouvelle ère
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

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
            <div class="section-title">📋 Résultats d'entraînement</div>
            <div
              v-for="r in tourResultat?.resultats"
              :key="r.combattantID"
              class="fighter-resultat"
            >
              <div class="fighter-resultat-header">
                <div class="fighter-resultat-left">
                  <span class="fighter-resultat-name">{{ r.prenom }} {{ r.nomFamille }}</span>
                  <span class="fighter-resultat-type">
                    {{ typeEntrainementIcon(r.typeEntrainement) }} {{ r.typeEntrainement }}
                  </span>
                </div>
                <span class="fighter-resultat-bilan" v-if="r.gains.length > 0">
                  {{ r.gains.length }} stat{{ r.gains.length > 1 ? 's' : '' }} améliorée{{ r.gains.length > 1 ? 's' : '' }}
                </span>
              </div>
              <div class="gains-list" v-if="r.gains.length > 0">
                <div v-for="g in r.gains" :key="g.stat" class="gain-row">
                  <span class="gain-stat">{{ g.stat }}</span>
                  <span class="gain-value">+{{ g.gain }}</span>
                </div>
              </div>
              <div v-else class="gains-empty">Aucun progrès ce mois — stats déjà au maximum.</div>
            </div>
          </div>
        </v-card-text>

        <!-- Résumé financier (affiché ici seulement s'il n'y a pas eu de combats) -->
        <div v-if="!tourResultat?.combatsResultats?.length" class="finance-summary finance-summary--training">
          <div class="finance-title">💰 Bilan financier</div>
          <div class="finance-row">
            <span>Dépenses (salaires + loyer)</span>
            <span class="finance-neg">−{{ formatMoney(tourResultat?.depensesTotales) }} €</span>
          </div>
          <div class="finance-row finance-balance">
            <span>Nouveau solde</span>
            <span>{{ formatMoney(tourResultat?.soldeApres) }} €</span>
          </div>
        </div>

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

    <!-- ── Dialog Prestige de l'écurie ──────────────────────────── -->
    <v-dialog v-model="dialogPrestige" max-width="480" persistent>
      <v-card class="prestige-dialog" rounded="xl" elevation="24">
        <div class="prestige-header">
          <div class="prestige-stars">
            {{ '⭐'.repeat(tourResultat?.prestigeEcurie ?? 1) }}
          </div>
          <div class="prestige-label">Prestige de l'écurie</div>
          <div class="prestige-niveau">Niveau {{ tourResultat?.prestigeEcurie }}</div>
        </div>
        <v-card-text class="prestige-body">
          <p>Ton écurie gagne en réputation ! De nouveaux combattants et organisations vont s'ouvrir à toi.</p>
          <div class="prestige-unlock" v-if="tourResultat?.prestigeEcurie >= 2">
            <span class="prestige-unlock-icon">🔓</span>
            <span>Accès à des combattants de niveau supérieur et à des organisations plus prestigieuses.</span>
          </div>
        </v-card-text>
        <v-card-actions style="justify-content:center;padding-bottom:24px">
          <v-btn variant="flat" color="amber-darken-2" rounded="pill" size="large" @click="fermerPrestige">
            Super, continuons !
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- ── Dialog Game Over ──────────────────────────────────── -->
    <v-dialog v-model="dialogGameOver" max-width="480" persistent>
      <v-card class="gameover-dialog" rounded="xl" elevation="24">
        <div class="gameover-header">
          <div class="gameover-icon">💀</div>
          <div class="gameover-title">GAME OVER</div>
          <div class="gameover-sub">Ton écurie est en faillite</div>
        </div>
        <v-card-text class="gameover-body">
          <p>Tu n'as plus assez d'argent pour payer tes charges mensuelles.</p>
          <p>Les combattants ont quitté l'écurie et les portes de la salle sont fermées.</p>
          <div class="gameover-solde">
            Solde final : <strong style="color:#ef4444">{{ formatMoney(tourResultat?.soldeApres) }} €</strong>
          </div>
        </v-card-text>
        <v-card-actions style="justify-content:center;padding-bottom:24px">
          <v-btn variant="flat" color="red-darken-2" rounded="pill" size="large" @click="allerAccueil">
            Recommencer une nouvelle partie
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
.game-prestige-badge {
  font-size: .8rem;
  padding: 3px 8px;
  border-radius: 20px;
  border: 1px solid rgba(251,191,36,.3);
  background: rgba(251,191,36,.08);
  margin-left: 6px;
  letter-spacing: 1px;
}

/* ── Dialog transition d'ère ─────────────────────────────── */
.transition-dialog {
  background: linear-gradient(160deg, #1e1b4b 0%, #0f172a 100%) !important;
  border: 1px solid rgba(99,102,241,.4);
  text-align: center;
}
.transition-header {
  padding: 32px 24px 8px;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 6px;
}
.transition-big-icon { font-size: 3.5rem; }
.transition-label {
  font-size: .8rem;
  text-transform: uppercase;
  letter-spacing: .1em;
  color: #6366f1;
  margin-top: 4px;
}
.transition-era {
  font-size: 1.8rem;
  font-weight: 900;
  color: #e2e8f0;
}
.transition-year {
  font-size: 1rem;
  font-weight: 700;
  color: #a5b4fc;
}
.transition-body {
  padding: 12px 28px 20px;
}
.transition-line {
  font-size: .88rem;
  color: #94a3b8;
  line-height: 1.7;
  margin-bottom: 8px;
}
.transition-line-main {
  font-size: 1rem;
  font-weight: 600;
  color: #e2e8f0;
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
.section-title {
  font-size: .78rem;
  font-weight: 700;
  color: #64748b;
  text-transform: uppercase;
  letter-spacing: .07em;
  margin-bottom: 12px;
}
.fighter-resultat {
  margin-bottom: 12px;
  padding: 12px 14px;
  border-radius: 12px;
  background: rgba(255,255,255,.04);
  border: 1px solid rgba(255,255,255,.06);
}
.fighter-resultat-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  margin-bottom: 8px;
  gap: 8px;
}
.fighter-resultat-left {
  display: flex;
  flex-direction: column;
  gap: 3px;
}
.fighter-resultat-name {
  font-weight: 600;
  font-size: .9rem;
  color: #e2e8f0;
}
.fighter-resultat-type {
  font-size: .74rem;
  padding: 2px 10px;
  border-radius: 20px;
  background: rgba(99,102,241,.2);
  color: #a5b4fc;
  display: inline-block;
  width: fit-content;
}
.fighter-resultat-bilan {
  font-size: .72rem;
  color: #22c55e;
  white-space: nowrap;
  align-self: flex-start;
}
.gains-empty {
  font-size: .76rem;
  color: #475569;
  font-style: italic;
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

/* ── Dialog résultats combats ─────────────────────────────── */
.combat-resultat {
  margin-bottom: 14px;
  border-radius: 12px;
  border: 1px solid rgba(255,255,255,.08);
  overflow: hidden;
}
.combat-banner {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 6px 14px;
}
.combat-banner-icon { font-size: 1.1rem; }
.combat-banner-label {
  font-size: .8rem;
  font-weight: 800;
  letter-spacing: .08em;
  text-transform: uppercase;
}
.combat-org {
  margin-left: auto;
  font-size: .72rem;
  color: #64748b;
}
.combat-matchup {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 12px;
  padding: 8px 14px;
}
.combat-fighter {
  font-size: .88rem;
  font-weight: 600;
  color: #e2e8f0;
}
.combat-vs {
  font-size: .7rem;
  font-weight: 800;
  color: #475569;
  letter-spacing: .05em;
}
.combat-details {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 6px 14px 10px;
}
.combat-method {
  font-size: .78rem;
  font-weight: 700;
  padding: 2px 8px;
  border-radius: 6px;
  background: rgba(99,102,241,.2);
  color: #a5b4fc;
}
.combat-detail-text {
  font-size: .78rem;
  color: #64748b;
}
.combat-rounds {
  margin: 6px 0 4px;
  display: flex;
  flex-direction: column;
  gap: 3px;
  border-top: 1px solid rgba(255,255,255,.08);
  padding-top: 6px;
}
.round-row {
  display: flex;
  flex-direction: column;
  gap: 3px;
  font-size: .74rem;
  color: #94a3b8;
  padding: 5px 6px;
  border-radius: 6px;
}
.round-row.round-finish {
  background: rgba(251,191,36,.08);
  border: 1px solid rgba(251,191,36,.15);
}
.round-top {
  display: flex;
  align-items: center;
  gap: 8px;
}
.round-num {
  font-weight: 700;
  color: #64748b;
  min-width: 22px;
  font-size: .76rem;
}
.round-winner {
  min-width: 62px;
  font-weight: 600;
  font-size: .76rem;
}
.round-win   { color: #4ade80; }
.round-loss  { color: #f87171; }
.round-draw  { color: #94a3b8; }
.round-actions {
  color: #94a3b8;
  font-size: .73rem;
  line-height: 1.4;
  padding-left: 30px;
  white-space: normal;
}
.round-finish-badge {
  font-weight: 700;
  color: #fbbf24;
  font-size: .72rem;
  white-space: nowrap;
  margin-left: auto;
}
.combat-bourse {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 4px 14px 8px;
}
.bourse-label {
  font-size: .78rem;
  color: #94a3b8;
}
.bourse-amount {
  font-size: .85rem;
  font-weight: 700;
}
.bourse-win  { color: #22c55e; }
.bourse-draw { color: #f59e0b; }
.bourse-loss { color: #f97316; }
.combat-injury {
  color: #ef4444;
  font-size: .82rem;
  margin-top: 6px;
  padding: 4px 8px;
  background: rgba(239,68,68,.08);
  border-radius: 6px;
}

/* ── Résumé financier ─────────────────────────────────────── */
.finance-summary {
  margin-top: 16px;
  padding: 12px 14px;
  border-radius: 10px;
  background: rgba(255,255,255,.03);
  border: 1px solid rgba(255,255,255,.06);
}
.finance-summary--training {
  margin: 12px 0 4px;
}
.finance-title {
  font-size: .8rem;
  font-weight: 700;
  color: #94a3b8;
  margin-bottom: 8px;
  text-transform: uppercase;
  letter-spacing: .06em;
}
.finance-row {
  display: flex;
  justify-content: space-between;
  font-size: .83rem;
  color: #94a3b8;
  margin-bottom: 4px;
}
.finance-neg { color: #ef4444; font-weight: 600; }
.finance-green { color: #22c55e; font-weight: 600; }
.finance-balance {
  margin-top: 6px;
  padding-top: 6px;
  border-top: 1px solid rgba(255,255,255,.06);
  color: #e2e8f0;
  font-weight: 700;
}

/* ── Prestige dialog ──────────────────────────────────────── */
.prestige-dialog {
  background: linear-gradient(160deg, #1c1a05 0%, #0f172a 100%) !important;
  border: 1px solid rgba(251,191,36,.4);
  text-align: center;
}
.prestige-header {
  padding: 32px 24px 8px;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 6px;
}
.prestige-stars { font-size: 2rem; letter-spacing: 4px; }
.prestige-label {
  font-size: .8rem;
  text-transform: uppercase;
  letter-spacing: .1em;
  color: #f59e0b;
  margin-top: 4px;
}
.prestige-niveau {
  font-size: 1.6rem;
  font-weight: 900;
  color: #fbbf24;
}
.prestige-body {
  padding: 8px 28px 16px;
  color: #94a3b8;
  font-size: .88rem;
  line-height: 1.7;
}
.prestige-body p { margin-bottom: 8px; }
.prestige-unlock {
  display: flex;
  align-items: flex-start;
  gap: 10px;
  margin-top: 12px;
  padding: 10px 14px;
  border-radius: 10px;
  background: rgba(251,191,36,.08);
  border: 1px solid rgba(251,191,36,.2);
  color: #e2e8f0;
  font-size: .84rem;
  text-align: left;
}
.prestige-unlock-icon { font-size: 1.1rem; flex-shrink: 0; }

/* ── Game Over ────────────────────────────────────────────── */
.gameover-dialog {
  background: linear-gradient(160deg, #1a0505 0%, #0f172a 100%) !important;
  border: 1px solid rgba(239,68,68,.5);
  text-align: center;
}
.gameover-header {
  padding: 36px 24px 12px;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 8px;
}
.gameover-icon  { font-size: 3.5rem; }
.gameover-title {
  font-size: 2rem;
  font-weight: 900;
  color: #ef4444;
  letter-spacing: .06em;
}
.gameover-sub {
  font-size: .9rem;
  color: #94a3b8;
}
.gameover-body {
  padding: 8px 28px 16px;
  color: #94a3b8;
  font-size: .88rem;
  line-height: 1.7;
}
.gameover-body p { margin-bottom: 8px; }
.gameover-solde {
  margin-top: 14px;
  padding: 10px 16px;
  border-radius: 10px;
  background: rgba(239,68,68,.1);
  border: 1px solid rgba(239,68,68,.2);
  color: #e2e8f0;
  font-size: .9rem;
}
</style>
