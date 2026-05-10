<script setup>
import { ref, computed, onMounted, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useAuth } from '../composables/useAuth'
import FightersTab       from '../components/FightersTab.vue'
import TrainingTab       from '../components/TrainingTab.vue'
import CombatPlanifieTab from '../components/CombatPlanifieTab.vue'
import FinancesTab       from '../components/FinancesTab.vue'
import StaffTab          from '../components/StaffTab.vue'
import RankingsTab       from '../components/RankingsTab.vue'

const router = useRouter()
const { email, authHeaders, clearSession } = useAuth()

const partie           = ref(null)
const loading          = ref(true)
const tab              = ref('dashboard')
const ecurieCount      = ref(0)
const prochainsCombats = ref([])

const avancement       = ref(false)
const tourResultat     = ref(null)
const dialogResultat   = ref(false)
const dialogTransition = ref(false)
const dialogCombats    = ref(false)
const dialogPrestige   = ref(false)
const dialogGameOver   = ref(false)

const EPOQUES = {
  NoRules:   { label: 'Underground Era', years: '1985 – 1999', icon: '🔥', color: '#ef4444' },
  GoldenAge: { label: 'Golden Age',      years: '2000 – 2012', icon: '🏆', color: '#f59e0b' },
  Modern:    { label: 'Modern MMA',      years: '2013 – Auj.', icon: '🧠', color: '#818cf8' },
}

const TABS = [
  { key: 'dashboard', icon: '🏠', label: 'Accueil',          available: true  },
  { key: 'fighters',  icon: '👊', label: 'Combattants',      available: true  },
  { key: 'training',  icon: '🏋️', label: 'Entraînement',     available: true  },
  { key: 'fights',    icon: '📋', label: 'Planifier combat', available: true  },
  { key: 'finances',  icon: '💰', label: 'Finances',         available: true  },
  { key: 'staff',     icon: '👥', label: 'Staff',            available: true  },
  { key: 'rankings',  icon: '📊', label: 'Classements',      available: true  },
]

const epoqueInfo = computed(() => partie.value ? EPOQUES[partie.value.epoque] ?? {} : {})

const argent = computed(() =>
  partie.value
    ? new Intl.NumberFormat('fr-FR', { style: 'currency', currency: 'EUR', maximumFractionDigits: 0 })
        .format(partie.value.argent)
    : '—'
)

const dateJeu = computed(() => {
  if (!partie.value) return ''
  const mois = ['Jan','Fév','Mar','Avr','Mai','Jun','Jul','Aoû','Sep','Oct','Nov','Déc']
  return `${mois[(partie.value.moisActuel ?? 1) - 1]} ${partie.value.anneeActuelle ?? 1985}`
})

function statColor(val) {
  if (val >= 75) return '#22c55e'
  if (val >= 55) return '#f59e0b'
  if (val >= 40) return '#f97316'
  return '#ef4444'
}

async function chargerPartie() {
  const res = await fetch('http://localhost:5219/api/partie/current', { headers: authHeaders() })
  if (res.status === 401) { clearSession(); router.push('/auth'); return }
  if (res.status === 404) { router.push('/home'); return }
  if (res.ok) partie.value = await res.json()
}

async function chargerProchainsCombats() {
  try {
    const res = await fetch('http://localhost:5219/api/combats-planifies', { headers: authHeaders() })
    if (res.ok) prochainsCombats.value = await res.json()
  } catch {}
}

onMounted(async () => {
  try {
    await chargerPartie()
    const ecurieRes = await fetch('http://localhost:5219/api/combattants/ecurie', { headers: authHeaders() })
    if (ecurieRes.ok) ecurieCount.value = (await ecurieRes.json()).length
    await chargerProchainsCombats()
  } catch {
    router.push('/home')
  } finally {
    loading.value = false
  }
})

watch(tab, async (newTab, oldTab) => {
  if (oldTab === 'fights') await chargerProchainsCombats()
})

async function avancerTour() {
  if (avancement.value) return
  avancement.value = true
  try {
    const res = await fetch('http://localhost:5219/api/entrainements/avancer-tour', {
      method: 'POST', headers: authHeaders(),
    })
    if (res.ok) {
      tourResultat.value = await res.json()
      await chargerPartie()
      await chargerProchainsCombats()
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
  if (tourResultat.value?.transitionEpoque)         dialogTransition.value = true
  else if (tourResultat.value?.resultats?.length > 0) dialogResultat.value  = true
  else if (tourResultat.value?.prestigeAugmente)    dialogPrestige.value   = true
  else if (tourResultat.value?.estGameOver)          dialogGameOver.value   = true
  else                                               tourResultat.value     = null
}

function fermerTransition() {
  dialogTransition.value = false
  if (tourResultat.value?.resultats?.length > 0) dialogResultat.value  = true
  else if (tourResultat.value?.prestigeAugmente) dialogPrestige.value   = true
  else if (tourResultat.value?.estGameOver)       dialogGameOver.value   = true
  else                                            tourResultat.value     = null
}

function fermerResultat() {
  dialogResultat.value = false
  if (tourResultat.value?.prestigeAugmente) dialogPrestige.value = true
  else if (tourResultat.value?.estGameOver) dialogGameOver.value = true
  else                                      tourResultat.value   = null
}

function fermerPrestige() {
  dialogPrestige.value = false
  if (tourResultat.value?.estGameOver) dialogGameOver.value = true
  else                                 tourResultat.value   = null
}

function allerAccueil() {
  dialogGameOver.value = false
  tourResultat.value   = null
  router.push('/home')
}

function combatResultIcon(c)  { return c.estNul ? '🟡' : c.estVictoire ? '🟢' : '🔴' }
function combatResultLabel(c) { return c.estNul ? 'NUL' : c.estVictoire ? 'VICTOIRE' : 'DÉFAITE' }
function combatResultColor(c) { return c.estNul ? '#f59e0b' : c.estVictoire ? '#22c55e' : '#ef4444' }

function logout() { clearSession(); router.push('/auth') }

const MOIS_FR = ['Janvier','Février','Mars','Avril','Mai','Juin','Juillet','Août','Septembre','Octobre','Novembre','Décembre']
function moisLabel(n) { return MOIS_FR[(n ?? 1) - 1] }

function typeEntrainementIcon(type) {
  return ({ Striking: '🥊', Lutte: '🤼', Grappling: '⛩️', Conditionnement: '🏋️', Mental: '🧠' })[type] ?? '🏃'
}
function formatMoney(val) { return Number(val ?? 0).toLocaleString('fr-FR') }
</script>

<template>
  <v-app>

    <!-- ── Loading ───────────────────────────────────────────── -->
    <div v-if="loading" class="loading-overlay">
      <v-progress-circular indeterminate color="red-darken-2" size="56" />
    </div>

    <!-- ── Layout 3 colonnes ──────────────────────────────────── -->
    <div v-else class="game-layout">

      <!-- ── Sidebar gauche ─────────────────────────────────── -->
      <aside class="sidebar">
        <div class="sidebar-header">
          <div class="sidebar-logo">
            <span class="logo-mma">MMA</span>
            <span class="logo-manager">MANAGER</span>
          </div>
        </div>

        <nav class="sidebar-nav">
          <button
            v-for="t in TABS"
            :key="t.key"
            class="sidebar-nav-item"
            :class="{ active: tab === t.key, disabled: !t.available }"
            @click="t.available && (tab = t.key)"
          >
            <span class="sidebar-nav-icon">{{ t.icon }}</span>
            <span class="sidebar-nav-label">{{ t.label }}</span>
            <span v-if="!t.available" class="sidebar-nav-soon">bientôt</span>
          </button>
        </nav>

        <div class="sidebar-footer">
          <div v-if="partie" class="sidebar-prestige">
            <span class="sp-label">RÉPUTATION DU GYM</span>
            <div class="prestige-bar">
              <div class="prestige-fill" :style="{ width: (partie.prestigeEcurie / 5 * 100) + '%' }" />
            </div>
            <span class="prestige-value">{{ partie.prestigeEcurie }} / 5</span>
          </div>
          <button class="sidebar-logout" @click="logout">⏻ Déconnexion</button>
        </div>
      </aside>

      <!-- ── Zone principale ─────────────────────────────────── -->
      <div class="main-zone">

        <!-- Topbar compacte -->
        <header class="topbar">
          <div class="topbar-left">
            <span v-if="partie" class="topbar-epoque" :style="{ color: epoqueInfo.color }">
              {{ epoqueInfo.icon }} {{ epoqueInfo.label?.toUpperCase() }}
            </span>
            <span class="topbar-divider">|</span>
            <span v-if="partie" class="topbar-date">📅 {{ dateJeu }}</span>
            <span class="topbar-divider">|</span>
            <span v-if="partie" class="topbar-tour">🔄 Tour {{ partie.tourActuel }}</span>
          </div>
          <div class="topbar-right">
            <div v-if="partie" class="topbar-money">
              <span class="money-label">💰 FONDS</span>
              <span class="money-value">{{ argent }}</span>
            </div>
            <div class="topbar-user">
              <span class="user-email-txt">{{ email }}</span>
              <span class="user-role">MANAGER</span>
            </div>
            <button class="topbar-logout" @click="logout">⏻</button>
          </div>
        </header>

        <!-- Contenu -->
        <main class="content-area">

          <!-- Dashboard -->
          <div v-if="tab === 'dashboard' && partie">
            <v-row>
              <v-col cols="12" md="5">
                <v-card class="dash-card trainer-card" elevation="8" rounded="xl">
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
                    <div class="trainer-stats">
                      <div v-for="stat in partie.entraineur.stats" :key="stat.cle" class="stat-row">
                        <span class="stat-label">{{ stat.nom }}</span>
                        <div class="stat-track">
                          <div class="stat-fill" :style="{ width: stat.valeur + '%', background: statColor(stat.valeur) }" />
                        </div>
                        <span class="stat-val" :style="{ color: statColor(stat.valeur) }">{{ stat.valeur }}</span>
                      </div>
                    </div>
                  </v-card-text>
                </v-card>
              </v-col>

              <v-col cols="12" md="7">
                <v-card class="dash-card mb-4" elevation="8" rounded="xl">
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

                <v-card class="dash-card" elevation="8" rounded="xl">
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
                      <v-btn variant="outlined" rounded="pill" size="small"
                        style="margin-top:12px;border-color:rgba(220,38,38,.5);color:#dc2626"
                        @click="tab = 'fighters'">
                        Recruter un combattant
                      </v-btn>
                    </div>
                    <div v-else class="placeholder-block">
                      <span style="font-size:2.5rem">👊</span>
                      <p>{{ ecurieCount }} combattant{{ ecurieCount > 1 ? 's' : '' }} dans ton écurie.</p>
                      <v-btn variant="outlined" rounded="pill" size="small"
                        style="margin-top:12px;border-color:rgba(220,38,38,.5);color:#dc2626"
                        @click="tab = 'fighters'">
                        Gérer l'écurie
                      </v-btn>
                    </div>
                  </v-card-text>
                </v-card>
              </v-col>
            </v-row>
          </div>

          <div v-else-if="tab === 'fighters'">
            <FightersTab
              :auth-headers="authHeaders"
              :partie="partie"
              @ecurie-updated="ecurieCount = $event"
              @argent-updated="partie.argent = $event"
            />
          </div>

          <div v-else-if="tab === 'training' && partie">
            <TrainingTab :auth-headers="authHeaders" :partie="partie" />
          </div>

          <div v-else-if="tab === 'fights' && partie">
            <CombatPlanifieTab :auth-headers="authHeaders" :partie="partie" />
          </div>

          <div v-else-if="tab === 'finances'">
            <FinancesTab :auth-headers="authHeaders" />
          </div>

          <div v-else-if="tab === 'staff'">
            <StaffTab :auth-headers="authHeaders" />
          </div>

          <div v-else-if="tab === 'rankings' && partie">
            <RankingsTab :auth-headers="authHeaders" :partie="partie" />
          </div>

          <div v-else-if="tab !== 'dashboard'" class="coming-soon">
            <div class="coming-icon">{{ TABS.find(t => t.key === tab)?.icon }}</div>
            <h2 class="coming-title">{{ TABS.find(t => t.key === tab)?.label }}</h2>
            <p class="coming-sub">Cette section est en cours de développement. Elle sera disponible prochainement.</p>
          </div>

        </main>
      </div>

      <!-- ── Panneau droit ─────────────────────────────────────── -->
      <aside class="right-panel">

        <!-- Aperçu du gym -->
        <div class="gym-overview">
          <div class="gym-hero">
            <span class="gym-hero-icon">🥊</span>
            <div v-if="partie" class="gym-hero-info">
              <span class="gym-hero-name">{{ partie.entraineur.prenom }} {{ partie.entraineur.nom }}</span>
              <span class="gym-hero-bg">{{ partie.entraineur.backgroundIcone }} {{ partie.entraineur.backgroundNom }}</span>
            </div>
          </div>
          <div class="gym-stats" v-if="partie">
            <div class="gym-stat">
              <span class="gym-stat-label">COMBATTANTS</span>
              <span class="gym-stat-value">{{ ecurieCount }}</span>
            </div>
            <div class="gym-stat">
              <span class="gym-stat-label">PRESTIGE</span>
              <span class="gym-stat-value gold">{{ '⭐'.repeat(partie.prestigeEcurie) }}</span>
            </div>
          </div>
          <div v-if="partie" class="gym-money">
            <span class="gym-money-label">FONDS DISPONIBLES</span>
            <span class="gym-money-value">{{ argent }}</span>
          </div>
        </div>

        <!-- Prochains combats -->
        <div class="rp-section">
          <div class="rp-section-header">
            <span>PROCHAINS COMBATS</span>
            <span class="rp-section-link" @click="tab = 'fights'">VOIR TOUT</span>
          </div>
          <div v-if="prochainsCombats.length === 0" class="upcoming-empty">
            Aucun combat planifié
          </div>
          <div v-else>
            <div
              v-for="c in prochainsCombats.slice(0, 4)"
              :key="c.combatPlanifieID"
              class="upcoming-fight"
            >
              <div class="upcoming-names">
                <span class="upcoming-fighter">{{ c.combattantPrenom }} {{ c.combattantNom }}</span>
                <span class="upcoming-vs">vs</span>
                <span class="upcoming-opp">{{ c.adversairePrenom }} {{ c.adversaireNom }}</span>
              </div>
              <span class="upcoming-org">{{ c.organisation }}</span>
            </div>
            <div v-if="prochainsCombats.length > 4" class="upcoming-more">
              +{{ prochainsCombats.length - 4 }} autres combats
            </div>
          </div>
        </div>

        <div class="rp-spacer" />

        <!-- Bouton Mois suivant -->
        <button class="btn-next-month" :disabled="avancement" @click="avancerTour">
          <span v-if="avancement" class="btn-spin">⟳</span>
          <span v-else>▶</span>
          {{ avancement ? 'Simulation...' : 'MOIS SUIVANT' }}
        </button>

      </aside>

    </div>

    <!-- ════════════════════════════════════════════════════════
         DIALOGS — inchangés (sauf color="indigo" → red-darken-2)
         ════════════════════════════════════════════════════════ -->

    <!-- ── Dialog résultats combats ────────────────────────── -->
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
            <div class="combat-banner" :style="{ background: combatResultColor(c) + '22' }">
              <span class="combat-banner-icon">{{ combatResultIcon(c) }}</span>
              <span class="combat-banner-label" :style="{ color: combatResultColor(c) }">{{ combatResultLabel(c) }}</span>
              <span class="combat-org">{{ c.organisation }}</span>
            </div>
            <div class="combat-matchup">
              <span class="combat-fighter">{{ c.combattantNom }}</span>
              <span class="combat-vs">VS</span>
              <span class="combat-fighter">{{ c.adversaireNom }}</span>
            </div>
            <div class="combat-details">
              <span class="combat-method">{{ c.methodeVictoire }}</span>
              <span class="combat-detail-text">{{ c.details }}</span>
            </div>
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
                    :class="{ 'round-win': r.gagnantRound === 'Combattant', 'round-loss': r.gagnantRound === 'Adversaire', 'round-draw': r.gagnantRound === 'Egal' }">
                    {{ r.gagnantRound === 'Combattant' ? '✅' : r.gagnantRound === 'Adversaire' ? '❌' : '➖' }}
                    {{ r.scoreCombattant }}–{{ r.scoreAdversaire }}
                  </span>
                  <span class="round-finish-badge" v-if="r.estFinish">🏁 {{ r.methodeFinish }}</span>
                </div>
                <div class="round-actions">{{ r.actionsPrincipales }}</div>
              </div>
            </div>
            <div class="combat-bourse" v-if="c.bourseGagnee">
              <span class="bourse-label">💰 Bourse :</span>
              <span class="bourse-amount" :class="c.estVictoire ? 'bourse-win' : c.estNul ? 'bourse-draw' : 'bourse-loss'">
                +{{ formatMoney(c.bourseGagnee) }} €
              </span>
            </div>
            <div v-if="c.blessureZone" class="combat-injury">
              🏥 Blessure : {{ c.blessureZone }} — Indisponible {{ c.semainesIndispo }} tour{{ c.semainesIndispo > 1 ? 's' : '' }}
            </div>
            <div v-if="c.nouvelleRivalite" class="combat-rivalry-new">
              🔥 Nouvelle rivalité ! {{ c.rivaliteRaison }}
            </div>
            <div v-else-if="c.rivaliteIntensite" class="combat-rivalry-existing">
              🔥 Rivalité intensité {{ c.rivaliteIntensite }}/5 — {{ c.rivaliteRaison }}
            </div>
          </div>
          <div v-if="tourResultat?.contratsTermines?.length > 0" class="contrats-termines">
            <div v-for="ct in tourResultat.contratsTermines" :key="ct.combattantNom + ct.organisationNom" class="contrat-termine-item">
              📄 Contrat terminé : {{ ct.combattantNom }} a complété ses {{ ct.combatsEffectues }} combats avec {{ ct.organisationNom }}. Il est maintenant libre !
            </div>
          </div>

          <div class="finance-summary">
            <div class="finance-title">💰 Bilan financier du mois</div>
            <div class="finance-row" v-if="tourResultat?.combatsResultats?.some(c => c.bourseGagnee)">
              <span>Bourses de combat</span>
              <span class="finance-green">+{{ formatMoney(tourResultat.combatsResultats.reduce((s,c) => s + (c.bourseGagnee||0), 0)) }} €</span>
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
          <v-btn variant="flat" color="red-darken-2" rounded="pill" @click="fermerCombats">Continuer</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- ── Dialog transition d'ère ──────────────────────────── -->
    <v-dialog v-model="dialogTransition" max-width="560" persistent>
      <v-card class="transition-dialog" rounded="xl" elevation="24">
        <div class="transition-header">
          <span class="transition-big-icon">
            {{ tourResultat?.nouvelleEpoque === 'GoldenAge' ? '🏆' : '🧠' }}
          </span>
          <div class="transition-label">Nouvelle ère</div>
          <div class="transition-era">{{ tourResultat?.nouvelleEpoque === 'GoldenAge' ? 'Golden Age' : 'Modern MMA' }}</div>
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
          <v-btn variant="flat" color="amber-darken-2" rounded="pill" size="large" @click="fermerTransition">
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
            <div v-for="r in tourResultat?.resultats" :key="r.combattantID" class="fighter-resultat">
              <div class="fighter-resultat-header">
                <div class="fighter-resultat-left">
                  <span class="fighter-resultat-name">{{ r.prenom }} {{ r.nomFamille }}</span>
                  <span class="fighter-resultat-type">{{ typeEntrainementIcon(r.typeEntrainement) }} {{ r.typeEntrainement }}</span>
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
          <v-btn variant="flat" color="red-darken-2" rounded="pill" @click="fermerResultat">Continuer</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- ── Dialog Prestige ──────────────────────────────────── -->
    <v-dialog v-model="dialogPrestige" max-width="480" persistent>
      <v-card class="prestige-dialog" rounded="xl" elevation="24">
        <div class="prestige-header">
          <div class="prestige-stars">{{ '⭐'.repeat(tourResultat?.prestigeEcurie ?? 1) }}</div>
          <div class="prestige-dialog-label">Prestige de l'écurie</div>
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

    <!-- ── Dialog Game Over ─────────────────────────────────── -->
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
/* ── Variables ─────────────────────────────────────────────── */
.game-layout {
  --bg-primary:    #0a0a0f;
  --bg-secondary:  #12121a;
  --bg-tertiary:   #1a1a2e;
  --accent-red:    #dc2626;
  --accent-red-dk: #991b1b;
  --accent-gold:   #f59e0b;
  --accent-gold-l: #fbbf24;
  --text-primary:  #e2e8f0;
  --text-secondary:#94a3b8;
  --text-muted:    #475569;
  --border:        rgba(255,255,255,.06);
}

/* ── Loading ────────────────────────────────────────────────── */
.loading-overlay {
  position: fixed;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  background: #0a0a0f;
  z-index: 9999;
}

/* ── Layout ─────────────────────────────────────────────────── */
.game-layout {
  display: grid;
  grid-template-columns: 220px 1fr 300px;
  min-height: 100vh;
  background: var(--bg-primary);
  color: var(--text-primary);
}

/* ── Sidebar ────────────────────────────────────────────────── */
.sidebar {
  background: var(--bg-secondary);
  border-right: 1px solid var(--border);
  display: flex;
  flex-direction: column;
  position: sticky;
  top: 0;
  height: 100vh;
  overflow-y: auto;
}

.sidebar-header {
  padding: 24px 16px 18px;
  border-bottom: 1px solid var(--border);
}

.sidebar-logo {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 2px;
}

.logo-mma {
  font-size: 2rem;
  font-weight: 900;
  color: var(--accent-red);
  letter-spacing: .05em;
  line-height: 1;
  text-shadow: 0 0 20px rgba(220,38,38,.35);
}

.logo-manager {
  font-size: .62rem;
  font-weight: 700;
  letter-spacing: .32em;
  color: var(--text-secondary);
  text-transform: uppercase;
}

.sidebar-nav {
  flex: 1;
  padding: 10px 8px;
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.sidebar-nav-item {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 10px 12px;
  border-radius: 8px;
  background: transparent;
  border: none;
  color: var(--text-secondary);
  font-size: .85rem;
  font-weight: 500;
  cursor: pointer;
  transition: all .15s;
  text-align: left;
  width: 100%;
  position: relative;
}

.sidebar-nav-item:hover:not(.disabled) {
  background: rgba(255,255,255,.04);
  color: var(--text-primary);
}

.sidebar-nav-item.active {
  background: rgba(220,38,38,.1);
  color: #fca5a5;
  font-weight: 700;
  border-left: 3px solid var(--accent-red);
  padding-left: 9px;
}

.sidebar-nav-item.disabled {
  opacity: .35;
  cursor: not-allowed;
}

.sidebar-nav-icon { font-size: 1rem; width: 22px; text-align: center; flex-shrink: 0; }
.sidebar-nav-label { flex: 1; }

.sidebar-nav-soon {
  font-size: .58rem;
  background: rgba(255,255,255,.05);
  color: var(--text-muted);
  padding: 1px 6px;
  border-radius: 4px;
  margin-left: auto;
}

.sidebar-footer {
  padding: 14px 12px 18px;
  border-top: 1px solid var(--border);
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.sidebar-prestige { display: flex; flex-direction: column; gap: 5px; }

.sp-label {
  font-size: .58rem;
  font-weight: 700;
  color: var(--text-muted);
  letter-spacing: .12em;
  text-transform: uppercase;
}

.prestige-bar {
  height: 5px;
  background: rgba(255,255,255,.06);
  border-radius: 3px;
  overflow: hidden;
}

.prestige-fill {
  height: 100%;
  background: linear-gradient(90deg, var(--accent-red), var(--accent-gold));
  border-radius: 3px;
  transition: width .4s;
}

.prestige-value { font-size: .72rem; color: var(--accent-gold); font-weight: 700; }

.sidebar-logout {
  font-size: .72rem;
  color: var(--text-muted);
  background: none;
  border: 1px solid var(--border);
  border-radius: 7px;
  padding: 7px 10px;
  cursor: pointer;
  transition: all .15s;
  text-align: center;
  width: 100%;
}

.sidebar-logout:hover { color: #ef4444; border-color: rgba(239,68,68,.3); background: rgba(239,68,68,.05); }

/* ── Main zone ──────────────────────────────────────────────── */
.main-zone {
  display: flex;
  flex-direction: column;
  min-height: 100vh;
}

/* ── Topbar ─────────────────────────────────────────────────── */
.topbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 10px 24px;
  background: var(--bg-secondary);
  border-bottom: 1px solid var(--border);
  flex-shrink: 0;
  position: sticky;
  top: 0;
  z-index: 10;
}

.topbar-left {
  display: flex;
  align-items: center;
  gap: 10px;
  font-size: .7rem;
  font-weight: 600;
  letter-spacing: .04em;
}

.topbar-epoque { font-weight: 700; }
.topbar-date, .topbar-tour { color: var(--text-secondary); }
.topbar-divider { color: var(--text-muted); }

.topbar-right { display: flex; align-items: center; gap: 18px; }

.topbar-money { display: flex; flex-direction: column; align-items: flex-end; }

.money-label {
  font-size: .56rem;
  font-weight: 700;
  letter-spacing: .1em;
  color: var(--text-muted);
}

.money-value { font-size: .92rem; font-weight: 800; color: var(--accent-gold); line-height: 1.1; }

.topbar-user { display: flex; flex-direction: column; align-items: flex-end; }
.user-email-txt { font-size: .7rem; color: var(--text-secondary); }
.user-role { font-size: .58rem; font-weight: 700; color: var(--accent-red); letter-spacing: .1em; }

.topbar-logout {
  background: none;
  border: 1px solid var(--border);
  color: var(--text-muted);
  padding: 5px 9px;
  border-radius: 6px;
  font-size: .8rem;
  cursor: pointer;
  transition: all .15s;
  line-height: 1;
}
.topbar-logout:hover { border-color: rgba(239,68,68,.4); color: #ef4444; }

/* ── Content ────────────────────────────────────────────────── */
.content-area {
  flex: 1;
  padding: 24px;
  overflow-y: auto;
}

/* ── Right panel ────────────────────────────────────────────── */
.right-panel {
  background: var(--bg-secondary);
  border-left: 1px solid var(--border);
  display: flex;
  flex-direction: column;
  position: sticky;
  top: 0;
  height: 100vh;
  overflow-y: auto;
  padding: 16px;
}

.gym-overview { margin-bottom: 14px; }

.gym-hero {
  height: 90px;
  background: linear-gradient(160deg, rgba(220,38,38,.18) 0%, rgba(245,158,11,.08) 60%, rgba(18,18,26,.9) 100%);
  border-radius: 10px;
  border: 1px solid rgba(220,38,38,.15);
  margin-bottom: 10px;
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 0 14px;
  overflow: hidden;
}

.gym-hero-icon { font-size: 2rem; opacity: .65; flex-shrink: 0; }

.gym-hero-info { display: flex; flex-direction: column; gap: 2px; min-width: 0; }

.gym-hero-name {
  font-size: .82rem;
  font-weight: 700;
  color: var(--text-primary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.gym-hero-bg { font-size: .68rem; color: var(--text-muted); }

.gym-stats { display: flex; gap: 6px; margin-bottom: 8px; }

.gym-stat {
  flex: 1;
  padding: 7px 8px;
  background: rgba(255,255,255,.03);
  border-radius: 8px;
  border: 1px solid var(--border);
  text-align: center;
}

.gym-stat-label {
  display: block;
  font-size: .56rem;
  font-weight: 700;
  color: var(--text-muted);
  letter-spacing: .1em;
  text-transform: uppercase;
  margin-bottom: 3px;
}

.gym-stat-value { font-size: .9rem; font-weight: 800; color: var(--text-primary); display: block; }
.gym-stat-value.gold { color: var(--accent-gold); font-size: .75rem; }

.gym-money {
  padding: 7px 10px;
  background: rgba(245,158,11,.06);
  border-radius: 8px;
  border: 1px solid rgba(245,158,11,.15);
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.gym-money-label { font-size: .58rem; font-weight: 700; letter-spacing: .08em; color: var(--text-muted); text-transform: uppercase; }
.gym-money-value { font-size: .88rem; font-weight: 800; color: var(--accent-gold); }

.rp-section {
  padding-top: 14px;
  border-top: 1px solid var(--border);
}

.rp-section-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 10px;
  font-size: .62rem;
  font-weight: 700;
  letter-spacing: .08em;
  color: var(--text-muted);
}

.rp-section-link {
  font-size: .6rem;
  color: var(--accent-red);
  cursor: pointer;
  font-weight: 700;
}
.rp-section-link:hover { opacity: .75; }

.upcoming-empty {
  font-size: .76rem;
  color: var(--text-muted);
  text-align: center;
  padding: 14px 0;
  font-style: italic;
}

.upcoming-fight {
  margin-bottom: 7px;
  padding: 7px 9px;
  background: rgba(255,255,255,.03);
  border-radius: 8px;
  border: 1px solid var(--border);
}

.upcoming-names {
  display: flex;
  align-items: center;
  gap: 4px;
  font-size: .74rem;
  margin-bottom: 2px;
  overflow: hidden;
}

.upcoming-fighter {
  font-weight: 700;
  color: var(--text-primary);
  flex: 1;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.upcoming-vs { font-size: .6rem; color: var(--text-muted); flex-shrink: 0; }

.upcoming-opp {
  color: var(--text-secondary);
  flex: 1;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  text-align: right;
}

.upcoming-org { font-size: .62rem; color: var(--text-muted); display: block; }
.upcoming-more { font-size: .68rem; color: var(--text-muted); text-align: center; padding-top: 5px; }

.rp-spacer { flex: 1; min-height: 12px; }

/* ── Bouton Mois suivant ────────────────────────────────────── */
.btn-next-month {
  width: 100%;
  padding: 15px;
  background: linear-gradient(135deg, var(--accent-red), var(--accent-red-dk));
  color: white;
  border: none;
  border-radius: 10px;
  font-size: .88rem;
  font-weight: 800;
  letter-spacing: .06em;
  cursor: pointer;
  transition: all .2s;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 10px;
  flex-shrink: 0;
}

.btn-next-month:hover:not(:disabled) {
  transform: translateY(-2px);
  box-shadow: 0 6px 24px rgba(220,38,38,.4);
}

.btn-next-month:disabled { opacity: .5; cursor: not-allowed; transform: none; }

.btn-spin { display: inline-block; animation: spin 1s linear infinite; }
@keyframes spin { to { transform: rotate(360deg); } }

/* ── Coming soon ────────────────────────────────────────────── */
.coming-soon { text-align: center; padding: 80px 20px; }
.coming-icon { font-size: 3.5rem; margin-bottom: 16px; }
.coming-title { font-size: 1.5rem; font-weight: 800; color: var(--text-primary); margin-bottom: 8px; }
.coming-sub { color: var(--text-muted); font-size: .9rem; }

/* ── Dashboard ──────────────────────────────────────────────── */
.dash-card {
  background: var(--bg-secondary) !important;
  border: 1px solid var(--border) !important;
}

.trainer-card { border-color: rgba(220,38,38,.2) !important; }

.card-label {
  font-size: .62rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: .1em;
  color: var(--accent-red);
  margin-bottom: 4px;
}

.trainer-meta { display: flex; flex-wrap: wrap; gap: 8px; }

.meta-chip {
  font-size: .78rem;
  padding: 3px 10px;
  border-radius: 20px;
  background: rgba(255,255,255,.05);
  border: 1px solid var(--border);
  color: var(--text-secondary);
}

.bg-desc { color: var(--text-secondary); line-height: 1.6; }
.trainer-stats { display: flex; flex-direction: column; gap: 7px; }

.stat-row { display: grid; grid-template-columns: 130px 1fr 32px; align-items: center; gap: 8px; }
.stat-label { font-size: .78rem; color: var(--text-secondary); }
.stat-track { height: 5px; background: rgba(255,255,255,.07); border-radius: 5px; overflow: hidden; }
.stat-fill { height: 100%; border-radius: 5px; transition: width .4s; }
.stat-val { font-size: .78rem; font-weight: 700; text-align: right; }

.budget-display { font-size: 2rem; font-weight: 900; color: var(--accent-gold); }

.placeholder-block { text-align: center; color: var(--text-muted); padding: 10px 0; }
.placeholder-block p { margin: 8px 0; font-size: .88rem; }

/* ── Dialogs ────────────────────────────────────────────────── */
.resultat-dialog {
  background: #0f1729 !important;
  border: 1px solid rgba(220,38,38,.2);
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

.resultat-date { font-size: .8rem; font-weight: 400; color: #dc2626; }
.resultat-body { padding: 8px 24px 16px; }
.resultat-empty { color: #475569; font-size: .9rem; text-align: center; padding: 24px 0; }

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

.fighter-resultat-header { display: flex; align-items: flex-start; justify-content: space-between; margin-bottom: 8px; gap: 8px; }
.fighter-resultat-left { display: flex; flex-direction: column; gap: 3px; }
.fighter-resultat-name { font-weight: 600; font-size: .9rem; color: #e2e8f0; }

.fighter-resultat-type {
  font-size: .74rem;
  padding: 2px 10px;
  border-radius: 20px;
  background: rgba(220,38,38,.15);
  color: #fca5a5;
  display: inline-block;
  width: fit-content;
}

.fighter-resultat-bilan { font-size: .72rem; color: #22c55e; white-space: nowrap; align-self: flex-start; }
.gains-empty { font-size: .76rem; color: #475569; font-style: italic; }
.gains-list { display: flex; flex-wrap: wrap; gap: 6px; }

.gain-row {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 4px 10px;
  border-radius: 8px;
  background: rgba(34,197,94,.08);
  border: 1px solid rgba(34,197,94,.15);
}

.gain-stat { font-size: .75rem; color: #94a3b8; }
.gain-value { font-size: .8rem; font-weight: 700; color: #22c55e; }
.resultat-actions { padding: 8px 24px 20px; justify-content: flex-end; }

/* ── Combats dialog ─────────────────────────────────────────── */
.combat-resultat { margin-bottom: 14px; border-radius: 12px; border: 1px solid rgba(255,255,255,.08); overflow: hidden; }
.combat-banner { display: flex; align-items: center; gap: 8px; padding: 6px 14px; }
.combat-banner-icon { font-size: 1.1rem; }
.combat-banner-label { font-size: .8rem; font-weight: 800; letter-spacing: .08em; text-transform: uppercase; }
.combat-org { margin-left: auto; font-size: .72rem; color: #64748b; }
.combat-matchup { display: flex; align-items: center; justify-content: center; gap: 12px; padding: 8px 14px; }
.combat-fighter { font-size: .88rem; font-weight: 600; color: #e2e8f0; }
.combat-vs { font-size: .7rem; font-weight: 800; color: #475569; letter-spacing: .05em; }
.combat-details { display: flex; align-items: center; gap: 8px; padding: 6px 14px 10px; }

.combat-method {
  font-size: .78rem;
  font-weight: 700;
  padding: 2px 8px;
  border-radius: 6px;
  background: rgba(220,38,38,.15);
  color: #fca5a5;
}

.combat-detail-text { font-size: .78rem; color: #64748b; }

.combat-rounds { margin: 6px 0 4px; display: flex; flex-direction: column; gap: 3px; border-top: 1px solid rgba(255,255,255,.08); padding-top: 6px; }

.round-row { display: flex; flex-direction: column; gap: 3px; font-size: .74rem; color: #94a3b8; padding: 5px 6px; border-radius: 6px; }
.round-row.round-finish { background: rgba(251,191,36,.08); border: 1px solid rgba(251,191,36,.15); }
.round-top { display: flex; align-items: center; gap: 8px; }
.round-num { font-weight: 700; color: #64748b; min-width: 22px; font-size: .76rem; }
.round-winner { min-width: 62px; font-weight: 600; font-size: .76rem; }
.round-win   { color: #4ade80; }
.round-loss  { color: #f87171; }
.round-draw  { color: #94a3b8; }
.round-actions { color: #94a3b8; font-size: .73rem; line-height: 1.4; padding-left: 30px; white-space: normal; }
.round-finish-badge { font-weight: 700; color: #fbbf24; font-size: .72rem; white-space: nowrap; margin-left: auto; }

.combat-bourse { display: flex; align-items: center; gap: 6px; padding: 4px 14px 8px; }
.bourse-label { font-size: .78rem; color: #94a3b8; }
.bourse-amount { font-size: .85rem; font-weight: 700; }
.bourse-win  { color: #22c55e; }
.bourse-draw { color: #f59e0b; }
.bourse-loss { color: #f97316; }
.combat-injury { color: #ef4444; font-size: .82rem; margin: 4px 8px 8px; padding: 4px 8px; background: rgba(239,68,68,.08); border-radius: 6px; }
.combat-rivalry-new { color: #f59e0b; font-size: .82rem; margin-top: 6px; padding: 4px 8px; background: rgba(245,158,11,.08); border-radius: 6px; font-weight: 600; }
.combat-rivalry-existing { color: #f59e0b; font-size: .78rem; margin-top: 4px; }

/* ── Contrats terminés ──────────────────────────────────────── */
.contrats-termines { margin: 12px 0; }
.contrat-termine-item { background: rgba(59,130,246,.08); color: #93c5fd; font-size: .82rem; padding: 8px 12px; border-radius: 6px; margin-bottom: 4px; }

/* ── Finance summary ────────────────────────────────────────── */
.finance-summary { margin-top: 16px; padding: 12px 14px; border-radius: 10px; background: rgba(255,255,255,.03); border: 1px solid rgba(255,255,255,.06); }
.finance-summary--training { margin: 12px 0 4px; }
.finance-title { font-size: .8rem; font-weight: 700; color: #94a3b8; margin-bottom: 8px; text-transform: uppercase; letter-spacing: .06em; }
.finance-row { display: flex; justify-content: space-between; font-size: .83rem; color: #94a3b8; margin-bottom: 4px; }
.finance-neg { color: #ef4444; font-weight: 600; }
.finance-green { color: #22c55e; font-weight: 600; }
.finance-balance { margin-top: 6px; padding-top: 6px; border-top: 1px solid rgba(255,255,255,.06); color: #e2e8f0; font-weight: 700; }

/* ── Transition dialog ──────────────────────────────────────── */
.transition-dialog {
  background: linear-gradient(160deg, #1e1b4b 0%, #0f172a 100%) !important;
  border: 1px solid rgba(99,102,241,.4);
  text-align: center;
}
.transition-header { padding: 32px 24px 8px; display: flex; flex-direction: column; align-items: center; gap: 6px; }
.transition-big-icon { font-size: 3.5rem; }
.transition-label { font-size: .8rem; text-transform: uppercase; letter-spacing: .1em; color: #6366f1; margin-top: 4px; }
.transition-era { font-size: 1.8rem; font-weight: 900; color: #e2e8f0; }
.transition-year { font-size: 1rem; font-weight: 700; color: #a5b4fc; }
.transition-body { padding: 12px 28px 20px; }
.transition-line { font-size: .88rem; color: #94a3b8; line-height: 1.7; margin-bottom: 8px; }
.transition-line-main { font-size: 1rem; font-weight: 600; color: #e2e8f0; }

/* ── Prestige dialog ────────────────────────────────────────── */
.prestige-dialog {
  background: linear-gradient(160deg, #1c1a05 0%, #0f172a 100%) !important;
  border: 1px solid rgba(251,191,36,.4);
  text-align: center;
}
.prestige-header { padding: 32px 24px 8px; display: flex; flex-direction: column; align-items: center; gap: 6px; }
.prestige-stars { font-size: 2rem; letter-spacing: 4px; }
.prestige-dialog-label { font-size: .8rem; text-transform: uppercase; letter-spacing: .1em; color: #f59e0b; margin-top: 4px; }
.prestige-niveau { font-size: 1.6rem; font-weight: 900; color: #fbbf24; }
.prestige-body { padding: 8px 28px 16px; color: #94a3b8; font-size: .88rem; line-height: 1.7; }
.prestige-body p { margin-bottom: 8px; }
.prestige-unlock {
  display: flex; align-items: flex-start; gap: 10px;
  margin-top: 12px; padding: 10px 14px; border-radius: 10px;
  background: rgba(251,191,36,.08); border: 1px solid rgba(251,191,36,.2);
  color: #e2e8f0; font-size: .84rem; text-align: left;
}
.prestige-unlock-icon { font-size: 1.1rem; flex-shrink: 0; }

/* ── Game Over ──────────────────────────────────────────────── */
.gameover-dialog {
  background: linear-gradient(160deg, #1a0505 0%, #0f172a 100%) !important;
  border: 1px solid rgba(239,68,68,.5);
  text-align: center;
}
.gameover-header { padding: 36px 24px 12px; display: flex; flex-direction: column; align-items: center; gap: 8px; }
.gameover-icon  { font-size: 3.5rem; }
.gameover-title { font-size: 2rem; font-weight: 900; color: #ef4444; letter-spacing: .06em; }
.gameover-sub   { font-size: .9rem; color: #94a3b8; }
.gameover-body  { padding: 8px 28px 16px; color: #94a3b8; font-size: .88rem; line-height: 1.7; }
.gameover-body p { margin-bottom: 8px; }
.gameover-solde {
  margin-top: 14px; padding: 10px 16px; border-radius: 10px;
  background: rgba(239,68,68,.1); border: 1px solid rgba(239,68,68,.2);
  color: #e2e8f0; font-size: .9rem;
}
</style>
