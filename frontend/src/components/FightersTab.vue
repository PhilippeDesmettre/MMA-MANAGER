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
const dialog       = ref(false)
const dialogFighter = ref(null)
const dialogMode   = ref('view') // 'view' | 'recruit'

// Filtre catégorie dans recrutement
const filterCat    = ref('Toutes')

const CATEGORIES = ['Toutes', 'Plume', 'Léger', 'Welter', 'Moyen', 'Mi-lourd', 'Lourd']

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
  'Plume':    '#38bdf8',
  'Léger':    '#818cf8',
  'Welter':   '#a78bfa',
  'Moyen':    '#6366f1',
  'Mi-lourd': '#f97316',
  'Lourd':    '#ef4444',
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
      // Déplace le combattant de disponibles → écurie
      const idx = disponibles.value.findIndex(f => f.combattantID === id)
      if (idx !== -1) disponibles.value.splice(idx, 1)
      await loadEcurie()
      // Ferme le dialog si on venait de là
      if (dialog.value && dialogFighter.value?.combattantID === id) dialog.value = false
    }
  } finally {
    recruiting.value = null
  }
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
  <v-dialog v-model="dialog" max-width="560" scrollable>
    <v-card v-if="dialogFighter" class="fighter-dialog" rounded="xl" elevation="16">
      <!-- Header -->
      <div class="dialog-header">
        <div class="dialog-header-left">
          <span class="dialog-style-icon">{{ styleIcon(dialogFighter.stylePrincipal) }}</span>
          <div>
            <div class="dialog-name">{{ dialogFighter.prenom }} {{ dialogFighter.nom }}</div>
            <div class="dialog-meta">
              {{ dialogFighter.nationalite }} · {{ dialogFighter.age }} ans
            </div>
          </div>
        </div>
        <div class="dialog-header-right">
          <span
            class="fighter-cat-badge"
            :style="{ background: catColor(dialogFighter.categoriePoids) + '22', color: catColor(dialogFighter.categoriePoids), borderColor: catColor(dialogFighter.categoriePoids) + '55' }"
          >{{ dialogFighter.categoriePoids }}</span>
          <v-btn icon="mdi-close" variant="text" size="small" @click="dialog = false" />
        </div>
      </div>

      <v-divider style="border-color:rgba(99,102,241,.2)" />

      <v-card-text class="dialog-body">

        <!-- Note globale -->
        <div class="dialog-note-block">
          <span class="dialog-note-label">Note globale</span>
          <span
            class="dialog-note-val"
            :style="{ color: noteColor(dialogFighter.noteGlobale) }"
          >{{ dialogFighter.noteGlobale }}</span>
          <span class="dialog-note-sub">/ 99</span>
        </div>

        <!-- Bio -->
        <p v-if="dialogFighter.biographie" class="dialog-bio">
          {{ dialogFighter.biographie }}
        </p>

        <!-- Stats -->
        <div class="dialog-stats">
          <div
            v-for="[label, val] in [
              ['Frappe debout',     dialogFighter.compStriking],
              ['Lutte',             dialogFighter.compLutte],
              ['Grappling',         dialogFighter.compGrappling],
              ['Conditionnement',   dialogFighter.compConditioning],
              ['Endurance',         dialogFighter.compStamina],
              ['Mental',            dialogFighter.compMental],
            ]"
            :key="label"
            class="dialog-stat-row"
          >
            <span class="dialog-stat-label">{{ label }}</span>
            <div class="dialog-stat-track">
              <div
                class="dialog-stat-fill"
                :style="{ width: val + '%', background: statColor(val) }"
              />
            </div>
            <span class="dialog-stat-val" :style="{ color: statColor(val) }">{{ val }}</span>
          </div>
        </div>

      </v-card-text>

      <v-divider style="border-color:rgba(99,102,241,.2)" />

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
          👊 Recruter
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>

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
  grid-template-columns: 36px 1fr auto auto auto;
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
.dialog-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 20px 20px 16px;
}
.dialog-header-left { display: flex; align-items: center; gap: 14px; }
.dialog-style-icon { font-size: 2.4rem; }
.dialog-name { font-size: 1.2rem; font-weight: 800; color: #e2e8f0; }
.dialog-meta { font-size: .82rem; color: #64748b; margin-top: 2px; }
.dialog-header-right { display: flex; align-items: center; gap: 10px; }

.dialog-body { padding: 20px; }
.dialog-note-block {
  display: flex;
  align-items: baseline;
  gap: 8px;
  margin-bottom: 16px;
}
.dialog-note-label { font-size: .82rem; color: #64748b; }
.dialog-note-val { font-size: 2rem; font-weight: 900; }
.dialog-note-sub { font-size: .82rem; color: #475569; }

.dialog-bio {
  font-size: .85rem;
  color: #94a3b8;
  line-height: 1.6;
  margin-bottom: 20px;
  padding: 12px 14px;
  background: rgba(99,102,241,.06);
  border-left: 3px solid rgba(99,102,241,.4);
  border-radius: 4px;
  font-style: italic;
}

.dialog-stats { display: flex; flex-direction: column; gap: 8px; }
.dialog-stat-row { display: grid; grid-template-columns: 130px 1fr 32px; align-items: center; gap: 10px; }
.dialog-stat-label { font-size: .82rem; color: #94a3b8; }
.dialog-stat-track { height: 6px; background: rgba(255,255,255,.07); border-radius: 6px; overflow: hidden; }
.dialog-stat-fill { height: 100%; border-radius: 6px; transition: width .5s ease; }
.dialog-stat-val { font-size: .82rem; font-weight: 700; text-align: right; }

.dialog-actions { padding: 12px 20px 16px; }
</style>
