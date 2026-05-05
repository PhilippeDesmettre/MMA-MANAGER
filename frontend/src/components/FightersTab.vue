<script setup>
import { ref, computed, onMounted } from 'vue'
import fighterDefault  from '../assets/fighter-default.png'
import fighterDefaultF from '../assets/fighter-default-f.png'
import { getFlagClass } from '../utils/countryFlags.js'

const props = defineProps({
  authHeaders: { type: Function, required: true },
  partie:      { type: Object, default: null }
})
const emit = defineEmits(['ecurie-updated', 'argent-updated'])

const API = 'http://localhost:5219/api'

const subTab       = ref('ecurie')
const ecurie       = ref([])
const disponibles  = ref([])
const loading      = ref(false)
const recruiting   = ref(null)

const dialog        = ref(false)
const dialogFighter = ref(null)
const dialogMode    = ref('view')

const snackbar      = ref(false)
const snackbarMsg   = ref('')

const filterCat    = ref('Toutes')

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
  'Poids moyen':     '#dc2626',
  'Poids mi-lourd':  '#f97316',
  'Lourd':           '#ef4444',
  'Super-lourd':     '#b91c1c',
  'Poids paille F':    '#22d3ee',
  'Poids mouche F':    '#34d399',
  'Poids coq F':       '#38bdf8',
  'Poids plume F':     '#818cf8',
  'Poids léger F':     '#a78bfa',
  'Poids mi-moyen F':  '#c084fc',
}

const filteredDisponibles = computed(() =>
  filterCat.value === 'Toutes'
    ? disponibles.value
    : disponibles.value.filter(f => f.categoriePoids === filterCat.value)
)

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
  } catch {}
}

async function recruter(id) {
  recruiting.value = id
  try {
    const res = await fetch(`${API}/combattants/${id}/recruter`, {
      method: 'POST', headers: props.authHeaders()
    })
    if (res.ok) {
      const data = await res.json()
      emit('argent-updated', data.nouveauSolde)
      const idx = disponibles.value.findIndex(f => f.combattantID === id)
      if (idx !== -1) disponibles.value.splice(idx, 1)
      await loadEcurie()
      if (dialog.value && dialogFighter.value?.combattantID === id) dialog.value = false
    } else {
      snackbarMsg.value = await res.text() || 'Erreur lors du recrutement.'
      snackbar.value = true
    }
  } finally {
    recruiting.value = null
  }
}

function formatPrix(val) { return Number(val ?? 0).toLocaleString('fr-FR') + ' €' }

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

function rawStatColor(val) {
  const n = typeof val === 'number' && !isNaN(val) ? Math.max(0, Math.min(100, val)) : 0
  return `hsl(${Math.round((n / 100) * 120)}, 70%, 52%)`
}

function noteColor(note) {
  if (note >= 65) return '#22c55e'
  if (note >= 55) return '#f59e0b'
  if (note >= 45) return '#f97316'
  return '#ef4444'
}

function styleIcon(style) { return STYLE_ICONS[style] ?? '🥋' }
function catColor(cat) { return CAT_COLORS[cat] ?? '#dc2626' }

function fighterImg(genre) { return genre === 'F' ? fighterDefaultF : fighterDefault }

function phaseIcon(phase) {
  return { 'Développement': '📈', 'Pic': '⭐', 'Déclin': '📉', 'Vétéran': '🏛️' }[phase] ?? '❓'
}

onMounted(() => { loadEcurie(); loadDisponibles() })
</script>

<template>
  <!-- ── Sub-navigation ─────────────────────────────────────────── -->
  <div class="fighters-subnav">
    <button class="fighters-subnav-btn" :class="{ active: subTab === 'ecurie' }" @click="subTab = 'ecurie'">
      🏠 Mon Écurie
      <span v-if="ecurie.length" class="subnav-badge">{{ ecurie.length }}</span>
    </button>
    <button class="fighters-subnav-btn" :class="{ active: subTab === 'recruter' }" @click="subTab = 'recruter'">
      🔍 Recruter
      <span v-if="disponibles.length" class="subnav-badge muted">{{ disponibles.length }}</span>
    </button>
  </div>

  <!-- ── Écurie ─────────────────────────────────────────────────── -->
  <div v-if="subTab === 'ecurie'">
    <div v-if="loading" class="text-center py-12">
      <v-progress-circular indeterminate color="red-darken-2" size="40" />
    </div>

    <div v-else-if="ecurie.length === 0" class="empty-state">
      <span class="empty-icon">👊</span>
      <p>Ton écurie est vide. Va recruter ton premier combattant !</p>
      <v-btn variant="outlined" rounded="pill" size="small" class="mt-3"
        style="border-color:#dc2626;color:#dc2626" @click="subTab = 'recruter'">
        Voir les combattants disponibles
      </v-btn>
    </div>

    <v-row v-else>
      <v-col v-for="f in ecurie" :key="f.combattantID" cols="12" sm="6" md="4" lg="3">
        <v-card class="fighter-card" elevation="6" rounded="xl" @click="openDialog(f, 'view')" style="cursor:pointer">
          <v-card-item>
            <div class="fighter-card-header">
              <img :src="fighterImg(f.genre)" class="fighter-avatar" alt="" />
              <span class="fighter-cat-badge"
                :style="{ background: catColor(f.categoriePoids) + '22', color: catColor(f.categoriePoids), borderColor: catColor(f.categoriePoids) + '55' }">
                {{ f.categoriePoids }}
              </span>
            </div>
            <div class="fighter-name">
              {{ f.prenom }} {{ f.nom }}
              <span v-if="f.semainesIndispo > 0" class="injury-badge">
                🏥 {{ f.blessureZone }} ({{ f.semainesIndispo }} tour{{ f.semainesIndispo > 1 ? 's' : '' }})
              </span>
            </div>
            <div class="fighter-meta">
              <span v-if="f.codePays" :class="getFlagClass(f.codePays)" class="flag-sm" />
              {{ f.nationalite }} · {{ f.age }} ans
              <span class="phase-mini" :class="`phase-${f.phaseCarriere?.toLowerCase()}`">
                {{ phaseIcon(f.phaseCarriere) }}
              </span>
              · {{ styleIcon(f.stylePrincipal) }} {{ f.stylePrincipal }}
              <span v-if="f.meilleurRangOrga" class="rang-badge">{{ f.meilleurRangOrga }}</span>
            </div>
          </v-card-item>
          <v-card-text class="pt-0">
            <div class="fighter-note-row">
              <span class="fighter-note-label">Note globale</span>
              <span class="fighter-note-val" :style="{ color: noteColor(f.noteGlobale) }">{{ f.noteGlobale }}</span>
            </div>
            <div class="fighter-mini-stats">
              <div class="mini-stat" v-for="[label, val] in [['Frappe', f.compStriking],['Lutte', f.compLutte],['Grapl.', f.compGrappling]]" :key="label">
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
    <v-alert
      v-if="props.partie && props.partie.prestigeEcurie <= 2"
      type="info" variant="tonal" rounded="lg" class="mb-3" style="font-size:.82rem"
    >
      <strong>Recrutement local</strong> — Ton écurie est au niveau {{ props.partie.prestigeEcurie }}.
      Seuls les combattants du même pays que ton entraîneur et de niveau ≤ {{ props.partie.prestigeEcurie === 1 ? 55 : 65 }} sont disponibles.
      Remporte des victoires pour augmenter le prestige et accéder à des talents internationaux.
    </v-alert>

    <div class="cat-filter">
      <button v-for="cat in CATEGORIES" :key="cat" class="cat-filter-btn"
        :class="{ active: filterCat === cat }"
        :style="filterCat === cat && cat !== 'Toutes'
          ? { background: catColor(cat) + '22', color: catColor(cat), borderColor: catColor(cat) + '66' }
          : {}"
        @click="filterCat = cat">
        {{ cat }}
      </button>
    </div>

    <div v-if="filteredDisponibles.length === 0" class="empty-state">
      <span class="empty-icon">🔍</span>
      <p v-if="disponibles.length === 0">Tous les combattants sont déjà dans ton écurie !</p>
      <p v-else>Aucun combattant dans cette catégorie.</p>
    </div>

    <div class="recruit-list">
      <div v-for="f in filteredDisponibles" :key="f.combattantID" class="recruit-row">
        <span class="recruit-flag-cell">
          <span v-if="f.codePays" :class="getFlagClass(f.codePays)" />
        </span>

        <div class="recruit-info">
          <span class="recruit-name">{{ f.prenom }} {{ f.nom }}</span>
          <span class="recruit-sub">{{ f.nationalite }} · {{ f.age }} ans · {{ f.stylePrincipal }}</span>
        </div>

        <span class="recruit-cat"
          :style="{ color: catColor(f.categoriePoids), borderColor: catColor(f.categoriePoids) + '55', background: catColor(f.categoriePoids) + '18' }">
          {{ f.categoriePoids }}
        </span>

        <span class="recruit-note" :style="{ color: noteColor(f.noteGlobale) }">{{ f.noteGlobale }}</span>

        <span class="recruit-prix">💰 {{ formatPrix(f.prixAchat) }}</span>

        <div class="recruit-actions">
          <v-btn size="small" variant="text" rounded="pill" class="recruit-btn-info" @click="openDialog(f, 'recruit')">
            Infos
          </v-btn>
          <v-btn size="small" variant="outlined" rounded="pill" class="recruit-btn-recruit"
            :loading="recruiting === f.combattantID" @click="recruter(f.combattantID)">
            Recruter
          </v-btn>
        </div>
      </div>
    </div>
  </div>

  <!-- ── Dialog détail combattant ──────────────────────────────── -->
  <v-dialog v-model="dialog" max-width="680" scrollable>
    <v-card v-if="dialogFighter" class="fighter-dialog" rounded="xl" elevation="16">

      <div class="dialog-topbar">
        <span class="fighter-cat-badge"
          :style="{ background: catColor(dialogFighter.categoriePoids) + '22', color: catColor(dialogFighter.categoriePoids), borderColor: catColor(dialogFighter.categoriePoids) + '55' }">
          {{ dialogFighter.categoriePoids }}
        </span>
        <v-spacer />
        <v-btn icon="mdi-close" variant="text" size="small" @click="dialog = false" />
      </div>

      <div class="dialog-hero">
        <div class="dialog-hero-left">
          <img :src="fighterImg(dialogFighter.genre)" class="dialog-silhouette" alt="" />
          <div>
            <div class="dialog-name">{{ dialogFighter.prenom }} {{ dialogFighter.nom }}</div>
            <div class="dialog-nickname" v-if="dialogFighter.biographie">{{ dialogFighter.biographie }}</div>
          </div>
        </div>
        <div class="dialog-overall-badge"
          :style="{ color: noteColor(dialogFighter.noteGlobale), borderColor: noteColor(dialogFighter.noteGlobale) + '55', background: noteColor(dialogFighter.noteGlobale) + '14' }">
          <span class="dialog-overall-num">{{ dialogFighter.noteGlobale }}</span>
          <span class="dialog-overall-label">OVR</span>
        </div>
      </div>

      <v-card-text class="dialog-body">

        <div v-if="dialogFighter.semainesIndispo > 0" class="dialog-injury-alert">
          🏥 Blessé : {{ dialogFighter.blessureZone }}
          (gravité {{ dialogFighter.blessureGravite }}/3)
          — Indisponible {{ dialogFighter.semainesIndispo }} tour{{ dialogFighter.semainesIndispo > 1 ? 's' : '' }}
        </div>

        <div class="dialog-section-title">👤 Identité</div>
        <div class="dialog-identity-grid">
          <div class="dialog-id-item">
            <span class="dialog-id-label">🎂 Âge</span>
            <span class="dialog-id-val">{{ dialogFighter.age }} ans</span>
          </div>
          <div class="dialog-id-item">
            <span class="dialog-id-label">🌍 Nationalité</span>
            <span class="dialog-id-val">
              <span v-if="dialogFighter.codePays" :class="getFlagClass(dialogFighter.codePays)" class="flag-sm" />
              {{ dialogFighter.nationalite }}
            </span>
          </div>
          <div class="dialog-id-item">
            <span class="dialog-id-label">⚧ Genre</span>
            <span class="dialog-id-val">{{ dialogFighter.genre === 'F' ? 'Femme' : 'Homme' }}</span>
          </div>
          <div class="dialog-id-item">
            <span class="dialog-id-label">🥋 Style</span>
            <span class="dialog-id-val">{{ styleIcon(dialogFighter.stylePrincipal) }} {{ dialogFighter.stylePrincipal }}</span>
          </div>
          <div class="dialog-id-item">
            <span class="dialog-id-label">📏 Taille</span>
            <span class="dialog-id-val">{{ dialogFighter.tailleCm ? dialogFighter.tailleCm + ' cm' : '—' }}</span>
          </div>
          <div class="dialog-id-item">
            <span class="dialog-id-label">💪 Allonge</span>
            <span class="dialog-id-val">{{ dialogFighter.allongeCm ? dialogFighter.allongeCm + ' cm' : '—' }}</span>
          </div>
          <div class="dialog-id-item">
            <span class="dialog-id-label">⚖️ Poids réel</span>
            <span class="dialog-id-val">{{ dialogFighter.poidsReelKg ? dialogFighter.poidsReelKg + ' kg' : '—' }}</span>
          </div>
        </div>

        <div class="career-phase-section">
          <div class="career-phase-badge" :class="`phase-${dialogFighter.phaseCarriere?.toLowerCase()}`">
            {{ phaseIcon(dialogFighter.phaseCarriere) }} {{ dialogFighter.phaseCarriere }}
          </div>
          <div class="career-potential">
            Potentiel estimé : <span class="potential-value">{{ dialogFighter.potentiel }}</span> / 100
            <div class="potential-bar">
              <div class="potential-fill" :style="{ width: dialogFighter.potentiel + '%' }"></div>
            </div>
          </div>
          <span v-if="dialogFighter.meilleurRangOrga" class="rang-badge rang-badge-dialog">
            {{ dialogFighter.meilleurRangOrga }}
          </span>
        </div>

        <div v-if="dialogFighter.rivalites?.length > 0" class="rivalites-section">
          <span class="section-title">🔥 Rivalités</span>
          <div v-for="r in dialogFighter.rivalites" :key="r.rivaliteID" class="rivalite-card">
            <div class="rivalite-header">
              <span class="rivalite-adversaire">VS {{ r.adversaireNom }}</span>
              <span class="rivalite-intensite">{{ '🔥'.repeat(r.intensite) }}{{ '◾'.repeat(5 - r.intensite) }}</span>
            </div>
            <div class="rivalite-details">
              <span class="rivalite-confrontations">{{ r.nbConfrontations }} confrontation{{ r.nbConfrontations > 1 ? 's' : '' }}</span>
              <span v-if="r.raison" class="rivalite-raison">{{ r.raison }}</span>
            </div>
          </div>
        </div>

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

        <div class="dialog-section-title">📊 Statistiques</div>
        <div class="dialog-stats">
          <div v-for="[icon, label, val] in [
            ['🥊', 'Frappe debout',   dialogFighter.compStriking],
            ['🤼', 'Lutte',           dialogFighter.compLutte],
            ['⛩️',  'Grappling',       dialogFighter.compGrappling],
            ['🏋️', 'Conditionnement', dialogFighter.compConditioning],
            ['💪', 'Endurance',       dialogFighter.compStamina],
            ['🧠', 'Mental',          dialogFighter.compMental],
          ]" :key="label" class="dialog-stat-row">
            <span class="dialog-stat-icon">{{ icon }}</span>
            <span class="dialog-stat-label">{{ label }}</span>
            <div class="dialog-stat-track">
              <div class="dialog-stat-fill" :style="{ width: val + '%', background: statColor(val) }" />
            </div>
            <span class="dialog-stat-val" :style="{ color: statColor(val) }">{{ val }}</span>
          </div>
        </div>

        <div class="dialog-section-title">🔬 Stats détaillées</div>
        <div class="dialog-raw-stats">
          <div class="raw-group">
            <div class="raw-group-title">🥊 Striking</div>
            <div class="raw-stat-row" v-for="[lbl, v] in [['Frappe', dialogFighter.statFrappeDebout],['Puissance', dialogFighter.statPuissance],['Précision', dialogFighter.statPrecision]]" :key="lbl">
              <span class="raw-label">{{ lbl }}</span>
              <div class="raw-track"><div class="raw-fill" :style="{ width: (v??0)+'%', background: rawStatColor(v??0) }" /></div>
              <span class="raw-val" :style="{ color: rawStatColor(v??0) }">{{ v??'—' }}</span>
            </div>
          </div>
          <div class="raw-group">
            <div class="raw-group-title">🤼 Lutte</div>
            <div class="raw-stat-row" v-for="[lbl, v] in [['Wrestling', dialogFighter.statWrestling],['Takedown', dialogFighter.statTakedown],['Anti-TD', dialogFighter.statAntiTakedown]]" :key="lbl">
              <span class="raw-label">{{ lbl }}</span>
              <div class="raw-track"><div class="raw-fill" :style="{ width: (v??0)+'%', background: rawStatColor(v??0) }" /></div>
              <span class="raw-val" :style="{ color: rawStatColor(v??0) }">{{ v??'—' }}</span>
            </div>
          </div>
          <div class="raw-group">
            <div class="raw-group-title">⛩️ Grappling</div>
            <div class="raw-stat-row" v-for="[lbl, v] in [['Jiu-Jitsu', dialogFighter.statJiuJitsu],['Submission', dialogFighter.statSubmission],['Évasion Sub', dialogFighter.statEvasionSub]]" :key="lbl">
              <span class="raw-label">{{ lbl }}</span>
              <div class="raw-track"><div class="raw-fill" :style="{ width: (v??0)+'%', background: rawStatColor(v??0) }" /></div>
              <span class="raw-val" :style="{ color: rawStatColor(v??0) }">{{ v??'—' }}</span>
            </div>
          </div>
          <div class="raw-group">
            <div class="raw-group-title">🏋️ Physique</div>
            <div class="raw-stat-row" v-for="[lbl, v] in [['Force', dialogFighter.statForce],['Vitesse', dialogFighter.statVitesse],['Agilité', dialogFighter.statAgilite],['Cardio', dialogFighter.statCardio],['Récupération', dialogFighter.statRecuperation],['Menton', dialogFighter.statMentoniere]]" :key="lbl">
              <span class="raw-label">{{ lbl }}</span>
              <div class="raw-track"><div class="raw-fill" :style="{ width: (v??0)+'%', background: rawStatColor(v??0) }" /></div>
              <span class="raw-val" :style="{ color: rawStatColor(v??0) }">{{ v??'—' }}</span>
            </div>
          </div>
          <div class="raw-group">
            <div class="raw-group-title">🧠 Mental</div>
            <div class="raw-stat-row" v-for="[lbl, v] in [['Mental', dialogFighter.statMental],['Expérience', dialogFighter.statExperience],['Adaptation', dialogFighter.statAdaptation]]" :key="lbl">
              <span class="raw-label">{{ lbl }}</span>
              <div class="raw-track"><div class="raw-fill" :style="{ width: (v??0)+'%', background: rawStatColor(v??0) }" /></div>
              <span class="raw-val" :style="{ color: rawStatColor(v??0) }">{{ v??'—' }}</span>
            </div>
          </div>
        </div>

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

      <v-card-actions class="dialog-actions">
        <v-btn variant="text" rounded="pill" @click="dialog = false">Fermer</v-btn>
        <v-spacer />
        <v-btn v-if="dialogMode === 'recruit'" variant="flat" rounded="pill" color="red-darken-2"
          :loading="recruiting === dialogFighter.combattantID"
          @click="recruter(dialogFighter.combattantID)">
          👊 Recruter — {{ formatPrix(dialogFighter.prixAchat) }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>

  <v-snackbar v-model="snackbar" color="error" timeout="4000" location="top">
    {{ snackbarMsg }}
    <template #actions>
      <v-btn variant="text" @click="snackbar = false">Fermer</v-btn>
    </template>
  </v-snackbar>

</template>

<style scoped>
.flag-sm { font-size: .85rem; margin-right: 4px; vertical-align: middle; }

.injury-badge {
  font-size: .7rem; color: #ef4444;
  background: rgba(239,68,68,.1);
  padding: 2px 8px; border-radius: 8px;
  margin-left: 8px; vertical-align: middle;
}

.dialog-injury-alert {
  background: rgba(239,68,68,.12); color: #ef4444;
  padding: 10px 16px; border-radius: 10px;
  font-size: .85rem; margin-bottom: 12px;
  border-left: 3px solid #ef4444;
}

/* ── Sub-nav ─────────────────────────────────────────────────── */
.fighters-subnav { display: flex; gap: 8px; margin-bottom: 24px; }

.fighters-subnav-btn {
  display: flex; align-items: center; gap: 8px;
  padding: 8px 20px; border-radius: 999px;
  border: 1.5px solid rgba(220,38,38,.25);
  background: rgba(220,38,38,.06);
  color: #94a3b8; font-size: .9rem; cursor: pointer; transition: all .2s;
}

.fighters-subnav-btn.active {
  border-color: #dc2626;
  background: rgba(220,38,38,.15);
  color: #fca5a5;
}

.subnav-badge {
  background: #dc2626; color: #fff;
  font-size: .72rem; font-weight: 700;
  border-radius: 999px; padding: 1px 7px;
}

.subnav-badge.muted { background: rgba(220,38,38,.3); color: #fca5a5; }

/* ── Empty ───────────────────────────────────────────────────── */
.empty-state { text-align: center; padding: 60px 20px; color: #64748b; }
.empty-icon { font-size: 3rem; display: block; margin-bottom: 16px; }

/* ── Fighter card ────────────────────────────────────────────── */
.fighter-card {
  background: linear-gradient(135deg, #1a0810 0%, #1e293b 100%);
  border: 1px solid rgba(220,38,38,.2);
  transition: transform .18s, border-color .18s;
}
.fighter-card:hover { transform: translateY(-3px); border-color: rgba(220,38,38,.5); }

.fighter-card-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 8px; gap: 8px; }

.fighter-avatar {
  width: 52px; height: 64px;
  object-fit: contain; object-position: top;
  opacity: .75;
  filter: drop-shadow(0 2px 6px rgba(0,0,0,.5));
}

.fighter-cat-badge {
  font-size: .72rem; font-weight: 700;
  border-radius: 999px; border: 1px solid;
  padding: 2px 10px; letter-spacing: .03em;
}

.fighter-name { font-size: 1.05rem; font-weight: 700; color: #e2e8f0; margin-bottom: 2px; }
.fighter-meta { font-size: .76rem; color: #64748b; }

.fighter-note-row { display: flex; justify-content: space-between; align-items: center; margin-bottom: 10px; }
.fighter-note-label { font-size: .78rem; color: #64748b; }
.fighter-note-val { font-size: 1.1rem; font-weight: 800; }

.fighter-mini-stats { display: flex; flex-direction: column; gap: 4px; }
.mini-stat { display: grid; grid-template-columns: 48px 1fr 28px; align-items: center; gap: 6px; }
.mini-stat-label { font-size: .72rem; color: #64748b; }
.mini-stat-track { height: 4px; background: rgba(255,255,255,.08); border-radius: 4px; overflow: hidden; }
.mini-stat-fill { height: 100%; border-radius: 4px; transition: width .4s; }
.mini-stat-val { font-size: .72rem; font-weight: 700; text-align: right; }

/* ── Cat filter ──────────────────────────────────────────────── */
.cat-filter { display: flex; flex-wrap: wrap; gap: 8px; margin-bottom: 20px; }

.cat-filter-btn {
  padding: 4px 14px; border-radius: 999px;
  border: 1.5px solid rgba(220,38,38,.2);
  background: transparent; color: #64748b; font-size: .82rem; cursor: pointer; transition: all .18s;
}

.cat-filter-btn.active {
  border-color: #dc2626; background: rgba(220,38,38,.12); color: #fca5a5;
}

/* ── Recruit list ────────────────────────────────────────────── */
.recruit-list { display: flex; flex-direction: column; gap: 8px; }

.recruit-row {
  display: grid;
  grid-template-columns: 30px 1fr auto auto auto auto;
  align-items: center;
  gap: 12px; padding: 12px 16px;
  background: rgba(20,10,20,.5);
  border: 1px solid rgba(220,38,38,.12);
  border-radius: 14px; transition: border-color .18s;
}

.recruit-row:hover { border-color: rgba(220,38,38,.35); }

.recruit-flag-cell { font-size: 1.1rem; text-align: center; }
.recruit-info { display: flex; flex-direction: column; min-width: 0; }
.recruit-name { font-size: .92rem; font-weight: 700; color: #e2e8f0; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.recruit-sub { font-size: .75rem; color: #64748b; }

.recruit-cat {
  font-size: .72rem; font-weight: 700;
  border-radius: 999px; border: 1px solid; padding: 2px 10px; white-space: nowrap;
}

.recruit-note { font-size: 1rem; font-weight: 800; min-width: 28px; text-align: center; }
.recruit-prix { font-size: .78rem; font-weight: 700; color: #f59e0b; white-space: nowrap; }
.recruit-actions { display: flex; gap: 6px; align-items: center; }
.recruit-btn-info { color: #94a3b8 !important; font-size: .8rem !important; }
.recruit-btn-recruit { border-color: rgba(220,38,38,.5) !important; color: #fca5a5 !important; font-size: .8rem !important; }

/* ── Dialog ──────────────────────────────────────────────────── */
.fighter-dialog {
  background: linear-gradient(160deg, #1a0810 0%, #1e293b 100%);
  border: 1px solid rgba(220,38,38,.25);
}

.dialog-topbar { display: flex; align-items: center; padding: 14px 16px 8px; gap: 10px; }

.dialog-hero {
  display: flex; align-items: center; justify-content: space-between;
  padding: 4px 20px 18px; gap: 16px;
}

.dialog-hero-left { display: flex; align-items: center; gap: 14px; }

.dialog-silhouette {
  width: 70px; height: 85px;
  object-fit: contain; object-position: top;
  opacity: .8;
  filter: drop-shadow(0 4px 8px rgba(0,0,0,.6));
}

.dialog-name { font-size: 1.25rem; font-weight: 800; color: #e2e8f0; }
.dialog-nickname { font-size: .82rem; color: #64748b; margin-top: 3px; font-style: italic; }

.dialog-overall-badge {
  display: flex; flex-direction: column; align-items: center;
  border: 2px solid; border-radius: 14px; padding: 8px 18px; min-width: 72px;
}
.dialog-overall-num { font-size: 2rem; font-weight: 900; line-height: 1; }
.dialog-overall-label { font-size: .65rem; font-weight: 700; letter-spacing: .1em; opacity: .7; margin-top: 2px; }

.dialog-body { padding: 0 20px 8px; }

.dialog-section-title {
  font-size: .7rem; font-weight: 700;
  text-transform: uppercase; letter-spacing: .08em;
  color: #475569; margin: 18px 0 10px;
}

.dialog-identity-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 8px; }

.dialog-id-item {
  display: flex; flex-direction: column; gap: 2px;
  padding: 8px 12px;
  background: rgba(255,255,255,.04);
  border-radius: 10px; border: 1px solid rgba(255,255,255,.06);
}

.dialog-id-label { font-size: .68rem; color: #475569; }
.dialog-id-val { font-size: .88rem; font-weight: 600; color: #e2e8f0; }

.dialog-record-block {
  display: flex; align-items: center; justify-content: center;
  gap: 12px; padding: 16px 12px;
  background: rgba(255,255,255,.03);
  border-radius: 14px; border: 1px solid rgba(255,255,255,.06);
}

.dialog-record-col { display: flex; flex-direction: column; align-items: center; gap: 3px; flex: 1; }
.record-big { font-size: 2.2rem; font-weight: 900; line-height: 1; }
.record-label { font-size: .72rem; color: #64748b; font-weight: 600; }
.record-breakdown { display: flex; gap: 6px; margin-top: 4px; flex-wrap: wrap; justify-content: center; }
.record-breakdown span { font-size: .65rem; color: #475569; background: rgba(255,255,255,.06); border-radius: 6px; padding: 1px 6px; }
.record-win  .record-big { color: #22c55e; }
.record-loss .record-big { color: #ef4444; }
.record-draw .record-big { color: #94a3b8; }
.record-separator { font-size: 1.4rem; color: #1e293b; font-weight: 900; flex: none; }

.dialog-stats { display: flex; flex-direction: column; gap: 7px; }
.dialog-stat-row { display: grid; grid-template-columns: 22px 130px 1fr 32px; align-items: center; gap: 8px; }
.dialog-stat-icon { font-size: .95rem; text-align: center; }
.dialog-stat-label { font-size: .82rem; color: #94a3b8; }
.dialog-stat-track { height: 6px; background: rgba(255,255,255,.07); border-radius: 6px; overflow: hidden; }
.dialog-stat-fill { height: 100%; border-radius: 6px; transition: width .5s ease; }
.dialog-stat-val { font-size: .82rem; font-weight: 700; text-align: right; }

.dialog-raw-stats { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; }

.raw-group {
  background: rgba(255,255,255,.03); border-radius: 10px;
  border: 1px solid rgba(255,255,255,.05); padding: 10px 12px;
}

.raw-group-title {
  font-size: .7rem; font-weight: 700; color: #dc2626;
  margin-bottom: 8px; text-transform: uppercase; letter-spacing: .06em;
}

.raw-stat-row { display: grid; grid-template-columns: 80px 1fr 26px; align-items: center; gap: 6px; margin-bottom: 5px; }
.raw-label { font-size: .72rem; color: #64748b; }
.raw-track { height: 4px; background: rgba(255,255,255,.06); border-radius: 4px; overflow: hidden; }
.raw-fill { height: 100%; border-radius: 4px; }
.raw-val { font-size: .72rem; font-weight: 700; text-align: right; }

.dialog-cost-block {
  margin-top: 4px; padding: 12px 14px; border-radius: 12px;
  background: rgba(245,158,11,.06); border: 1px solid rgba(245,158,11,.2);
  display: flex; flex-direction: column; gap: 6px;
}

.dialog-cost-row { display: flex; justify-content: space-between; align-items: center; }
.dialog-cost-label { font-size: .8rem; color: #94a3b8; }
.dialog-cost-val { font-size: .92rem; font-weight: 700; color: #f59e0b; }
.dialog-cost-sal { font-size: .85rem; font-weight: 600; color: #94a3b8; }
.dialog-actions { padding: 12px 20px 16px; }

/* ── Phase de carrière ───────────────────────────────────── */
.phase-mini {
  font-size: .8rem;
  display: inline-block;
  margin: 0 2px;
}

.career-phase-section {
  display: flex;
  align-items: center;
  gap: 16px;
  margin-bottom: 4px;
}

.career-phase-badge {
  font-size: .8rem;
  font-weight: 700;
  padding: 4px 12px;
  border-radius: 6px;
  white-space: nowrap;
}

.phase-développement { background: rgba(34,197,94,.12);  color: #22c55e; }
.phase-pic           { background: rgba(245,158,11,.12); color: #f59e0b; }
.phase-déclin        { background: rgba(239,68,68,.12);  color: #ef4444; }
.phase-vétéran       { background: rgba(148,163,184,.12); color: #94a3b8; }

.career-potential { flex: 1; font-size: .78rem; color: #64748b; }
.potential-value  { font-weight: 700; color: #f59e0b; }

.potential-bar {
  height: 6px;
  background: rgba(255,255,255,.05);
  border-radius: 3px;
  margin-top: 4px;
  overflow: hidden;
}
.potential-fill {
  height: 100%;
  background: linear-gradient(90deg, #f59e0b, #22c55e);
  border-radius: 3px;
  transition: width .4s ease;
}

.rivalites-section { margin-top: 16px; }
.section-title { font-size: .75rem; font-weight: 700; color: #f59e0b; letter-spacing: .05em; text-transform: uppercase; display: block; margin-bottom: 8px; }
.rivalite-card { background: rgba(245,158,11,.06); border: 1px solid rgba(245,158,11,.15); border-radius: 8px; padding: 10px 14px; margin-bottom: 6px; }
.rivalite-header { display: flex; justify-content: space-between; align-items: center; }
.rivalite-adversaire { font-weight: 700; font-size: .85rem; color: #e2e8f0; }
.rivalite-intensite { font-size: .9rem; }
.rivalite-details { display: flex; gap: 12px; margin-top: 4px; }
.rivalite-confrontations { font-size: .75rem; color: #94a3b8; }
.rivalite-raison { font-size: .75rem; color: #f59e0b; font-style: italic; }

.rang-badge {
  font-size: .7rem;
  padding: 2px 8px;
  border-radius: 4px;
  background: rgba(245,158,11,.1);
  color: #f59e0b;
  font-weight: 600;
  margin-left: 6px;
}
.rang-badge-dialog {
  font-size: .78rem;
  padding: 3px 10px;
  margin-left: 0;
  white-space: nowrap;
}
</style>
