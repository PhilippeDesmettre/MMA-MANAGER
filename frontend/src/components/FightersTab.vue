<script setup>
import { ref, computed, onMounted } from 'vue'

const props = defineProps({
  authHeaders: { type: Function, required: true }
})
const emit = defineEmits(['ecurie-updated'])

const API = 'http://localhost:5219/api'

// ── State ────────────────────────────────────────────────────────
const subTab       = ref('ecurie')
const ecurie       = ref([])
const disponibles  = ref([])
const loading      = ref(false)
const recruiting   = ref(null)   // ID du combattant en cours de recrutement

// Dialog
const dialog        = ref(false)
const dialogFighter = ref(null)
const dialogMode    = ref('view') // 'view' | 'recruit'

// Snackbar erreur
const snackbar      = ref(false)
const snackbarMsg   = ref('')

// Filtre catégorie dans recrutement
const filterCat    = ref('Toutes')

// Catégories construites dynamiquement depuis les données
const CATEGORIES = computed(() => {
  const cats = [...new Set(disponibles.value.map(f => f.categoriePoids))].sort()
  return ['Toutes', ...cats]
})

const STYLE_ICONS = {
  'Kickboxeur':   '🦶',
  'Boxeur':       '🥊',
  'Lutteur':      '💪',
  'Judoka':       '🥋',
  'BJJ':          '⛩️',
  'Muay Thai':    '🐉',
  'Samboïste':    '🤼',
  'MMA Complet':  '⚡',
}

const CAT_COLORS = {
  'Poids paille':    '#22d3ee',
  'Poids mouche':    '#34d399',
  'Poids coq':       '#38bdf8',
  'Poids plume':     '#818cf8',
  'Poids léger':     '#a78bfa',
  'Poids mi-moyen':  '#c084fc',
  'Poids moyen':     '#6366f1',
  'Poids mi-lourd':  '#f97316',
  'Lourd':           '#ef4444',
  'Super-lourd':     '#dc2626',
  // Femmes (suffixe " F")
  'Poids paille F':    '#22d3ee',
  'Poids mouche F':    '#34d399',
  'Poids coq F':       '#38bdf8',
  'Poids plume F':     '#818cf8',
  'Poids léger F':     '#a78bfa',
  'Poids mi-moyen F':  '#c084fc',
}

// ── Computed ─────────────────────────────────────────────────────
const filteredDisponibles = computed(() =>
  filterCat.value === 'Toutes'
    ? disponibles.value
    : disponibles.value.filter(f => f.categoriePoids === filterCat.value)
)

// ── Methods ──────────────────────────────────────────────────────
async function loadEcurie() {
  loading.value = true
  try {
    const res = await fetch(`${API}/combattants/ecurie`, { headers: props.authHeaders() })
    if (res.ok) {
      ecurie.value = await res.json()
      emit('ecurie-updated', ecurie.value.length)
    }
  } finally {
    loading.value = false
  }
}

async function loadDisponibles() {
  try {
    const res = await fetch(`${API}/combattants/disponibles`, { headers: props.authHeaders() })
    if (res.ok) disponibles.value = await res.json()
  } catch { /* silencieux */ }
}

async function recruter(id) {
  recruiting.value = id
  try {
    const res = await fetch(`${API}/combattants/${id}/recruter`, {
      method: 'POST',
      headers: props.authHeaders()
    })
    if (res.ok) {
      const idx = disponibles.value.findIndex(f => f.combattantID === id)
      if (idx !== -1) disponibles.value.splice(idx, 1)
      await loadEcurie()
      if (dialog.value && dialogFighter.value?.combattantID === id) dialog.value = false
    } else {
      const msg = await res.text()
      snackbarMsg.value = msg || 'Erreur lors du recrutement.'
      snackbar.value = true
    }
  } finally {
    recruiting.value = null
  }
}

function formatPrix(val) {
  return Number(val ?? 0).toLocaleString('fr-FR') + ' €'
}

function openDialog(fighter, mode = 'view') {
  dialogFighter.value = fighter
  dialogMode.value = mode
  dialog.value = true
}

function statColor(val) {
  if (val >= 75) return '#22c55e'
  if (val >= 55) return '#f59e0b'
  if (val >= 40) return '#f97316'
  return '#ef4444'
}

// Dégradé continu 0→rouge, 50→jaune, 100→vert pour les stats brutes
function rawStatColor(val) {
  const n = typeof val === 'number' && !isNaN(val) ? Math.max(0, Math.min(100, val)) : 0
  const hue = Math.round((n / 100) * 120)
  return `hsl(${hue}, 70%, 52%)`
}

function noteColor(note) {
  if (note >= 65) return '#22c55e'
  if (note >= 55) return '#f59e0b'
  if (note >= 45) return '#f97316'
  return '#ef4444'
}

function styleIcon(style) {
  return STYLE_ICONS[style] ?? '🥋'
}

function catColor(cat) {
  return CAT_COLORS[cat] ?? '#6366f1'
}

onMounted(() => {
  loadEcurie()
  loadDisponibles()
})
</script>

<template>
  <!-- ── Sub-navigation ─────────────────────────────────────────── -->
  <div class="fighters-subnav">
    <button
      class="fighters-subnav-btn"
      :class="{ active: subTab === 'ecurie' }"
      @click="subTab = 'ecurie'"
    >
      🏠 Mon Écurie
      <span v-if="ecurie.length" class="subnav-badge">{{ ecurie.length }}</span>
    </button>
    <button
      class="fighters-subnav-btn"
      :class="{ active: subTab === 'recruter' }"
      @click="subTab = 'recruter'"
    >
      🔍 Recruter
      <span v-if="disponibles.length" class="subnav-badge muted">{{ disponibles.length }}</span>
    </button>
  </div>

  <!-- ── Écurie ─────────────────────────────────────────────────── -->
  <div v-if="subTab === 'ecurie'">
    <div v-if="loading" class="text-center py-12">
      <v-progress-circular indeterminate color="indigo" size="40" />
    </div>

    <div v-else-if="ecurie.length === 0" class="empty-state">
      <span class="empty-icon">👊</span>
      <p>Ton écurie est vide. Va recruter ton premier combattant !</p>
      <v-btn variant="outlined" rounded="pill" size="small" class="mt-3" style="border-color:#6366f1;color:#6366f1"
        @click="subTab = 'recruter'">
        Voir les combattants disponibles
      </v-btn>
    </div>

    <v-row v-else>
      <v-col
        v-for="f in ecurie"
        :key="f.combattantID"
        cols="12" sm="6" md="4" lg="3"
      >
        <v-card
          class="fighter-card"
          elevation="6"
          rounded="xl"
          @click="openDialog(f, 'view')"
          style="cursor:pointer"
        >
          <v-card-item>
            <div class="fighter-card-header">
              <span class="fighter-style-icon">{{ styleIcon(f.stylePrincipal) }}</span>
              <span
                class="fighter-cat-badge"
                :style="{ background: catColor(f.categoriePoids) + '22', color: catColor(f.categoriePoids), borderColor: catColor(f.categoriePoids) + '55' }"
              >{{ f.categoriePoids }}</span>
            </div>
            <div class="fighter-name">{{ f.prenom }} {{ f.nom }}</div>
            <div class="fighter-meta">
              {{ f.nationalite }} · {{ f.age }} ans · {{ f.stylePrincipal }}
            </div>
          </v-card-item>
          <v-card-text class="pt-0">
            <div class="fighter-note-row">
              <span class="fighter-note-label">Note globale</span>
              <span class="fighter-note-val" :style="{ color: noteColor(f.noteGlobale) }">
                {{ f.noteGlobale }}
              </span>
            </div>
            <div class="fighter-mini-stats">
              <div class="mini-stat" v-for="[label, val] in [
                ['Frappe', f.compStriking],
                ['Lutte',  f.compLutte],
                ['Grapl.', f.compGrappling],
              ]" :key="label">
                <span class="mini-stat-label">{{ label }}</span>
                <div class="mini-stat-track">
                  <div class="mini-stat-fill" :style="{ width: val + '%', background: statColor(val) }" />
                </div>
                <span class="mini-stat-val" :style="{ color: statColor(val) }">{{ val }}</span>
              </div>
            </div>
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>
  </div>

  <!-- ── Recruter ───────────────────────────────────────────────── -->
  <div v-if="subTab === 'recruter'">

    <!-- Filtre catégorie -->
    <div class="cat-filter">
      <button
        v-for="cat in CATEGORIES"
        :key="cat"
        class="cat-filter-btn"
        :class="{ active: filterCat === cat }"
        :style="filterCat === cat && cat !== 'Toutes'
          ? { background: catColor(cat) + '22', color: catColor(cat), borderColor: catColor(cat) + '66' }
          : {}"
        @click="filterCat = cat"
      >
        {{ cat }}
      </button>
    </div>

    <div v-if="filteredDisponibles.length === 0" class="empty-state">
      <span class="empty-icon">🔍</span>
      <p v-if="disponibles.length === 0">Tous les combattants sont déjà dans ton écurie !</p>
      <p v-else>Aucun combattant dans cette catégorie.</p>
    </div>

    <!-- Liste des combattants disponibles -->
    <div class="recruit-list">
      <div
        v-for="f in filteredDisponibles"
        :key="f.combattantID"
        class="recruit-row"
      >
        <!-- Style icon -->
        <span class="recruit-style-icon">{{ styleIcon(f.stylePrincipal) }}</span>

        <!-- Infos principales -->
        <div class="recruit-info">
          <span class="recruit-name">{{ f.prenom }} {{ f.nom }}</span>
          <span class="recruit-sub">{{ f.nationalite }} · {{ f.age }} ans · {{ f.stylePrincipal }}</span>
        </div>

        <!-- Catégorie -->
        <span
          class="recruit-cat"
          :style="{ color: catColor(f.categoriePoids), borderColor: catColor(f.categoriePoids) + '55', background: catColor(f.categoriePoids) + '18' }"
        >{{ f.categoriePoids }}</span>

        <!-- Note globale -->
        <span class="recruit-note" :style="{ color: noteColor(f.noteGlobale) }">
          {{ f.noteGlobale }}
        </span>

        <!-- Prix -->
        <span class="recruit-prix">💰 {{ formatPrix(f.prixAchat) }}</span>

        <!-- Actions -->
        <div class="recruit-actions">
          <v-btn
            size="small"
            variant="text"
            rounded="pill"
            class="recruit-btn-info"
            @click="openDialog(f, 'recruit')"
          >
            Infos
          </v-btn>
          <v-btn
            size="small"
            variant="outlined"
            rounded="pill"
            class="recruit-btn-recruit"
            :loading="recruiting === f.combattantID"
            @click="recruter(f.combattantID)"
          >
            Recruter
          </v-btn>
        </div>
      </div>
    </div>
  </div>

  <!-- ── Dialog détail combattant ──────────────────────────────── -->
  <v-dialog v-model="dialog" max-width="680" scrollable>
    <v-card v-if="dialogFighter" class="fighter-dialog" rounded="xl" elevation="16">

      <!-- ── Barre titre ── -->
      <div class="dialog-topbar">
        <span
          class="fighter-cat-badge"
          :style="{ background: catColor(dialogFighter.categoriePoids) + '22', color: catColor(dialogFighter.categoriePoids), borderColor: catColor(dialogFighter.categoriePoids) + '55' }"
        >{{ dialogFighter.categoriePoids }}</span>
        <v-spacer />
        <v-btn icon="mdi-close" variant="text" size="small" @click="dialog = false" />
      </div>

      <!-- ── Hero section ── -->
      <div class="dialog-hero">
        <div class="dialog-hero-left">
          <span class="dialog-style-icon">{{ styleIcon(dialogFighter.stylePrincipal) }}</span>
          <div>
            <div class="dialog-name">{{ dialogFighter.prenom }} {{ dialogFighter.nom }}</div>
            <div class="dialog-nickname" v-if="dialogFighter.biographie">{{ dialogFighter.biographie }}</div>
          </div>
        </div>
        <div class="dialog-overall-badge" :style="{ color: noteColor(dialogFighter.noteGlobale), borderColor: noteColor(dialogFighter.noteGlobale) + '55', background: noteColor(dialogFighter.noteGlobale) + '14' }">
          <span class="dialog-overall-num">{{ dialogFighter.noteGlobale }}</span>
          <span class="dialog-overall-label">OVR</span>
        </div>
      </div>

      <v-card-text class="dialog-body">

        <!-- ── Identité ── -->
        <div class="dialog-section-title">👤 Identité</div>
        <div class="dialog-identity-grid">
          <div class="dialog-id-item">
            <span class="dialog-id-label">🎂 Âge</span>
            <span class="dialog-id-val">{{ dialogFighter.age }} ans</span>
          </div>
          <div class="dialog-id-item">
            <span class="dialog-id-label">🌍 Nationalité</span>
            <span class="dialog-id-val">{{ dialogFighter.nationalite }}</span>
          </div>
          <div class="dialog-id-item">
            <span class="dialog-id-label">⚧ Genre</span>
            <span class="dialog-id-val">{{ dialogFighter.genre === 'F' ? 'Femme' : 'Homme' }}</span>
          </div>
          <div class="dialog-id-item">
            <span class="dialog-id-label">🥋 Style</span>
            <span class="dialog-id-val">{{ styleIcon(dialogFighter.stylePrincipal) }} {{ dialogFighter.stylePrincipal }}</span>
          </div>
        </div>

        <!-- ── Bilan sportif ── -->
        <div class="dialog-section-title">🏆 Bilan sportif</div>
        <div class="dialog-record-block">
          <div class="dialog-record-col record-win">
            <span class="record-big">{{ dialogFighter.victoires }}</span>
            <span class="record-label">Victoires</span>
            <div class="record-breakdown">
              <span v-if="dialogFighter.victoiresKO">{{ dialogFighter.victoiresKO }} KO</span>
              <span v-if="dialogFighter.victoiresSub">{{ dialogFighter.victoiresSub }} Sub</span>
              <span v-if="dialogFighter.victoiresDec">{{ dialogFighter.victoiresDec }} Déc</span>
            </div>
          </div>
          <div class="record-separator">—</div>
          <div class="dialog-record-col record-loss">
            <span class="record-big">{{ dialogFighter.defaites }}</span>
            <span class="record-label">Défaites</span>
            <div class="record-breakdown">
              <span v-if="dialogFighter.defaitesKO">{{ dialogFighter.defaitesKO }} KO</span>
              <span v-if="dialogFighter.defaitesSub">{{ dialogFighter.defaitesSub }} Sub</span>
              <span v-if="dialogFighter.defaitesDec">{{ dialogFighter.defaitesDec }} Déc</span>
            </div>
          </div>
          <div class="record-separator">—</div>
          <div class="dialog-record-col record-draw">
            <span class="record-big">{{ dialogFighter.nuls }}</span>
            <span class="record-label">Nuls</span>
          </div>
        </div>

        <!-- ── Stats composites ── -->
        <div class="dialog-section-title">📊 Statistiques</div>
        <div class="dialog-stats">
          <div
            v-for="[icon, label, val] in [
              ['🥊', 'Frappe debout',   dialogFighter.compStriking],
              ['🤼', 'Lutte',           dialogFighter.compLutte],
              ['⛩️',  'Grappling',       dialogFighter.compGrappling],
              ['🏋️', 'Conditionnement', dialogFighter.compConditioning],
              ['💪', 'Endurance',       dialogFighter.compStamina],
              ['🧠', 'Mental',          dialogFighter.compMental],
            ]"
            :key="label"
            class="dialog-stat-row"
          >
            <span class="dialog-stat-icon">{{ icon }}</span>
            <span class="dialog-stat-label">{{ label }}</span>
            <div class="dialog-stat-track">
              <div class="dialog-stat-fill" :style="{ width: val + '%', background: statColor(val) }" />
            </div>
            <span class="dialog-stat-val" :style="{ color: statColor(val) }">{{ val }}</span>
          </div>
        </div>

        <!-- ── Stats détaillées ── -->
        <div class="dialog-section-title">🔬 Stats détaillées</div>
        <div class="dialog-raw-stats">
          <div class="raw-group">
            <div class="raw-group-title">🥊 Striking</div>
            <div class="raw-stat-row" v-for="[lbl, v] in [['Frappe', dialogFighter.statFrappeDebout], ['Puissance', dialogFighter.statPuissance], ['Précision', dialogFighter.statPrecision]]" :key="lbl">
              <span class="raw-label">{{ lbl }}</span>
              <div class="raw-track"><div class="raw-fill" :style="{ width: (v ?? 0) + '%', background: rawStatColor(v ?? 0) }" /></div>
              <span class="raw-val" :style="{ color: rawStatColor(v ?? 0) }">{{ v ?? '—' }}</span>
            </div>
          </div>
          <div class="raw-group">
            <div class="raw-group-title">🤼 Lutte</div>
            <div class="raw-stat-row" v-for="[lbl, v] in [['Wrestling', dialogFighter.statWrestling], ['Takedown', dialogFighter.statTakedown], ['Anti-TD', dialogFighter.statAntiTakedown]]" :key="lbl">
              <span class="raw-label">{{ lbl }}</span>
              <div class="raw-track"><div class="raw-fill" :style="{ width: (v ?? 0) + '%', background: rawStatColor(v ?? 0) }" /></div>
              <span class="raw-val" :style="{ color: rawStatColor(v ?? 0) }">{{ v ?? '—' }}</span>
            </div>
          </div>
          <div class="raw-group">
            <div class="raw-group-title">⛩️ Grappling</div>
            <div class="raw-stat-row" v-for="[lbl, v] in [['Jiu-Jitsu', dialogFighter.statJiuJitsu], ['Submission', dialogFighter.statSubmission], ['Évasion Sub', dialogFighter.statEvasionSub]]" :key="lbl">
              <span class="raw-label">{{ lbl }}</span>
              <div class="raw-track"><div class="raw-fill" :style="{ width: (v ?? 0) + '%', background: rawStatColor(v ?? 0) }" /></div>
              <span class="raw-val" :style="{ color: rawStatColor(v ?? 0) }">{{ v ?? '—' }}</span>
            </div>
          </div>
          <div class="raw-group">
            <div class="raw-group-title">🏋️ Physique</div>
            <div class="raw-stat-row" v-for="[lbl, v] in [['Force', dialogFighter.statForce], ['Vitesse', dialogFighter.statVitesse], ['Agilité', dialogFighter.statAgilite], ['Cardio', dialogFighter.statCardio], ['Récupération', dialogFighter.statRecuperation], ['Menton', dialogFighter.statMentoniere]]" :key="lbl">
              <span class="raw-label">{{ lbl }}</span>
              <div class="raw-track"><div class="raw-fill" :style="{ width: (v ?? 0) + '%', background: rawStatColor(v ?? 0) }" /></div>
              <span class="raw-val" :style="{ color: rawStatColor(v ?? 0) }">{{ v ?? '—' }}</span>
            </div>
          </div>
          <div class="raw-group">
            <div class="raw-group-title">🧠 Mental</div>
            <div class="raw-stat-row" v-for="[lbl, v] in [['Mental', dialogFighter.statMental], ['Expérience', dialogFighter.statExperience], ['Adaptation', dialogFighter.statAdaptation]]" :key="lbl">
              <span class="raw-label">{{ lbl }}</span>
              <div class="raw-track"><div class="raw-fill" :style="{ width: (v ?? 0) + '%', background: rawStatColor(v ?? 0) }" /></div>
              <span class="raw-val" :style="{ color: rawStatColor(v ?? 0) }">{{ v ?? '—' }}</span>
            </div>
          </div>
        </div>

        <!-- ── Coûts recrutement ── -->
        <div v-if="dialogMode === 'recruit'" class="dialog-cost-block">
          <div class="dialog-section-title" style="margin-top:0">💰 Recrutement</div>
          <div class="dialog-cost-row">
            <span class="dialog-cost-label">Coût de recrutement</span>
            <span class="dialog-cost-val">{{ formatPrix(dialogFighter.prixAchat) }}</span>
          </div>
          <div class="dialog-cost-row">
            <span class="dialog-cost-label">Salaire mensuel</span>
            <span class="dialog-cost-sal">{{ formatPrix(dialogFighter.salaireMensuel) }} / mois</span>
          </div>
        </div>

      </v-card-text>

      <!-- Actions -->
      <v-card-actions class="dialog-actions">
        <v-btn variant="text" rounded="pill" @click="dialog = false">Fermer</v-btn>
        <v-spacer />
        <v-btn
          v-if="dialogMode === 'recruit'"
          variant="flat"
          rounded="pill"
          color="indigo"
          :loading="recruiting === dialogFighter.combattantID"
          @click="recruter(dialogFighter.combattantID)"
        >
          👊 Recruter — {{ formatPrix(dialogFighter.prixAchat) }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>

  <!-- Snackbar erreur recrutement -->
  <v-snackbar v-model="snackbar" color="error" timeout="4000" location="top">
    {{ snackbarMsg }}
    <template #actions>
      <v-btn variant="text" @click="snackbar = false">Fermer</v-btn>
    </template>
  </v-snackbar>

</template>

<style scoped>
/* ── Sub-navigation ──────────────────────────────────────────────── */
.fighters-subnav {
  display: flex;
  gap: 8px;
  margin-bottom: 24px;
}
.fighters-subnav-btn {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 20px;
  border-radius: 999px;
  border: 1.5px solid rgba(99,102,241,.25);
  background: rgba(99,102,241,.06);
  color: #94a3b8;
  font-size: .9rem;
  cursor: pointer;
  transition: all .2s;
}
.fighters-subnav-btn.active {
  border-color: #6366f1;
  background: rgba(99,102,241,.18);
  color: #a5b4fc;
}
.subnav-badge {
  background: #6366f1;
  color: #fff;
  font-size: .72rem;
  font-weight: 700;
  border-radius: 999px;
  padding: 1px 7px;
}
.subnav-badge.muted {
  background: rgba(99,102,241,.3);
  color: #a5b4fc;
}

/* ── Empty state ─────────────────────────────────────────────────── */
.empty-state {
  text-align: center;
  padding: 60px 20px;
  color: #64748b;
}
.empty-icon { font-size: 3rem; display: block; margin-bottom: 16px; }

/* ── Fighter card (écurie) ───────────────────────────────────────── */
.fighter-card {
  background: linear-gradient(135deg, #1e1b4b 0%, #1e293b 100%);
  border: 1px solid rgba(99,102,241,.2);
  transition: transform .18s, border-color .18s;
}
.fighter-card:hover {
  transform: translateY(-3px);
  border-color: rgba(99,102,241,.5);
}
.fighter-card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 6px;
}
.fighter-style-icon { font-size: 1.6rem; }
.fighter-cat-badge {
  font-size: .72rem;
  font-weight: 700;
  border-radius: 999px;
  border: 1px solid;
  padding: 2px 10px;
  letter-spacing: .03em;
}
.fighter-name {
  font-size: 1.05rem;
  font-weight: 700;
  color: #e2e8f0;
  margin-bottom: 2px;
}
.fighter-meta {
  font-size: .78rem;
  color: #64748b;
}
.fighter-note-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 10px;
}
.fighter-note-label { font-size: .78rem; color: #64748b; }
.fighter-note-val { font-size: 1.1rem; font-weight: 800; }

/* Mini stats sur la card */
.fighter-mini-stats { display: flex; flex-direction: column; gap: 4px; }
.mini-stat { display: grid; grid-template-columns: 48px 1fr 28px; align-items: center; gap: 6px; }
.mini-stat-label { font-size: .72rem; color: #64748b; }
.mini-stat-track { height: 4px; background: rgba(255,255,255,.08); border-radius: 4px; overflow: hidden; }
.mini-stat-fill { height: 100%; border-radius: 4px; transition: width .4s; }
.mini-stat-val { font-size: .72rem; font-weight: 700; text-align: right; }

/* ── Filtre catégorie ────────────────────────────────────────────── */
.cat-filter {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-bottom: 20px;
}
.cat-filter-btn {
  padding: 4px 14px;
  border-radius: 999px;
  border: 1.5px solid rgba(99,102,241,.2);
  background: transparent;
  color: #64748b;
  font-size: .82rem;
  cursor: pointer;
  transition: all .18s;
}
.cat-filter-btn.active {
  border-color: #6366f1;
  background: rgba(99,102,241,.15);
  color: #a5b4fc;
}

/* ── Liste recrutement ───────────────────────────────────────────── */
.recruit-list { display: flex; flex-direction: column; gap: 8px; }
.recruit-row {
  display: grid;
  grid-template-columns: 36px 1fr auto auto auto auto;
  align-items: center;
  gap: 12px;
  padding: 12px 16px;
  background: rgba(30,27,75,.5);
  border: 1px solid rgba(99,102,241,.15);
  border-radius: 14px;
  transition: border-color .18s;
}
.recruit-row:hover { border-color: rgba(99,102,241,.4); }
.recruit-style-icon { font-size: 1.4rem; text-align: center; }
.recruit-info { display: flex; flex-direction: column; min-width: 0; }
.recruit-name { font-size: .92rem; font-weight: 700; color: #e2e8f0; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.recruit-sub { font-size: .75rem; color: #64748b; }
.recruit-cat {
  font-size: .72rem;
  font-weight: 700;
  border-radius: 999px;
  border: 1px solid;
  padding: 2px 10px;
  white-space: nowrap;
}
.recruit-note { font-size: 1rem; font-weight: 800; min-width: 28px; text-align: center; }
.recruit-prix {
  font-size: .78rem;
  font-weight: 700;
  color: #f59e0b;
  white-space: nowrap;
}
.recruit-actions { display: flex; gap: 6px; align-items: center; }
.recruit-btn-info { color: #94a3b8 !important; font-size: .8rem !important; }
.recruit-btn-recruit {
  border-color: rgba(99,102,241,.5) !important;
  color: #a5b4fc !important;
  font-size: .8rem !important;
}

/* ── Dialog ──────────────────────────────────────────────────────── */
.fighter-dialog {
  background: linear-gradient(160deg, #1e1b4b 0%, #1e293b 100%);
  border: 1px solid rgba(99,102,241,.3);
}
.dialog-topbar {
  display: flex;
  align-items: center;
  padding: 14px 16px 8px;
  gap: 10px;
}
.dialog-hero {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 4px 20px 18px;
  gap: 16px;
}
.dialog-hero-left { display: flex; align-items: center; gap: 14px; }
.dialog-style-icon { font-size: 2.8rem; line-height: 1; }
.dialog-name { font-size: 1.25rem; font-weight: 800; color: #e2e8f0; }
.dialog-nickname { font-size: .82rem; color: #64748b; margin-top: 3px; font-style: italic; }
.dialog-overall-badge {
  display: flex;
  flex-direction: column;
  align-items: center;
  border: 2px solid;
  border-radius: 14px;
  padding: 8px 18px;
  min-width: 72px;
}
.dialog-overall-num { font-size: 2rem; font-weight: 900; line-height: 1; }
.dialog-overall-label { font-size: .65rem; font-weight: 700; letter-spacing: .1em; opacity: .7; margin-top: 2px; }

.dialog-body { padding: 0 20px 8px; }
.dialog-section-title {
  font-size: .7rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: .08em;
  color: #475569;
  margin: 18px 0 10px;
}

/* Identité */
.dialog-identity-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 8px;
}
.dialog-id-item {
  display: flex;
  flex-direction: column;
  gap: 2px;
  padding: 8px 12px;
  background: rgba(255,255,255,.04);
  border-radius: 10px;
  border: 1px solid rgba(255,255,255,.06);
}
.dialog-id-label { font-size: .68rem; color: #475569; }
.dialog-id-val   { font-size: .88rem; font-weight: 600; color: #e2e8f0; }

/* Bilan sportif */
.dialog-record-block {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 12px;
  padding: 16px 12px;
  background: rgba(255,255,255,.03);
  border-radius: 14px;
  border: 1px solid rgba(255,255,255,.06);
}
.dialog-record-col {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 3px;
  flex: 1;
}
.record-big { font-size: 2.2rem; font-weight: 900; line-height: 1; }
.record-label { font-size: .72rem; color: #64748b; font-weight: 600; }
.record-breakdown {
  display: flex;
  gap: 6px;
  margin-top: 4px;
  flex-wrap: wrap;
  justify-content: center;
}
.record-breakdown span {
  font-size: .65rem;
  color: #475569;
  background: rgba(255,255,255,.06);
  border-radius: 6px;
  padding: 1px 6px;
}
.record-win  .record-big { color: #22c55e; }
.record-loss .record-big { color: #ef4444; }
.record-draw .record-big { color: #94a3b8; }
.record-separator { font-size: 1.4rem; color: #1e293b; font-weight: 900; flex: none; }

/* Stats composites */
.dialog-stats { display: flex; flex-direction: column; gap: 7px; }
.dialog-stat-row {
  display: grid;
  grid-template-columns: 22px 130px 1fr 32px;
  align-items: center;
  gap: 8px;
}
.dialog-stat-icon { font-size: .95rem; text-align: center; }
.dialog-stat-label { font-size: .82rem; color: #94a3b8; }
.dialog-stat-track { height: 6px; background: rgba(255,255,255,.07); border-radius: 6px; overflow: hidden; }
.dialog-stat-fill { height: 100%; border-radius: 6px; transition: width .5s ease; }
.dialog-stat-val { font-size: .82rem; font-weight: 700; text-align: right; }

/* Stats détaillées */
.dialog-raw-stats {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 12px;
}
.raw-group {
  background: rgba(255,255,255,.03);
  border-radius: 10px;
  border: 1px solid rgba(255,255,255,.05);
  padding: 10px 12px;
}
.raw-group-title {
  font-size: .7rem;
  font-weight: 700;
  color: #6366f1;
  margin-bottom: 8px;
  text-transform: uppercase;
  letter-spacing: .06em;
}
.raw-stat-row {
  display: grid;
  grid-template-columns: 80px 1fr 26px;
  align-items: center;
  gap: 6px;
  margin-bottom: 5px;
}
.raw-label { font-size: .72rem; color: #64748b; }
.raw-track { height: 4px; background: rgba(255,255,255,.06); border-radius: 4px; overflow: hidden; }
.raw-fill { height: 100%; border-radius: 4px; }
.raw-val { font-size: .72rem; font-weight: 700; text-align: right; }

/* Bloc coûts recrutement */
.dialog-cost-block {
  margin-top: 4px;
  padding: 12px 14px;
  border-radius: 12px;
  background: rgba(245,158,11,.06);
  border: 1px solid rgba(245,158,11,.2);
  display: flex;
  flex-direction: column;
  gap: 6px;
}
.dialog-cost-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
}
.dialog-cost-label { font-size: .8rem; color: #94a3b8; }
.dialog-cost-val   { font-size: .92rem; font-weight: 700; color: #f59e0b; }
.dialog-cost-sal   { font-size: .85rem; font-weight: 600; color: #94a3b8; }

.dialog-actions { padding: 12px 20px 16px; }
</style>
