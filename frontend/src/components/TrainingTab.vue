<script setup>
import { ref, computed, onMounted } from 'vue'

const props = defineProps({
  authHeaders: { type: Function, required: true },
  partie:      { type: Object,   required: true },
})

const emit = defineEmits(['tour-avance'])

const API = 'http://localhost:5219/api'

// ── State ─────────────────────────────────────────────────────
const ecurie      = ref([])
const planifies   = ref([])  // { combattantID, typeEntrainement, ... }
const expanded    = ref(null) // combattantID du panneau ouvert
const loading     = ref(true)
const avancement  = ref(false)

// ── Constantes ────────────────────────────────────────────────
const TYPES_ENTRAINEMENT = [
  { key: 'Striking',        label: 'Striking',        icon: '🥊', desc: 'Frappe debout, puissance, précision' },
  { key: 'Lutte',           label: 'Lutte',           icon: '🤼', desc: 'Wrestling, takedown, anti-takedown' },
  { key: 'Grappling',       label: 'Grappling',       icon: '⛩️', desc: 'Jiu-Jitsu, submission, évasion' },
  { key: 'Conditionnement', label: 'Conditionnement', icon: '🏋️', desc: 'Force, vitesse, agilité, cardio' },
  { key: 'Mental',          label: 'Mental',          icon: '🧠', desc: 'Mental, expérience, adaptation' },
]

const CAPACITY_MAX = 2 // 1 staff × 2 entraînements

// ── Computed ──────────────────────────────────────────────────
const capaciteUtilisee = computed(() => planifies.value.length)

const dateJeu = computed(() => {
  const mois = [
    'Janvier','Février','Mars','Avril','Mai','Juin',
    'Juillet','Août','Septembre','Octobre','Novembre','Décembre',
  ]
  return `${mois[(props.partie.moisActuel ?? 1) - 1]} ${props.partie.anneeActuelle ?? 1985}`
})

// ── Chargement ────────────────────────────────────────────────
async function charger() {
  loading.value = true
  try {
    const [ecurieRes, planifiesRes] = await Promise.all([
      fetch(`${API}/combattants/ecurie`, { headers: props.authHeaders() }),
      fetch(`${API}/entrainements/planifies`, { headers: props.authHeaders() }),
    ])
    if (ecurieRes.ok)    ecurie.value    = await ecurieRes.json()
    if (planifiesRes.ok) planifies.value = await planifiesRes.json()
  } finally {
    loading.value = false
  }
}

onMounted(charger)

// ── Helpers ───────────────────────────────────────────────────
function planifiePour(combattantID) {
  return planifies.value.find(p => p.combattantID === combattantID) ?? null
}

function typeLabel(key) {
  return TYPES_ENTRAINEMENT.find(t => t.key === key) ?? { icon: '❓', label: key }
}

// ── Actions ───────────────────────────────────────────────────
async function planifier(combattantID, typeEntrainement) {
  // Si déjà planifié pour ce combattant, on remplace (le backend gère)
  const alreadyPlanifie = planifiePour(combattantID)
  if (!alreadyPlanifie && capaciteUtilisee.value >= CAPACITY_MAX) return

  const res = await fetch(`${API}/entrainements/planifier`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', ...props.authHeaders() },
    body: JSON.stringify({ combattantID, typeEntrainement }),
  })
  if (res.ok) {
    await charger()
    expanded.value = null
  }
}

async function annuler(ep) {
  const res = await fetch(`${API}/entrainements/${ep.entrainementPlanifieID}/annuler`, {
    method: 'DELETE',
    headers: props.authHeaders(),
  })
  if (res.ok) await charger()
}
</script>

<template>
  <div class="training-tab">

    <!-- En-tête capacité staff -->
    <div class="training-header">
      <div class="training-header-left">
        <span class="training-title">Entraînement</span>
        <span class="training-date">{{ dateJeu }}</span>
      </div>
      <div class="staff-capacity">
        <span class="staff-label">Staff</span>
        <div class="staff-slots">
          <div
            v-for="i in CAPACITY_MAX"
            :key="i"
            class="staff-slot"
            :class="{ used: i <= capaciteUtilisee }"
          />
        </div>
        <span class="staff-count">{{ capaciteUtilisee }}/{{ CAPACITY_MAX }}</span>
      </div>
    </div>

    <div v-if="loading" class="text-center py-12">
      <v-progress-circular indeterminate color="indigo" size="36" />
    </div>

    <div v-else-if="ecurie.length === 0" class="empty-state">
      <span style="font-size:2.5rem">👊</span>
      <p>Aucun combattant dans ton écurie. Recrute d'abord des combattants.</p>
    </div>

    <div v-else class="fighter-list">
      <div
        v-for="fighter in ecurie"
        :key="fighter.combattantID"
        class="fighter-row"
      >
        <!-- Ligne principale -->
        <div
          class="fighter-row-main"
          :class="{ 'is-expanded': expanded === fighter.combattantID }"
          @click="expanded = expanded === fighter.combattantID ? null : fighter.combattantID"
        >
          <div class="fighter-row-info">
            <span class="fighter-name">{{ fighter.prenom }} {{ fighter.nom }}</span>
            <span class="fighter-meta">
              {{ fighter.categoriePoids }} · {{ fighter.stylePrincipal }} · {{ fighter.noteGlobale }}
            </span>
          </div>

          <div class="fighter-row-right">
            <!-- Badge entraînement planifié -->
            <template v-if="planifiePour(fighter.combattantID)">
              <span class="training-badge">
                {{ typeLabel(planifiePour(fighter.combattantID).typeEntrainement).icon }}
                {{ planifiePour(fighter.combattantID).typeEntrainement }}
              </span>
              <button
                class="cancel-btn"
                @click.stop="annuler(planifiePour(fighter.combattantID))"
                title="Annuler l'entraînement"
              >✕</button>
            </template>
            <template v-else>
              <span class="no-training" :class="{ 'capacity-full': capaciteUtilisee >= CAPACITY_MAX }">
                {{ capaciteUtilisee >= CAPACITY_MAX ? 'Complet' : 'Aucun entraînement' }}
              </span>
            </template>

            <span class="expand-icon">{{ expanded === fighter.combattantID ? '▲' : '▼' }}</span>
          </div>
        </div>

        <!-- Panneau déroulant -->
        <div v-if="expanded === fighter.combattantID" class="training-panel">
          <p class="panel-hint">Choisir un type d'entraînement :</p>
          <div class="training-options">
            <button
              v-for="type in TYPES_ENTRAINEMENT"
              :key="type.key"
              class="training-option"
              :class="{
                selected: planifiePour(fighter.combattantID)?.typeEntrainement === type.key,
                disabled: capaciteUtilisee >= CAPACITY_MAX && !planifiePour(fighter.combattantID),
              }"
              :disabled="capaciteUtilisee >= CAPACITY_MAX && !planifiePour(fighter.combattantID)"
              @click="planifier(fighter.combattantID, type.key)"
            >
              <span class="option-icon">{{ type.icon }}</span>
              <span class="option-label">{{ type.label }}</span>
              <span class="option-desc">{{ type.desc }}</span>
            </button>
          </div>
        </div>
      </div>
    </div>

  </div>
</template>

<style scoped>
.training-tab {
  max-width: 860px;
  margin: 0 auto;
}

/* Header */
.training-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 24px;
  padding: 16px 20px;
  background: rgba(99,102,241,.08);
  border: 1px solid rgba(99,102,241,.2);
  border-radius: 16px;
}
.training-header-left {
  display: flex;
  flex-direction: column;
  gap: 2px;
}
.training-title {
  font-size: 1.15rem;
  font-weight: 700;
  color: #e2e8f0;
}
.training-date {
  font-size: .82rem;
  color: #94a3b8;
}

/* Staff capacity */
.staff-capacity {
  display: flex;
  align-items: center;
  gap: 10px;
}
.staff-label {
  font-size: .8rem;
  color: #94a3b8;
  text-transform: uppercase;
  letter-spacing: .05em;
}
.staff-slots {
  display: flex;
  gap: 6px;
}
.staff-slot {
  width: 28px;
  height: 12px;
  border-radius: 6px;
  background: rgba(255,255,255,.1);
  border: 1px solid rgba(255,255,255,.15);
  transition: background .25s;
}
.staff-slot.used {
  background: #6366f1;
  border-color: #6366f1;
}
.staff-count {
  font-size: .85rem;
  font-weight: 600;
  color: #a5b4fc;
  min-width: 28px;
}

/* Fighter list */
.fighter-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}
.fighter-row {
  border-radius: 14px;
  overflow: hidden;
  border: 1px solid rgba(255,255,255,.07);
  background: rgba(255,255,255,.03);
}

.fighter-row-main {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 14px 18px;
  cursor: pointer;
  transition: background .2s;
}
.fighter-row-main:hover,
.fighter-row-main.is-expanded {
  background: rgba(99,102,241,.1);
}

.fighter-row-info {
  display: flex;
  flex-direction: column;
  gap: 2px;
}
.fighter-name {
  font-size: .95rem;
  font-weight: 600;
  color: #e2e8f0;
}
.fighter-meta {
  font-size: .78rem;
  color: #64748b;
}

.fighter-row-right {
  display: flex;
  align-items: center;
  gap: 10px;
}
.training-badge {
  font-size: .78rem;
  padding: 4px 10px;
  border-radius: 20px;
  background: rgba(99,102,241,.2);
  color: #a5b4fc;
  font-weight: 600;
}
.cancel-btn {
  background: rgba(239,68,68,.15);
  border: none;
  color: #f87171;
  border-radius: 50%;
  width: 22px;
  height: 22px;
  font-size: .7rem;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: background .2s;
}
.cancel-btn:hover {
  background: rgba(239,68,68,.35);
}
.no-training {
  font-size: .78rem;
  color: #475569;
}
.no-training.capacity-full {
  color: #f97316;
}
.expand-icon {
  font-size: .65rem;
  color: #64748b;
}

/* Training panel */
.training-panel {
  padding: 16px 18px 18px;
  background: rgba(0,0,0,.15);
  border-top: 1px solid rgba(255,255,255,.05);
}
.panel-hint {
  font-size: .78rem;
  color: #64748b;
  margin-bottom: 12px;
}
.training-options {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(150px, 1fr));
  gap: 8px;
}
.training-option {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 4px;
  padding: 12px 8px;
  border-radius: 12px;
  border: 1px solid rgba(255,255,255,.08);
  background: rgba(255,255,255,.04);
  cursor: pointer;
  transition: all .2s;
  text-align: center;
}
.training-option:hover:not(:disabled) {
  background: rgba(99,102,241,.15);
  border-color: rgba(99,102,241,.4);
}
.training-option.selected {
  background: rgba(99,102,241,.25);
  border-color: #6366f1;
}
.training-option.disabled,
.training-option:disabled {
  opacity: .35;
  cursor: not-allowed;
}
.option-icon {
  font-size: 1.4rem;
}
.option-label {
  font-size: .82rem;
  font-weight: 600;
  color: #e2e8f0;
}
.option-desc {
  font-size: .68rem;
  color: #64748b;
  line-height: 1.3;
}

/* Empty state */
.empty-state {
  text-align: center;
  padding: 48px 24px;
  color: #475569;
}
.empty-state p {
  margin-top: 12px;
}
</style>
