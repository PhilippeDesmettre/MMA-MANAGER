<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useAuth } from '../composables/useAuth'

const router = useRouter()
const { authHeaders, clearSession } = useAuth()

// ── Form state ────────────────────────────────────────────────
const step = ref(1) // 1: identité | 2: background | 3: époque
const prenom         = ref('')
const nom            = ref('')
const paysOrigineID  = ref(null)
const paysResID      = ref(null)
const backgroundID   = ref(null)
const epoque         = ref('')

// ── Data ──────────────────────────────────────────────────────
const pays       = ref([])
const backgrounds = ref([])
const submitting  = ref(false)
const error       = ref('')

const EPOQUES = [
  {
    key: 'NoRules',
    icon: '🔥',
    label: 'Underground Era',
    years: '1985 – 1999',
    desc: 'Vale Tudo, UFC origines — presque aucune règle, styles opposés, combattants ultra-spécialisés. Imprévisible et brutal.',
    color: '#ef4444',
  },
  {
    key: 'GoldenAge',
    icon: '🏆',
    label: 'Golden Age',
    years: '2000 – 2012',
    desc: 'PRIDE, Fedor, Wanderlei — le spectacle avant tout. Soccer kicks, ring au lieu de cage, légendes vivantes.',
    color: '#f59e0b',
  },
  {
    key: 'Modern',
    icon: '🧠',
    label: 'Modern MMA',
    years: '2013 – Aujourd\'hui',
    desc: 'Domination UFC, combattants ultra-complets, analyse scientifique. Très stratégique, gestion de carrière complexe.',
    color: '#6366f1',
  },
]

const selectedBackground = computed(() =>
  backgrounds.value.find(b => b.backgroundID === backgroundID.value) ?? null
)

// Navigation steps
const step1Valid = computed(() => prenom.value.trim() && nom.value.trim() && paysOrigineID.value && paysResID.value)
const step2Valid = computed(() => !!backgroundID.value)
const step3Valid = computed(() => !!epoque.value)

onMounted(async () => {
  const [paysRes, bgRes] = await Promise.all([
    fetch('http://localhost:5219/api/pays'),
    fetch('http://localhost:5219/api/backgrounds'),
  ])
  pays.value       = await paysRes.json()
  backgrounds.value = await bgRes.json()
})

async function submit() {
  if (!step1Valid.value || !step2Valid.value || !step3Valid.value) return
  submitting.value = true
  error.value = ''
  try {
    const res = await fetch('http://localhost:5219/api/partie', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', ...authHeaders() },
      body: JSON.stringify({
        prenom: prenom.value,
        nom: nom.value,
        paysOrigineID: paysOrigineID.value,
        paysResidenceID: paysResID.value,
        backgroundID: backgroundID.value,
        epoque: epoque.value,
      })
    })
    if (res.status === 401) {
      clearSession()
      router.push('/auth')
      return
    }
    if (!res.ok) {
      error.value = await res.text()
      return
    }
    router.push('/game')
  } catch {
    error.value = 'Erreur de connexion au serveur.'
  } finally {
    submitting.value = false
  }
}

// Stat display helpers
const STAT_LABELS = {
  bonusStriking: 'Frappe debout', bonusLutte: 'Wrestling',
  bonusGrappling: 'Grappling', bonusConditioning: 'Conditionnement',
  bonusStamina: 'Endurance', bonusMental: 'Mental',
  bonusStrategie: 'Stratégie', bonusNegociation: 'Négociation',
  bonusMotivation: 'Motivation',
}

function getStatBars(bg) {
  return Object.entries(STAT_LABELS)
    .map(([key, label]) => ({ label, base: 30, bonus: bg[key] ?? 0 }))
    .filter(s => s.bonus > 0)
    .sort((a, b) => b.bonus - a.bonus)
}
</script>

<template>
  <v-app>
    <v-main class="app-shell">
      <v-container class="create-wrap">

        <!-- Header -->
        <div class="create-header">
          <v-btn variant="text" size="small" style="color:#475569" @click="router.push('/home')">
            ← Retour
          </v-btn>
          <p class="hero-kicker mb-0">Nouvelle Partie</p>
        </div>

        <h1 class="create-title">Créer ton entraîneur</h1>

        <!-- Stepper header -->
        <div class="step-header">
          <div
            v-for="(s, i) in ['Identité', 'Background', 'Époque']"
            :key="i"
            class="step-item"
            :class="{ active: step === i + 1, done: step > i + 1 }"
          >
            <div class="step-dot">{{ step > i + 1 ? '✓' : i + 1 }}</div>
            <span class="step-label">{{ s }}</span>
          </div>
        </div>

        <!-- ── STEP 1 : Identité ──────────────────────────── -->
        <div v-if="step === 1" class="step-card">
          <v-row>
            <v-col cols="12" sm="6">
              <v-text-field
                v-model="prenom"
                label="Prénom"
                variant="outlined"
                rounded="lg"
                class="auth-field"
              />
            </v-col>
            <v-col cols="12" sm="6">
              <v-text-field
                v-model="nom"
                label="Nom"
                variant="outlined"
                rounded="lg"
                class="auth-field"
              />
            </v-col>
            <v-col cols="12" sm="6">
              <v-autocomplete
                v-model="paysOrigineID"
                :items="pays"
                item-title="nom"
                item-value="paysID"
                label="Pays d'origine"
                variant="outlined"
                rounded="lg"
                class="auth-field"
              />
            </v-col>
            <v-col cols="12" sm="6">
              <v-autocomplete
                v-model="paysResID"
                :items="pays"
                item-title="nom"
                item-value="paysID"
                label="Pays de résidence"
                variant="outlined"
                rounded="lg"
                class="auth-field"
              />
            </v-col>
          </v-row>

          <v-btn
            block size="large" rounded="pill"
            class="home-btn-primary mt-2"
            :disabled="!step1Valid"
            @click="step = 2"
          >
            Suivant →
          </v-btn>
        </div>

        <!-- ── STEP 2 : Background ────────────────────────── -->
        <div v-if="step === 2" class="step-card">
          <p class="step-hint">Ton background définit tes stats de départ en tant qu'entraîneur.</p>

          <div class="bg-grid">
            <div
              v-for="bg in backgrounds"
              :key="bg.backgroundID"
              class="bg-card"
              :class="{ selected: backgroundID === bg.backgroundID }"
              @click="backgroundID = bg.backgroundID"
            >
              <span class="bg-icon">{{ bg.icone }}</span>
              <span class="bg-name">{{ bg.nom }}</span>
            </div>
          </div>

          <!-- Détail du background sélectionné -->
          <transition name="fade">
            <div v-if="selectedBackground" class="bg-detail">
              <p class="bg-desc">{{ selectedBackground.description }}</p>
              <div class="bg-stats">
                <div
                  v-for="stat in getStatBars(selectedBackground)"
                  :key="stat.label"
                  class="bg-stat-row"
                >
                  <span class="bg-stat-label">{{ stat.label }}</span>
                  <div class="bg-stat-track">
                    <div class="bg-stat-base" :style="{ width: stat.base + '%' }" />
                    <div class="bg-stat-bonus" :style="{ width: stat.bonus + '%' }" />
                  </div>
                  <span class="bg-stat-val">{{ stat.base + stat.bonus }}</span>
                </div>
              </div>
            </div>
          </transition>

          <div class="d-flex gap-3 mt-4">
            <v-btn variant="outlined" rounded="pill" class="home-btn-secondary flex-grow-1" @click="step = 1">
              ← Retour
            </v-btn>
            <v-btn
              rounded="pill" class="home-btn-primary flex-grow-1"
              :disabled="!step2Valid"
              @click="step = 3"
            >
              Suivant →
            </v-btn>
          </div>
        </div>

        <!-- ── STEP 3 : Époque ────────────────────────────── -->
        <div v-if="step === 3" class="step-card">
          <p class="step-hint">L'époque choisie définit le contexte du jeu, les règles et l'ambiance des organisations.</p>

          <div class="epoque-grid">
            <div
              v-for="e in EPOQUES"
              :key="e.key"
              class="epoque-card"
              :class="{ selected: epoque === e.key }"
              :style="epoque === e.key ? `--epoque-color: ${e.color}` : ''"
              @click="epoque = e.key"
            >
              <div class="epoque-icon">{{ e.icon }}</div>
              <div class="epoque-label">{{ e.label }}</div>
              <div class="epoque-years">{{ e.years }}</div>
              <p class="epoque-desc">{{ e.desc }}</p>
            </div>
          </div>

          <v-alert v-if="error" type="error" variant="tonal" rounded="lg" class="mb-4">
            {{ error }}
          </v-alert>

          <div class="d-flex gap-3 mt-4">
            <v-btn variant="outlined" rounded="pill" class="home-btn-secondary flex-grow-1" @click="step = 2">
              ← Retour
            </v-btn>
            <v-btn
              rounded="pill" class="home-btn-primary flex-grow-1"
              :disabled="!step3Valid"
              :loading="submitting"
              @click="submit"
            >
              Lancer la partie 🚀
            </v-btn>
          </div>
        </div>

      </v-container>
    </v-main>
  </v-app>
</template>
