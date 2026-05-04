<script setup>
import { ref, computed, onMounted } from 'vue'

const props = defineProps({
  authHeaders: { type: Function, required: true },
  partie:      { type: Object,   required: true },
})

const API = 'http://localhost:5219/api'

// ── State ─────────────────────────────────────────────────────
const ecurie    = ref([])
const planifies = ref([])
const staff     = ref([])   // CoachDto[] (joueur + staff embauché)
const expanded  = ref(null) // combattantID ouvert
const loading   = ref(true)

// ── Constantes ────────────────────────────────────────────────
const TYPES_ENTRAINEMENT = [
  { key: 'Striking',        label: 'Striking',        icon: '🥊', desc: 'Frappe · Puissance · Précision' },
  { key: 'Lutte',           label: 'Lutte',           icon: '🤼', desc: 'Wrestling · Takedown · Anti-TD' },
  { key: 'Grappling',       label: 'Grappling',       icon: '⛩️', desc: 'Jiu-Jitsu · Submission · Évasion' },
  { key: 'Conditionnement', label: 'Conditionnement', icon: '🏋️', desc: 'Force · Vitesse · Cardio' },
  { key: 'Mental',          label: 'Mental',          icon: '🧠', desc: 'Mental · Expérience · Adaptation' },
]

// Capacité dynamique : calculée depuis le nb de coaches chargés
const capaciteMax = computed(() => Math.max(2, staff.value.length * 2))

// ── Computed ──────────────────────────────────────────────────
const capaciteUtilisee = computed(() => planifies.value.length)

const dateJeu = computed(() => {
  const mois = [
    'Janvier','Février','Mars','Avril','Mai','Juin',
    'Juillet','Août','Septembre','Octobre','Novembre','Décembre',
  ]
  return `${mois[(props.partie.moisActuel ?? 1) - 1]} ${props.partie.anneeActuelle ?? 1985}`
})

// Coach sélectionné par combattant : Map<combattantID, {id, type}>
const coachSelections = ref({})

function coachLabel(coach) {
  return `${coach.icone ?? ''} ${coach.prenom} ${coach.nom}`.trim()
}

function selectedCoach(combattantID) {
  if (coachSelections.value[combattantID]) return coachSelections.value[combattantID]
  // Par défaut : premier de la liste
  const first = staff.value[0]
  if (!first) return null
  return { id: first.id, type: first.type }
}

function selectCoach(combattantID, coach) {
  coachSelections.value = {
    ...coachSelections.value,
    [combattantID]: { id: coach.id, type: coach.type }
  }
}

function isCoachSelected(combattantID, coach) {
  const sel = coachSelections.value[combattantID]
  if (sel) return sel.id === coach.id && sel.type === coach.type
  // Par défaut : le premier coach de la liste
  const first = staff.value[0]
  return first ? (first.id === coach.id && first.type === coach.type) : false
}

// ── Chargement ────────────────────────────────────────────────
async function charger() {
  loading.value = true
  try {
    const [ecurieRes, planifiesRes, staffRes] = await Promise.all([
      fetch(`${API}/combattants/ecurie`,           { headers: props.authHeaders() }),
      fetch(`${API}/entrainements/planifies`,       { headers: props.authHeaders() }),
      fetch(`${API}/entrainements/staff`,           { headers: props.authHeaders() }),
    ])
    if (ecurieRes.ok)    ecurie.value    = await ecurieRes.json()
    if (planifiesRes.ok) planifies.value = await planifiesRes.json()
    if (staffRes.ok)     staff.value     = await staffRes.json()
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
  const alreadyPlanifie = planifiePour(combattantID)
  if (!alreadyPlanifie && capaciteUtilisee.value >= capaciteMax.value) return

  const sel = selectedCoach(combattantID)
  const entraineurJoueurID = sel?.type === 'Joueur' ? sel.id : null
  const staffPartieID      = sel?.type === 'Staff'  ? sel.id : null

  const res = await fetch(`${API}/entrainements/planifier`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', ...props.authHeaders() },
    body: JSON.stringify({ combattantID, typeEntrainement, entraineurJoueurID, staffPartieID }),
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

    <!-- En-tête -->
    <div class="training-header">
      <div class="training-header-left">
        <span class="training-title">Entraînement</span>
        <span class="training-date">{{ dateJeu }} · Tour {{ partie.tourActuel }}</span>
      </div>
      <div class="staff-block">
        <!-- Info staff -->
        <div class="staff-info" title="Coaches actifs">
          <span class="staff-icon-label">👨‍🏫 Coaches</span>
          <span class="staff-name">
            {{ staff.length === 0 ? 'Aucun coach' : staff.map(s => s.prenom).join(', ') }}
          </span>
        </div>
        <!-- Jauge capacité -->
        <div class="staff-capacity">
          <div class="staff-slots">
            <div
              v-for="i in capaciteMax"
              :key="i"
              class="staff-slot"
              :class="{ used: i <= capaciteUtilisee }"
            />
          </div>
          <span class="staff-count">{{ capaciteUtilisee }}/{{ capaciteMax }}</span>
        </div>
      </div>
    </div>

    <div v-if="loading" class="text-center py-12">
      <v-progress-circular indeterminate color="indigo" size="36" />
    </div>

    <div v-else-if="ecurie.length === 0" class="empty-state">
      <span style="font-size:2.5rem">👊</span>
      <p>Aucun combattant dans ton écurie.</p>
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
              {{ fighter.categoriePoids }} · {{ fighter.stylePrincipal }} · Cote {{ fighter.noteGlobale }}
            </span>
          </div>

          <div class="fighter-row-right">
            <template v-if="planifiePour(fighter.combattantID)">
              <div class="training-planned">
                <span class="training-badge">
                  {{ typeLabel(planifiePour(fighter.combattantID).typeEntrainement).icon }}
                  {{ planifiePour(fighter.combattantID).typeEntrainement }}
                </span>
                <span class="coach-badge">
                  👨‍🏫 {{ planifiePour(fighter.combattantID).coachPrenom }}
                </span>
              </div>
              <button
                class="cancel-btn"
                @click.stop="annuler(planifiePour(fighter.combattantID))"
                title="Annuler"
              >✕</button>
            </template>
            <template v-else>
              <span class="no-training" :class="{ 'capacity-full': capaciteUtilisee >= capaciteMax }">
                {{ capaciteUtilisee >= capaciteMax ? 'Complet' : 'Repos' }}
              </span>
            </template>
            <span class="expand-icon">{{ expanded === fighter.combattantID ? '▲' : '▼' }}</span>
          </div>
        </div>

        <!-- Panneau déroulant -->
        <div v-if="expanded === fighter.combattantID" class="training-panel">

          <!-- Stats du combattant -->
          <div class="fighter-stats-overview">
            <span class="panel-section-label">Stats actuelles</span>
            <div class="stats-grid">
              <div class="stat-group">
                <span class="stat-group-label">🥊 Striking</span>
                <div class="stat-bar-row"><span class="stat-name">Frappe</span><div class="stat-bar"><div class="stat-fill strike" :style="{ width: fighter.compStriking + '%' }"></div></div><span class="stat-val">{{ fighter.compStriking }}</span></div>
              </div>
              <div class="stat-group">
                <span class="stat-group-label">🤼 Lutte</span>
                <div class="stat-bar-row"><span class="stat-name">Wrestling</span><div class="stat-bar"><div class="stat-fill lutte" :style="{ width: fighter.compLutte + '%' }"></div></div><span class="stat-val">{{ fighter.compLutte }}</span></div>
              </div>
              <div class="stat-group">
                <span class="stat-group-label">⛩️ Grappling</span>
                <div class="stat-bar-row"><span class="stat-name">Jiu-Jitsu</span><div class="stat-bar"><div class="stat-fill grappling" :style="{ width: fighter.compGrappling + '%' }"></div></div><span class="stat-val">{{ fighter.compGrappling }}</span></div>
              </div>
              <div class="stat-group">
                <span class="stat-group-label">🏋️ Condition</span>
                <div class="stat-bar-row"><span class="stat-name">Physique</span><div class="stat-bar"><div class="stat-fill conditioning" :style="{ width: fighter.compConditioning + '%' }"></div></div><span class="stat-val">{{ fighter.compConditioning }}</span></div>
              </div>
              <div class="stat-group">
                <span class="stat-group-label">🧠 Mental</span>
                <div class="stat-bar-row"><span class="stat-name">Mental</span><div class="stat-bar"><div class="stat-fill mental" :style="{ width: fighter.compMental + '%' }"></div></div><span class="stat-val">{{ fighter.compMental }}</span></div>
              </div>
              <div class="stat-group">
                <span class="stat-group-label">💪 Endurance</span>
                <div class="stat-bar-row"><span class="stat-name">Stamina</span><div class="stat-bar"><div class="stat-fill stamina" :style="{ width: fighter.compStamina + '%' }"></div></div><span class="stat-val">{{ fighter.compStamina }}</span></div>
              </div>
            </div>
          </div>

          <!-- Sélecteur de coach -->
          <div class="coach-selector">
            <span class="panel-section-label">Coach assigné</span>
            <div class="coach-options">
              <div
                v-for="coach in staff"
                :key="coach.type + '_' + coach.id"
                class="coach-option"
                :class="{ selected: isCoachSelected(fighter.combattantID, coach) }"
                @click="selectCoach(fighter.combattantID, coach)"
              >
                <span class="coach-check">{{ isCoachSelected(fighter.combattantID, coach) ? '✅' : '⬜' }}</span>
                <span>{{ coach.icone }}</span>
                <span>{{ coach.prenom }} {{ coach.nom }}</span>
                <span class="coach-type-tag" :class="coach.type === 'Joueur' ? 'tag-joueur' : 'tag-staff'">
                  {{ coach.type === 'Joueur' ? 'Toi' : 'Staff' }}
                </span>
              </div>
              <div v-if="!staff.length" class="coach-option disabled">Aucun coach</div>
            </div>
          </div>

          <!-- Sélecteur d'entraînement -->
          <div class="training-selector">
            <span class="panel-section-label">Type d'entraînement</span>
            <div class="training-options">
              <button
                v-for="type in TYPES_ENTRAINEMENT"
                :key="type.key"
                class="training-option"
                :class="{
                  selected: planifiePour(fighter.combattantID)?.typeEntrainement === type.key,
                  disabled: capaciteUtilisee >= capaciteMax && !planifiePour(fighter.combattantID),
                }"
                :disabled="capaciteUtilisee >= capaciteMax && !planifiePour(fighter.combattantID)"
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
  flex-wrap: wrap;
  gap: 12px;
  margin-bottom: 24px;
  padding: 16px 20px;
  background: rgba(220,38,38,.08);
  border: 1px solid rgba(220,38,38,.2);
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

/* Staff block */
.staff-block {
  display: flex;
  align-items: center;
  gap: 16px;
}
.staff-info {
  display: flex;
  flex-direction: column;
  gap: 2px;
}
.staff-icon-label {
  font-size: .72rem;
  color: #64748b;
  text-transform: uppercase;
  letter-spacing: .05em;
}
.staff-name {
  font-size: .85rem;
  font-weight: 600;
  color: #fca5a5;
}
.staff-capacity {
  display: flex;
  align-items: center;
  gap: 8px;
}
.staff-slots {
  display: flex;
  gap: 5px;
}
.staff-slot {
  width: 26px;
  height: 10px;
  border-radius: 5px;
  background: rgba(255,255,255,.1);
  border: 1px solid rgba(255,255,255,.12);
  transition: background .25s;
}
.staff-slot.used {
  background: #dc2626;
  border-color: #dc2626;
}
.staff-count {
  font-size: .82rem;
  font-weight: 600;
  color: #fca5a5;
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
  background: rgba(220,38,38,.1);
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
.training-planned {
  display: flex;
  flex-direction: column;
  align-items: flex-end;
  gap: 3px;
}
.training-badge {
  font-size: .78rem;
  padding: 3px 10px;
  border-radius: 20px;
  background: rgba(220,38,38,.2);
  color: #fca5a5;
  font-weight: 600;
}
.coach-badge {
  font-size: .72rem;
  color: #64748b;
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
.cancel-btn:hover { background: rgba(239,68,68,.35); }
.no-training {
  font-size: .78rem;
  color: #475569;
}
.no-training.capacity-full { color: #f97316; }
.expand-icon { font-size: .65rem; color: #64748b; }

/* Training panel */
.training-panel {
  padding: 16px 18px 18px;
  background: rgba(0,0,0,.15);
  border-top: 1px solid rgba(255,255,255,.05);
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.panel-section-label {
  display: block;
  font-size: .72rem;
  color: #64748b;
  text-transform: uppercase;
  letter-spacing: .06em;
  margin-bottom: 8px;
}

/* Coach selector */
.coach-options {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}
.coach-option {
  font-size: .82rem;
  padding: 6px 14px;
  border-radius: 20px;
  border: 1px solid rgba(220,38,38,.3);
  background: rgba(220,38,38,.12);
  color: #fca5a5;
  display: flex;
  align-items: center;
  gap: 8px;
  cursor: pointer;
  transition: all .2s;
}
.coach-option:hover {
  border-color: rgba(220,38,38,.6);
  background: rgba(220,38,38,.2);
}
.coach-option.selected {
  border: 2px solid #22c55e;
  background: rgba(34,197,94,.18);
  font-weight: 700;
  color: #4ade80;
  box-shadow: 0 0 10px rgba(34,197,94,.5);
}
.coach-check {
  font-size: .9rem;
  flex-shrink: 0;
}
.coach-option.disabled {
  opacity: .4;
  color: #64748b;
  border-color: rgba(255,255,255,.1);
  background: transparent;
}
.coach-only-tag {
  font-size: .68rem;
  color: #dc2626;
  opacity: .7;
}
.coach-type-tag {
  font-size: .65rem;
  padding: 1px 6px;
  border-radius: 8px;
  font-weight: 700;
}
.tag-joueur { background: rgba(220,38,38,.2); color: #fca5a5; }
.tag-staff  { background: rgba(34,197,94,.15);  color: #4ade80; }

/* Training options */
.training-options {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(148px, 1fr));
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
  background: rgba(220,38,38,.15);
  border-color: rgba(220,38,38,.4);
}
.training-option.selected {
  background: rgba(220,38,38,.25);
  border-color: #dc2626;
}
.training-option.disabled,
.training-option:disabled { opacity: .35; cursor: not-allowed; }
.option-icon { font-size: 1.4rem; }
.option-label { font-size: .82rem; font-weight: 600; color: #e2e8f0; }
.option-desc { font-size: .68rem; color: #64748b; line-height: 1.3; }

/* Fighter stats overview */
.fighter-stats-overview {
  padding-bottom: 4px;
}
.stats-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 10px;
}
@media (max-width: 600px) {
  .stats-grid { grid-template-columns: repeat(2, 1fr); }
}
.stat-group {
  display: flex;
  flex-direction: column;
  gap: 4px;
}
.stat-group-label {
  font-size: .7rem;
  font-weight: 700;
  color: #94a3b8;
  letter-spacing: .03em;
}
.stat-bar-row {
  display: flex;
  align-items: center;
  gap: 6px;
}
.stat-name {
  font-size: .68rem;
  color: #64748b;
  min-width: 48px;
}
.stat-bar {
  flex: 1;
  height: 6px;
  border-radius: 3px;
  background: rgba(255,255,255,.08);
  overflow: hidden;
}
.stat-fill {
  height: 100%;
  border-radius: 3px;
  transition: width .3s ease;
}
.stat-fill.strike       { background: #ef4444; }
.stat-fill.lutte        { background: #f97316; }
.stat-fill.grappling    { background: #dc2626; }
.stat-fill.conditioning { background: #22c55e; }
.stat-fill.mental       { background: #a855f7; }
.stat-fill.stamina      { background: #eab308; }
.stat-val {
  font-size: .72rem;
  font-weight: 700;
  color: #fca5a5;
  min-width: 20px;
  text-align: right;
}

/* Empty state */
.empty-state {
  text-align: center;
  padding: 48px 24px;
  color: #475569;
}
.empty-state p { margin-top: 12px; }
</style>

