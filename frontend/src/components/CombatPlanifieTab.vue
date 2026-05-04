<script setup>
import { ref, computed, watch, onMounted } from 'vue'

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
const form = ref({ adversaireID: null, organisationID: null, delaiMois: 1,
  gameplan: { approche: 'Balanced', distance: 'Moyenne', cibles: 'Mixte', rythme: 'Normal' } })

const approches = [
  { value: 'Striking',  icon: '🥊', label: 'Striking',  desc: 'Rester debout, frapper à distance' },
  { value: 'Balanced',  icon: '⚖️', label: 'Équilibré', desc: "S'adapter aux opportunités" },
  { value: 'Grappling', icon: '🤼', label: 'Grappling', desc: 'Emmener au sol, soumettre' },
  { value: 'Clinch',    icon: '🤜', label: 'Clinch',    desc: 'Corps-à-corps, genoux, coudes' },
]
const distances = [
  { value: 'Exterieur', icon: '📏', label: 'Extérieure', desc: "Utiliser l'allonge et les kicks" },
  { value: 'Moyenne',   icon: '⚡', label: 'Moyenne',    desc: 'Distance standard' },
  { value: 'Interieur', icon: '💥', label: 'Intérieure', desc: 'Combos, uppercuts, crochets' },
]
const cibles_opts = [
  { value: 'Tete',   icon: '🎯', label: 'Tête',   desc: 'Chercher le KO' },
  { value: 'Corps',  icon: '🫁', label: 'Corps',  desc: "Épuiser l'adversaire" },
  { value: 'Jambes', icon: '🦵', label: 'Jambes', desc: 'Réduire la mobilité' },
  { value: 'Mixte',  icon: '🔀', label: 'Mixte',  desc: 'Varier les cibles' },
]
const rythmes = [
  { value: 'Agressif', icon: '🔥', label: 'Agressif', desc: 'Attaque dès le 1er round' },
  { value: 'Normal',   icon: '🏃', label: 'Normal',   desc: 'Rythme régulier' },
  { value: 'Patient',  icon: '🧠', label: 'Patient',  desc: 'Monter en puissance' },
]
const saving = ref(false)

// ── Computed ──────────────────────────────────────────────────
const dateJeu = computed(() => {
  const MOIS = ['Janvier','Février','Mars','Avril','Mai','Juin',
                'Juillet','Août','Septembre','Octobre','Novembre','Décembre']
  return `${MOIS[(props.partie.moisActuel ?? 1) - 1]} ${props.partie.anneeActuelle ?? 1985}`
})

const selectedAdv = computed(() =>
  adversaires.value.find(a => a.combattantID === form.value.adversaireID) ?? null
)

const selectedOrg = computed(() =>
  organisations.value.find(o => o.organisationID === form.value.organisationID) ?? null
)

const contratPrevu = computed(() => {
  const org = selectedOrg.value
  if (!org) return null
  const epoque = props.partie.epoque ?? 'NoRules'
  let nbCombats = 1
  let exclusif = false
  if (epoque !== 'NoRules') {
    if (org.prestige >= 4) {
      nbCombats = 3; exclusif = true
    } else if (org.prestige >= 3) {
      nbCombats = 2; exclusif = epoque === 'Modern'
    }
  }
  return { nbCombats, exclusif }
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

function formatMoney(n) {
  return n?.toLocaleString('fr-FR') ?? '0'
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

async function chargerAdversaires(combattantID) {
  loadingAdv.value = true
  adversaires.value = []
  form.value.adversaireID = null
  try {
    const orgParam = form.value.organisationID ? `?organisationID=${form.value.organisationID}` : ''
    const res = await fetch(`${API}/combats-planifies/adversaires/${combattantID}${orgParam}`, {
      headers: props.authHeaders()
    })
    if (res.ok) adversaires.value = await res.json()
  } finally {
    loadingAdv.value = false
  }
}

function parseGameplan(gp) {
  if (!gp) return { approche: 'Balanced', distance: 'Moyenne', cibles: 'Mixte', rythme: 'Normal' }
  if (typeof gp === 'object') return gp
  try { return JSON.parse(gp) } catch { return { approche: gp, distance: 'Moyenne', cibles: 'Mixte', rythme: 'Normal' } }
}

function gameplanLabel(gameplanStr) {
  const gp = parseGameplan(gameplanStr)
  const approche = gp.approche ?? 'Balanced'
  const icons = { Striking: '🥊', Grappling: '🤼', Clinch: '🤜', Balanced: '⚖️' }
  return { approche, icon: icons[approche] ?? '⚖️' }
}

async function ouvrirPlanification(combattantID) {
  if (expanded.value === combattantID) { expanded.value = null; return }
  expanded.value = combattantID
  form.value = { adversaireID: null, organisationID: null, delaiMois: 1,
    gameplan: { approche: 'Balanced', distance: 'Moyenne', cibles: 'Mixte', rythme: 'Normal' } }
  adversaires.value = []
  await chargerAdversaires(combattantID)
}

// Recharger les adversaires quand l'organisation change
watch(() => form.value.organisationID, (newVal, oldVal) => {
  if (newVal !== oldVal && expanded.value) {
    chargerAdversaires(expanded.value)
  }
})

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
        gameplan:       JSON.stringify(form.value.gameplan),
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
        Choisis une organisation, puis un adversaire adapté à son niveau.
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
          @click="!combatPourFighter(fighter.combattantID) && !fighter.semainesIndispo && ouvrirPlanification(fighter.combattantID)"
          :style="combatPourFighter(fighter.combattantID) || fighter.semainesIndispo ? 'cursor:default' : 'cursor:pointer'"
        >
          <div class="fighter-info">
            <div class="fighter-name-row">
              <span class="fighter-name">{{ fighter.prenom }} {{ fighter.nom }}</span>
              <span class="fighter-record" :class="recordClass(fighter)">
                {{ fighter.victoires }}-{{ fighter.defaites }}-{{ fighter.nuls }}
              </span>
              <span v-if="fighter.semainesIndispo > 0" class="injury-badge">
                🏥 {{ fighter.blessureZone }} ({{ fighter.semainesIndispo }}t)
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
                <span class="gameplan-badge"
                  :class="`gp-${gameplanLabel(combatPourFighter(fighter.combattantID).gameplan).approche.toLowerCase()}`">
                  {{ gameplanLabel(combatPourFighter(fighter.combattantID).gameplan).icon }}
                  {{ gameplanLabel(combatPourFighter(fighter.combattantID).gameplan).approche }}
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
                  <div class="org-bourse">
                    💰 Victoire : {{ formatMoney(org.bourseVictoireMin) }}–{{ formatMoney(org.bourseVictoireMax) }} €
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
                {{ form.organisationID ? 'Aucun adversaire à ce niveau.' : 'Sélectionne une organisation.' }}
              </div>
              <div v-else class="adv-list">
                <div
                  v-for="adv in adversaires"
                  :key="adv.combattantID"
                  class="adv-option"
                  :class="{ selected: form.adversaireID === adv.combattantID }"
                  @click="form.adversaireID = adv.combattantID"
                >
                  <div class="adv-top">
                    <span class="adv-name">{{ adv.prenom }} {{ adv.nomFamille }}</span>
                    <span class="adv-note">{{ adv.noteGlobale }}</span>
                  </div>
                  <div class="adv-meta">
                    <span>{{ adv.stylePrincipal }}</span>
                    <span v-if="adv.tailleCm" class="adversaire-physique">
                      📏 {{ adv.tailleCm }}cm · 💪 {{ adv.allongeCm }}cm · ⚖️ {{ adv.poidsReelKg }}kg
                    </span>
                    <span class="adv-record" :class="{
                      'record-positive': adv.victoires > adv.defaites,
                      'record-negative': adv.victoires < adv.defaites,
                      'record-neutral': adv.victoires === adv.defaites
                    }">{{ adv.victoires }}-{{ adv.defaites }}-{{ adv.nuls }}</span>
                  </div>
                </div>
              </div>
            </div>

          </div>

          <!-- Stats de l'adversaire sélectionné -->
          <div v-if="selectedAdv" class="adv-stats-panel">
            <span class="plan-label">📊 Stats de {{ selectedAdv.prenom }} {{ selectedAdv.nomFamille }}</span>
            <div class="adv-stats-grid">
              <div class="adv-stat-item">
                <span class="adv-stat-label">🥊 Striking</span>
                <div class="stat-bar"><div class="stat-fill strike" :style="{ width: selectedAdv.compStriking + '%' }"></div></div>
                <span class="adv-stat-val">{{ selectedAdv.compStriking }}</span>
              </div>
              <div class="adv-stat-item">
                <span class="adv-stat-label">🤼 Lutte</span>
                <div class="stat-bar"><div class="stat-fill lutte" :style="{ width: selectedAdv.compLutte + '%' }"></div></div>
                <span class="adv-stat-val">{{ selectedAdv.compLutte }}</span>
              </div>
              <div class="adv-stat-item">
                <span class="adv-stat-label">⛩️ Grappling</span>
                <div class="stat-bar"><div class="stat-fill grappling" :style="{ width: selectedAdv.compGrappling + '%' }"></div></div>
                <span class="adv-stat-val">{{ selectedAdv.compGrappling }}</span>
              </div>
              <div class="adv-stat-item">
                <span class="adv-stat-label">🏋️ Condition</span>
                <div class="stat-bar"><div class="stat-fill conditioning" :style="{ width: selectedAdv.compConditioning + '%' }"></div></div>
                <span class="adv-stat-val">{{ selectedAdv.compConditioning }}</span>
              </div>
              <div class="adv-stat-item">
                <span class="adv-stat-label">💪 Endurance</span>
                <div class="stat-bar"><div class="stat-fill stamina" :style="{ width: selectedAdv.compStamina + '%' }"></div></div>
                <span class="adv-stat-val">{{ selectedAdv.compStamina }}</span>
              </div>
              <div class="adv-stat-item">
                <span class="adv-stat-label">🧠 Mental</span>
                <div class="stat-bar"><div class="stat-fill mental" :style="{ width: selectedAdv.compMental + '%' }"></div></div>
                <span class="adv-stat-val">{{ selectedAdv.compMental }}</span>
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

          <!-- Gameplan multi-axes -->
          <div class="plan-gameplan">
            <span class="plan-label">Gameplan</span>
            <div class="gameplan-grid">

              <div class="gameplan-section">
                <span class="gameplan-section-label">Approche</span>
                <div class="gameplan-options">
                  <button
                    v-for="opt in approches" :key="opt.value"
                    class="gameplan-btn"
                    :class="{ selected: form.gameplan.approche === opt.value }"
                    @click="form.gameplan.approche = opt.value"
                  >
                    <span class="gameplan-icon">{{ opt.icon }}</span>
                    <span class="gameplan-name">{{ opt.label }}</span>
                    <span class="gameplan-desc">{{ opt.desc }}</span>
                  </button>
                </div>
              </div>

              <div class="gameplan-section">
                <span class="gameplan-section-label">Distance</span>
                <div class="gameplan-options">
                  <button
                    v-for="opt in distances" :key="opt.value"
                    class="gameplan-btn"
                    :class="{ selected: form.gameplan.distance === opt.value }"
                    @click="form.gameplan.distance = opt.value"
                  >
                    <span class="gameplan-icon">{{ opt.icon }}</span>
                    <span class="gameplan-name">{{ opt.label }}</span>
                    <span class="gameplan-desc">{{ opt.desc }}</span>
                  </button>
                </div>
              </div>

              <div class="gameplan-section">
                <span class="gameplan-section-label">Cibles</span>
                <div class="gameplan-options">
                  <button
                    v-for="opt in cibles_opts" :key="opt.value"
                    class="gameplan-btn"
                    :class="{ selected: form.gameplan.cibles === opt.value }"
                    @click="form.gameplan.cibles = opt.value"
                  >
                    <span class="gameplan-icon">{{ opt.icon }}</span>
                    <span class="gameplan-name">{{ opt.label }}</span>
                    <span class="gameplan-desc">{{ opt.desc }}</span>
                  </button>
                </div>
              </div>

              <div class="gameplan-section">
                <span class="gameplan-section-label">Rythme</span>
                <div class="gameplan-options">
                  <button
                    v-for="opt in rythmes" :key="opt.value"
                    class="gameplan-btn"
                    :class="{ selected: form.gameplan.rythme === opt.value }"
                    @click="form.gameplan.rythme = opt.value"
                  >
                    <span class="gameplan-icon">{{ opt.icon }}</span>
                    <span class="gameplan-name">{{ opt.label }}</span>
                    <span class="gameplan-desc">{{ opt.desc }}</span>
                  </button>
                </div>
              </div>

            </div>
          </div>

          <!-- Résumé bourse -->
          <div v-if="selectedOrg" class="bourse-summary">
            <span class="plan-label">💰 Contrat</span>
            <div class="bourse-info">
              <span class="bourse-item win">Victoire : {{ formatMoney(selectedOrg.bourseVictoireMin) }}–{{ formatMoney(selectedOrg.bourseVictoireMax) }} €</span>
              <span class="bourse-item lose">Défaite : {{ formatMoney(selectedOrg.bourseDefaiteMin) }}–{{ formatMoney(selectedOrg.bourseDefaiteMax) }} €</span>
            </div>
            <div v-if="contratPrevu" class="contrat-terms">
              <span class="contrat-duree">
                📄 {{ contratPrevu.nbCombats }} combat{{ contratPrevu.nbCombats > 1 ? 's' : '' }}
              </span>
              <span class="contrat-exclusif" :class="contratPrevu.exclusif ? 'excl-oui' : 'excl-non'">
                {{ contratPrevu.exclusif ? '🔒 Exclusif' : '🔓 Non exclusif' }}
              </span>
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

.injury-badge {
  font-size: .68rem;
  color: #ef4444;
  background: rgba(239,68,68,.1);
  padding: 1px 7px;
  border-radius: 8px;
  margin-left: 6px;
}

/* Header */
.combat-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 8px;
  margin-bottom: 24px;
  padding: 16px 20px;
  background: rgba(220,38,38,.08);
  border: 1px solid rgba(220,38,38,.2);
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
.fighter-row-main:hover { background: rgba(220,38,38,.08); }
.fighter-row-main.is-expanded { background: rgba(220,38,38,.1); }
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
  color: #dc2626;
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
.org-option:hover { background: rgba(220,38,38,.1); border-color: rgba(220,38,38,.3); }
.org-option.selected { background: rgba(220,38,38,.2); border-color: #dc2626; }
.org-option-top { display: flex; justify-content: space-between; align-items: center; }
.org-nom { font-size: .85rem; font-weight: 600; color: #e2e8f0; }
.org-prestige { font-size: .75rem; color: #f59e0b; letter-spacing: .05em; }
.org-bourse { font-size: .7rem; color: #22c55e; margin-top: 2px; }
.org-annee { font-size: .72rem; color: #64748b; margin-top: 2px; display: block; }

/* Adversaire */
.adv-list { display: flex; flex-direction: column; gap: 5px; max-height: 240px; overflow-y: auto; }
.adv-option {
  padding: 8px 12px;
  border-radius: 10px;
  border: 1px solid rgba(255,255,255,.07);
  background: rgba(255,255,255,.03);
  cursor: pointer;
  transition: all .18s;
}
.adv-option:hover { background: rgba(220,38,38,.1); border-color: rgba(220,38,38,.3); }
.adv-option.selected { background: rgba(220,38,38,.2); border-color: #dc2626; }
.adv-top { display: flex; align-items: center; justify-content: space-between; }
.adv-name { font-size: .85rem; font-weight: 600; color: #e2e8f0; }
.adv-meta { display: flex; align-items: center; justify-content: space-between; font-size: .75rem; color: #64748b; margin-top: 2px; }
.adv-note { font-weight: 700; color: #fca5a5; font-size: .85rem; }
.adversaire-physique { font-size: .68rem; color: #64748b; }
.adv-record {
  font-size: .7rem;
  font-weight: 700;
  padding: 1px 6px;
  border-radius: 8px;
}

/* Adversaire stats panel */
.adv-stats-panel {
  padding: 12px 14px;
  background: rgba(220,38,38,.06);
  border: 1px solid rgba(220,38,38,.15);
  border-radius: 12px;
}
.adv-stats-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 8px;
  margin-top: 8px;
}
@media (max-width: 600px) {
  .adv-stats-grid { grid-template-columns: repeat(2, 1fr); }
}
.adv-stat-item {
  display: flex;
  align-items: center;
  gap: 6px;
}
.adv-stat-label {
  font-size: .68rem;
  color: #94a3b8;
  min-width: 72px;
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
.stat-fill.stamina      { background: #eab308; }
.stat-fill.mental       { background: #a855f7; }
.adv-stat-val {
  font-size: .72rem;
  font-weight: 700;
  color: #fca5a5;
  min-width: 20px;
  text-align: right;
}

/* Bourse summary */
.bourse-summary {
  padding: 10px 14px;
  background: rgba(34,197,94,.06);
  border: 1px solid rgba(34,197,94,.15);
  border-radius: 12px;
}
.bourse-info {
  display: flex;
  gap: 20px;
  margin-top: 6px;
  flex-wrap: wrap;
}
.bourse-item {
  font-size: .82rem;
  font-weight: 600;
}
.bourse-item.win  { color: #22c55e; }
.bourse-item.lose { color: #f97316; }
.contrat-terms {
  display: flex;
  gap: 12px;
  margin-top: 6px;
  flex-wrap: wrap;
}
.contrat-duree {
  font-size: .78rem;
  color: #94a3b8;
}
.contrat-exclusif {
  font-size: .78rem;
  font-weight: 600;
  padding: 1px 8px;
  border-radius: 8px;
}
.excl-oui { background: rgba(239,68,68,.15); color: #f87171; }
.excl-non { background: rgba(34,197,94,.12); color: #4ade80; }

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
.delai-btn:hover { background: rgba(220,38,38,.12); border-color: rgba(220,38,38,.3); }
.delai-btn.selected { background: rgba(220,38,38,.22); border-color: #dc2626; color: #fca5a5; }
.delai-date { font-size: .7rem; color: #dc2626; }

/* Gameplan selector */
.plan-gameplan { display: flex; flex-direction: column; gap: 8px; }
.gameplan-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 12px;
}
@media (max-width: 600px) { .gameplan-grid { grid-template-columns: 1fr; } }
.gameplan-section { display: flex; flex-direction: column; gap: 6px; }
.gameplan-section-label {
  font-size: .68rem;
  color: #94a3b8;
  text-transform: uppercase;
  letter-spacing: .06em;
}
.gameplan-options { display: flex; gap: 6px; flex-wrap: wrap; }
.gameplan-btn {
  flex: 1;
  min-width: 72px;
  padding: 8px 8px;
  border-radius: 10px;
  border: 1px solid rgba(255,255,255,.1);
  background: rgba(255,255,255,.04);
  cursor: pointer;
  transition: all .18s;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 2px;
  text-align: center;
}
.gameplan-btn:hover   { background: rgba(220,38,38,.10); border-color: rgba(220,38,38,.3); }
.gameplan-btn.selected { background: rgba(220,38,38,.22); border-color: #dc2626; }
.gameplan-icon  { font-size: 1.1rem; }
.gameplan-name  { font-size: .78rem; font-weight: 700; color: #e2e8f0; }
.gameplan-desc  { font-size: .62rem; color: #64748b; }
.gameplan-btn.selected .gameplan-name { color: #fca5a5; }

/* Gameplan badge on planned fight */
.gameplan-badge {
  font-size: .68rem;
  font-weight: 600;
  padding: 1px 7px;
  border-radius: 8px;
  margin-top: 1px;
}
.gp-striking  { background: rgba(239,68,68,.15);  color: #f87171; }
.gp-balanced  { background: rgba(220,38,38,.15); color: #fca5a5; }
.gp-grappling { background: rgba(34,197,94,.15);  color: #4ade80; }
.gp-clinch    { background: rgba(249,115,22,.15); color: #fb923c; }

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
  background: linear-gradient(135deg, #dc2626, #ef4444);
  color: white;
  font-size: .88rem;
  font-weight: 700;
  cursor: pointer;
  transition: all .18s;
}
.btn-planifier:hover:not(:disabled) { transform: translateY(-1px); box-shadow: 0 4px 16px rgba(220,38,38,.4); }
.btn-planifier:disabled { opacity: .45; cursor: not-allowed; }

/* Empty state */
.empty-state { text-align: center; padding: 48px 24px; color: #475569; }
.empty-state p { margin-top: 12px; }
</style>

