<script setup>
import { ref, computed, onMounted } from 'vue'

const props = defineProps({
  authHeaders: { type: Function, required: true },
  partie:      { type: Object,   required: true },
})

const API = 'http://localhost:5219/api'

// ── State ─────────────────────────────────────────────────────
const ecurie        = ref([])
const combatsPlanifies = ref([])
const organisations = ref([])
const loading       = ref(true)

// Formulaire de planification
const expanded      = ref(null)     // combattantID ouvert
const adversaires   = ref([])
const loadingAdv    = ref(false)
const form = ref({ adversaireID: null, organisationID: null, delaiMois: 1, gameplan: 'Balanced' })
const saving = ref(false)

// ── Computed ──────────────────────────────────────────────────
const dateJeu = computed(() => {
  const MOIS = ['Janvier','Février','Mars','Avril','Mai','Juin',
                'Juillet','Août','Septembre','Octobre','Novembre','Décembre']
  return `${MOIS[(props.partie.moisActuel ?? 1) - 1]} ${props.partie.anneeActuelle ?? 1985}`
})

const PRESTIGE_STARS = (n) => '★'.repeat(n) + '☆'.repeat(5 - n)

function combatPourFighter(combattantID) {
  return combatsPlanifies.value.find(c => c.combattantID === combattantID) ?? null
}

function tourVersDate(tour) {
  let mois = (props.partie.moisActuel ?? 1) + (tour - props.partie.tourActuel)
  let annee = props.partie.anneeActuelle ?? 1985
  while (mois > 12) { mois -= 12; annee++ }
  const MOIS = ['Jan','Fév','Mar','Avr','Mai','Jun','Jul','Aoû','Sep','Oct','Nov','Déc']
  return `${MOIS[mois - 1]} ${annee}`
}

// ── Chargement ────────────────────────────────────────────────
async function charger() {
  loading.value = true
  try {
    const [ecurieRes, combatsRes, orgsRes] = await Promise.all([
      fetch(`${API}/combattants/ecurie`,                { headers: props.authHeaders() }),
      fetch(`${API}/combats-planifies`,                 { headers: props.authHeaders() }),
      fetch(`${API}/combats-planifies/organisations`,   { headers: props.authHeaders() }),
    ])
    if (ecurieRes.ok)  ecurie.value         = await ecurieRes.json()
    if (combatsRes.ok) combatsPlanifies.value = await combatsRes.json()
    if (orgsRes.ok)    organisations.value  = await orgsRes.json()
  } finally {
    loading.value = false
  }
}

async function ouvrirPlanification(combattantID) {
  if (expanded.value === combattantID) { expanded.value = null; return }
  expanded.value = combattantID
  form.value = { adversaireID: null, organisationID: null, delaiMois: 1, gameplan: 'Balanced' }
  adversaires.value = []
  loadingAdv.value = true
  try {
    const res = await fetch(`${API}/combats-planifies/adversaires/${combattantID}`, {
      headers: props.authHeaders()
    })
    if (res.ok) adversaires.value = await res.json()
  } finally {
    loadingAdv.value = false
  }
}

async function planifier(combattantID) {
  if (!form.value.adversaireID || !form.value.organisationID) return
  saving.value = true
  try {
    const tourPrevu = props.partie.tourActuel + form.value.delaiMois
    const res = await fetch(`${API}/combats-planifies`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', ...props.authHeaders() },
      body: JSON.stringify({
        combattantID,
        adversaireID:   form.value.adversaireID,
        organisationID: form.value.organisationID,
        tourPrevu,
        gameplan:       form.value.gameplan,
      }),
    })
    if (res.ok) {
      expanded.value = null
      await charger()
    }
  } finally {
    saving.value = false
  }
}

async function annuler(id) {
  await fetch(`${API}/combats-planifies/${id}`, {
    method: 'DELETE',
    headers: props.authHeaders(),
  })
  await charger()
}

function recordClass(fighter) {
  if (fighter.victoires > fighter.defaites) return 'record-positive'
  if (fighter.victoires < fighter.defaites) return 'record-negative'
  return 'record-neutral'
}

onMounted(charger)
</script>

<template>
  <div class="combat-tab">

    <!-- Header -->
    <div class="combat-header">
      <div>
        <span class="combat-title">Planifier un combat</span>
        <span class="combat-date">{{ dateJeu }} · Tour {{ partie.tourActuel }}</span>
      </div>
      <div class="combat-header-note">
        Choisis un combattant, un adversaire et une organisation.
      </div>
    </div>

    <div v-if="loading" class="text-center py-12">
      <v-progress-circular indeterminate color="indigo" size="36" />
    </div>

    <div v-else-if="ecurie.length === 0" class="empty-state">
      <span style="font-size:2.5rem">📋</span>
      <p>Aucun combattant dans ton écurie. Recrute des combattants d'abord.</p>
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
          @click="!combatPourFighter(fighter.combattantID) && ouvrirPlanification(fighter.combattantID)"
          :style="combatPourFighter(fighter.combattantID) ? 'cursor:default' : 'cursor:pointer'"
        >
          <div class="fighter-info">
            <div class="fighter-name-row">
              <span class="fighter-name">{{ fighter.prenom }} {{ fighter.nom }}</span>
              <span class="fighter-record" :class="recordClass(fighter)">
                {{ fighter.victoires }}-{{ fighter.defaites }}-{{ fighter.nuls }}
              </span>
            </div>
            <span class="fighter-meta">{{ fighter.categoriePoids }} · {{ fighter.stylePrincipal }} · Cote {{ fighter.noteGlobale }}</span>
          </div>

          <div class="fighter-right">
            <!-- Combat planifié -->
            <template v-if="combatPourFighter(fighter.combattantID)">
              <div class="combat-planifie-info">
                <span class="vs-label">VS</span>
                <span class="adversaire-name">
                  {{ combatPourFighter(fighter.combattantID).adversairePrenom }}
                  {{ combatPourFighter(fighter.combattantID).adversaireNom }}
                </span>
                <span class="org-name">{{ combatPourFighter(fighter.combattantID).organisation }}</span>
                <span class="tour-badge">
                  Tour {{ combatPourFighter(fighter.combattantID).tourPrevu }}
                  ({{ tourVersDate(combatPourFighter(fighter.combattantID).tourPrevu) }})
                </span>
                <span class="gameplan-badge" :class="`gp-${(combatPourFighter(fighter.combattantID).gameplan || 'Balanced').toLowerCase()}`">
                  {{ combatPourFighter(fighter.combattantID).gameplan === 'Striking' ? '🥊' : combatPourFighter(fighter.combattantID).gameplan === 'Grappling' ? '🤼' : '⚖️' }}
                  {{ combatPourFighter(fighter.combattantID).gameplan || 'Balanced' }}
                </span>
              </div>
              <button
                class="cancel-btn"
                @click.stop="annuler(combatPourFighter(fighter.combattantID).combatPlanifieID)"
                title="Annuler le combat"
              >✕</button>
            </template>

            <!-- Pas de combat -->
            <template v-else>
              <span class="no-combat">Aucun combat planifié</span>
              <span class="expand-icon">{{ expanded === fighter.combattantID ? '▲' : '▼' }}</span>
            </template>
          </div>
        </div>

        <!-- Panneau planification -->
        <div v-if="expanded === fighter.combattantID && !combatPourFighter(fighter.combattantID)" class="plan-panel">

          <div class="plan-grid">

            <!-- Organisation -->
            <div class="plan-section">
              <span class="plan-label">Organisation</span>
              <div v-if="organisations.length === 0" class="plan-empty">Aucune organisation disponible</div>
              <div class="org-list">
                <div
                  v-for="org in organisations"
                  :key="org.organisationID"
                  class="org-option"
                  :class="{ selected: form.organisationID === org.organisationID }"
                  @click="form.organisationID = org.organisationID"
                >
                  <div class="org-option-top">
                    <span class="org-nom">{{ org.nom }}</span>
                    <span class="org-prestige" :title="`Prestige ${org.prestige}/5`">
                      {{ PRESTIGE_STARS(org.prestige) }}
                    </span>
                  </div>
                  <span class="org-annee">{{ org.anneeCreation }}{{ org.estFictive ? ' · Circuit local' : '' }}</span>
                </div>
              </div>
            </div>

            <!-- Adversaire -->
            <div class="plan-section">
              <span class="plan-label">Adversaire</span>
              <div v-if="loadingAdv" class="text-center py-4">
                <v-progress-circular indeterminate color="indigo" size="20" />
              </div>
              <div v-else-if="adversaires.length === 0" class="plan-empty">
                Aucun adversaire disponible dans cette catégorie.
              </div>
              <div v-else class="adv-list">
                <div
                  v-for="adv in adversaires"
                  :key="adv.combattantID"
                  class="adv-option"
                  :class="{ selected: form.adversaireID === adv.combattantID }"
                  @click="form.adversaireID = adv.combattantID"
                >
                  <span class="adv-name">{{ adv.prenom }} {{ adv.nomFamille }}</span>
                  <div class="adv-meta">
                    <span>{{ adv.stylePrincipal }}</span>
                    <span class="adv-note">{{ adv.noteGlobale }}</span>
                  </div>
                </div>
              </div>
            </div>

          </div>

          <!-- Délai -->
          <div class="plan-delai">
            <span class="plan-label">Date prévue</span>
            <div class="delai-options">
              <button
                v-for="d in [1, 2, 3]"
                :key="d"
                class="delai-btn"
                :class="{ selected: form.delaiMois === d }"
                @click="form.delaiMois = d"
              >
                Dans {{ d }} mois
                <span class="delai-date">({{ tourVersDate(partie.tourActuel + d) }})</span>
              </button>
            </div>
          </div>

          <!-- Gameplan -->
          <div class="plan-gameplan">
            <span class="plan-label">Gameplan</span>
            <div class="gameplan-options">
              <button
                class="gameplan-btn gameplan-striking"
                :class="{ selected: form.gameplan === 'Striking' }"
                @click="form.gameplan = 'Striking'"
              >
                <span class="gameplan-icon">🥊</span>
                <span class="gameplan-name">Striking</span>
                <span class="gameplan-desc">Rester debout, éviter les takedowns</span>
              </button>
              <button
                class="gameplan-btn gameplan-balanced"
                :class="{ selected: form.gameplan === 'Balanced' }"
                @click="form.gameplan = 'Balanced'"
              >
                <span class="gameplan-icon">⚖️</span>
                <span class="gameplan-name">Équilibré</span>
                <span class="gameplan-desc">S'adapter selon les opportunités</span>
              </button>
              <button
                class="gameplan-btn gameplan-grappling"
                :class="{ selected: form.gameplan === 'Grappling' }"
                @click="form.gameplan = 'Grappling'"
              >
                <span class="gameplan-icon">🤼</span>
                <span class="gameplan-name">Grappling</span>
                <span class="gameplan-desc">Emmener au sol, chercher la soumission</span>
              </button>
            </div>
          </div>

          <!-- Action -->
          <div class="plan-actions">
            <button class="btn-annuler" @click="expanded = null">Annuler</button>
            <button
              class="btn-planifier"
              :disabled="!form.adversaireID || !form.organisationID || saving"
              @click="planifier(fighter.combattantID)"
            >
              {{ saving ? 'En cours…' : '📋 Planifier le combat' }}
            </button>
          </div>

        </div>
      </div>
    </div>

  </div>
</template>

<style scoped>
.combat-tab { max-width: 900px; margin: 0 auto; }

/* Header */
.combat-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 8px;
  margin-bottom: 24px;
  padding: 16px 20px;
  background: rgba(99,102,241,.08);
  border: 1px solid rgba(99,102,241,.2);
  border-radius: 16px;
}
.combat-title {
  display: block;
  font-size: 1.15rem;
  font-weight: 700;
  color: #e2e8f0;
}
.combat-date {
  display: block;
  font-size: .82rem;
  color: #94a3b8;
  margin-top: 2px;
}
.combat-header-note {
  font-size: .8rem;
  color: #64748b;
  align-self: center;
}

/* Fighter list */
.fighter-list { display: flex; flex-direction: column; gap: 8px; }
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
  transition: background .2s;
}
.fighter-row-main:hover { background: rgba(99,102,241,.08); }
.fighter-row-main.is-expanded { background: rgba(99,102,241,.1); }
.fighter-info { display: flex; flex-direction: column; gap: 2px; }
.fighter-name-row { display: flex; align-items: center; gap: 8px; }
.fighter-name { font-size: .95rem; font-weight: 600; color: #e2e8f0; }
.fighter-record {
  font-size: .72rem;
  font-weight: 700;
  padding: 1px 7px;
  border-radius: 10px;
  letter-spacing: .03em;
}
.record-positive { background: rgba(34,197,94,.15); color: #22c55e; }
.record-negative { background: rgba(239,68,68,.15);  color: #ef4444; }
.record-neutral  { background: rgba(148,163,184,.1); color: #94a3b8; }
.fighter-meta { font-size: .78rem; color: #64748b; }
.fighter-right { display: flex; align-items: center; gap: 10px; }

/* Combat planifié */
.combat-planifie-info {
  display: flex;
  flex-direction: column;
  align-items: flex-end;
  gap: 3px;
}
.vs-label {
  font-size: .7rem;
  font-weight: 800;
  color: #f97316;
  letter-spacing: .1em;
}
.adversaire-name {
  font-size: .88rem;
  font-weight: 700;
  color: #e2e8f0;
}
.org-name {
  font-size: .75rem;
  color: #6366f1;
}
.tour-badge {
  font-size: .72rem;
  color: #64748b;
}
.no-combat { font-size: .78rem; color: #475569; }
.expand-icon { font-size: .65rem; color: #64748b; }
.cancel-btn {
  background: rgba(239,68,68,.15);
  border: none;
  color: #f87171;
  border-radius: 50%;
  width: 24px;
  height: 24px;
  font-size: .7rem;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: background .2s;
}
.cancel-btn:hover { background: rgba(239,68,68,.35); }

/* Plan panel */
.plan-panel {
  padding: 16px 18px 20px;
  background: rgba(0,0,0,.15);
  border-top: 1px solid rgba(255,255,255,.05);
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.plan-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 16px;
}
@media (max-width: 600px) {
  .plan-grid { grid-template-columns: 1fr; }
}
.plan-section { display: flex; flex-direction: column; gap: 8px; }
.plan-label {
  font-size: .72rem;
  color: #64748b;
  text-transform: uppercase;
  letter-spacing: .06em;
}
.plan-empty { font-size: .82rem; color: #475569; padding: 8px 0; }

/* Organisation */
.org-list { display: flex; flex-direction: column; gap: 6px; max-height: 240px; overflow-y: auto; }
.org-option {
  padding: 8px 12px;
  border-radius: 10px;
  border: 1px solid rgba(255,255,255,.07);
  background: rgba(255,255,255,.03);
  cursor: pointer;
  transition: all .18s;
}
.org-option:hover { background: rgba(99,102,241,.1); border-color: rgba(99,102,241,.3); }
.org-option.selected { background: rgba(99,102,241,.2); border-color: #6366f1; }
.org-option-top { display: flex; justify-content: space-between; align-items: center; }
.org-nom { font-size: .85rem; font-weight: 600; color: #e2e8f0; }
.org-prestige { font-size: .75rem; color: #f59e0b; letter-spacing: .05em; }
.org-annee { font-size: .72rem; color: #64748b; margin-top: 2px; display: block; }

/* Adversaire */
.adv-list { display: flex; flex-direction: column; gap: 5px; max-height: 240px; overflow-y: auto; }
.adv-option {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 8px 12px;
  border-radius: 10px;
  border: 1px solid rgba(255,255,255,.07);
  background: rgba(255,255,255,.03);
  cursor: pointer;
  transition: all .18s;
}
.adv-option:hover { background: rgba(99,102,241,.1); border-color: rgba(99,102,241,.3); }
.adv-option.selected { background: rgba(99,102,241,.2); border-color: #6366f1; }
.adv-name { font-size: .85rem; font-weight: 600; color: #e2e8f0; }
.adv-meta { display: flex; align-items: center; gap: 8px; font-size: .75rem; color: #64748b; }
.adv-note { font-weight: 700; color: #a5b4fc; }

/* Délai */
.plan-delai { display: flex; flex-direction: column; gap: 8px; }
.delai-options { display: flex; gap: 8px; flex-wrap: wrap; }
.delai-btn {
  padding: 8px 16px;
  border-radius: 20px;
  border: 1px solid rgba(255,255,255,.1);
  background: rgba(255,255,255,.04);
  color: #94a3b8;
  font-size: .82rem;
  cursor: pointer;
  transition: all .18s;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 2px;
}
.delai-btn:hover { background: rgba(99,102,241,.12); border-color: rgba(99,102,241,.3); }
.delai-btn.selected { background: rgba(99,102,241,.22); border-color: #6366f1; color: #a5b4fc; }
.delai-date { font-size: .7rem; color: #6366f1; }

/* Gameplan selector */
.plan-gameplan { display: flex; flex-direction: column; gap: 8px; }
.gameplan-options { display: flex; gap: 8px; flex-wrap: wrap; }
.gameplan-btn {
  flex: 1;
  min-width: 120px;
  padding: 10px 12px;
  border-radius: 12px;
  border: 1px solid rgba(255,255,255,.1);
  background: rgba(255,255,255,.04);
  cursor: pointer;
  transition: all .18s;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 3px;
  text-align: center;
}
.gameplan-icon  { font-size: 1.2rem; }
.gameplan-name  { font-size: .82rem; font-weight: 700; color: #e2e8f0; }
.gameplan-desc  { font-size: .68rem; color: #64748b; }
.gameplan-striking:hover,  .gameplan-striking.selected  { background: rgba(239,68,68,.12);  border-color: rgba(239,68,68,.4);  }
.gameplan-balanced:hover,  .gameplan-balanced.selected  { background: rgba(99,102,241,.12); border-color: rgba(99,102,241,.4); }
.gameplan-grappling:hover, .gameplan-grappling.selected { background: rgba(34,197,94,.12);  border-color: rgba(34,197,94,.4);  }
.gameplan-striking.selected  .gameplan-name { color: #f87171; }
.gameplan-balanced.selected  .gameplan-name { color: #a5b4fc; }
.gameplan-grappling.selected .gameplan-name { color: #4ade80; }

/* Gameplan badge on planned fight */
.gameplan-badge {
  font-size: .68rem;
  font-weight: 600;
  padding: 1px 7px;
  border-radius: 8px;
  margin-top: 1px;
}
.gp-striking  { background: rgba(239,68,68,.15);  color: #f87171; }
.gp-balanced  { background: rgba(99,102,241,.15); color: #a5b4fc; }
.gp-grappling { background: rgba(34,197,94,.15);  color: #4ade80; }

/* Actions */
.plan-actions {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
  padding-top: 4px;
}
.btn-annuler {
  padding: 8px 18px;
  border-radius: 20px;
  border: 1px solid rgba(255,255,255,.1);
  background: transparent;
  color: #64748b;
  font-size: .85rem;
  cursor: pointer;
  transition: all .18s;
}
.btn-annuler:hover { background: rgba(255,255,255,.05); }
.btn-planifier {
  padding: 9px 22px;
  border-radius: 20px;
  border: none;
  background: linear-gradient(135deg, #6366f1, #818cf8);
  color: white;
  font-size: .88rem;
  font-weight: 700;
  cursor: pointer;
  transition: all .18s;
}
.btn-planifier:hover:not(:disabled) { transform: translateY(-1px); box-shadow: 0 4px 16px rgba(99,102,241,.4); }
.btn-planifier:disabled { opacity: .45; cursor: not-allowed; }

/* Empty state */
.empty-state { text-align: center; padding: 48px 24px; color: #475569; }
.empty-state p { margin-top: 12px; }
</style>
