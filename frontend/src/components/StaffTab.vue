<script setup>
import { ref, computed, onMounted } from 'vue'

const props = defineProps({
  authHeaders: { type: Function, required: true },
})

const API      = 'http://localhost:5219/api'
const loading  = ref(true)
const staff    = ref([])   // StaffDisponibleDto[] with estEmbauche
const sousOnglet = ref('mon-staff')  // 'mon-staff' | 'recruter'
const snackbar = ref(false)
const snackbarMsg = ref('')
const snackbarColor = ref('error')

const ROLE_LABELS = {
  CoachStriking:        { label: 'Coach Striking',         icon: '🥊', color: '#ef4444' },
  CoachLutte:           { label: 'Coach Lutte',             icon: '🤼', color: '#f97316' },
  CoachGrappling:       { label: 'Coach Grappling',         icon: '⛩️', color: '#6366f1' },
  CoachConditioning:    { label: 'Coach Conditionnement',   icon: '🏋️', color: '#22c55e' },
  CoachMental:          { label: 'Coach Mental',            icon: '🧠', color: '#a78bfa' },
  PreparateurPhysique:  { label: 'Préparateur Physique',    icon: '💪', color: '#f59e0b' },
  Agent:                { label: 'Agent',                   icon: '🤝', color: '#06b6d4' },
}

const embauches = computed(() => staff.value.filter(s => s.estEmbauche))
const disponibles = computed(() => staff.value.filter(s => !s.estEmbauche))

function roleInfo(role) {
  return ROLE_LABELS[role] ?? { label: role, icon: '👥', color: '#64748b' }
}

function fmt(val) {
  return Number(val ?? 0).toLocaleString('fr-FR', { minimumFractionDigits: 0, maximumFractionDigits: 0 })
}

function statColor(val) {
  if (val >= 75) return '#22c55e'
  if (val >= 55) return '#f59e0b'
  if (val >= 40) return '#f97316'
  return '#ef4444'
}

async function charger() {
  loading.value = true
  try {
    const res = await fetch(`${API}/staff`, { headers: props.authHeaders() })
    if (res.ok) staff.value = await res.json()
  } finally {
    loading.value = false
  }
}

async function embaucher(s) {
  const res = await fetch(`${API}/staff/${s.staffDisponibleID}/embaucher`, {
    method: 'POST',
    headers: props.authHeaders(),
  })
  if (res.ok) {
    snackbarMsg.value   = `${s.prenom} ${s.nomFamille} rejoint ton staff !`
    snackbarColor.value = 'success'
    snackbar.value      = true
    await charger()
  } else {
    const msg = await res.text().catch(() => 'Erreur inconnue')
    snackbarMsg.value   = msg || 'Erreur lors du recrutement.'
    snackbarColor.value = 'error'
    snackbar.value      = true
  }
}

async function licencier(s) {
  if (!s.staffPartieID) return
  const res = await fetch(`${API}/staff/embauches/${s.staffPartieID}`, {
    method: 'DELETE',
    headers: props.authHeaders(),
  })
  if (res.ok) {
    snackbarMsg.value   = `${s.prenom} ${s.nomFamille} a quitté le staff.`
    snackbarColor.value = 'info'
    snackbar.value      = true
    await charger()
  }
}

onMounted(charger)
</script>

<template>
  <div class="staff-tab">

    <div class="staff-header">
      <div>
        <span class="staff-title">Staff</span>
        <span class="staff-sub">Gérez vos entraîneurs et préparateurs</span>
      </div>
      <div class="staff-count-badge">
        {{ embauches.length }} membre{{ embauches.length > 1 ? 's' : '' }} · +{{ embauches.length * 2 }} slots entraînement/tour
      </div>
    </div>

    <!-- Sous-onglets -->
    <div class="sub-tabs">
      <button
        class="sub-tab"
        :class="{ active: sousOnglet === 'mon-staff' }"
        @click="sousOnglet = 'mon-staff'"
      >
        👥 Mon Staff
        <span v-if="embauches.length" class="sub-tab-badge">{{ embauches.length }}</span>
      </button>
      <button
        class="sub-tab"
        :class="{ active: sousOnglet === 'recruter' }"
        @click="sousOnglet = 'recruter'"
      >
        ➕ Recruter
        <span class="sub-tab-badge">{{ disponibles.length }}</span>
      </button>
    </div>

    <div v-if="loading" class="text-center py-12">
      <v-progress-circular indeterminate color="indigo" size="36" />
    </div>

    <!-- Mon Staff -->
    <div v-else-if="sousOnglet === 'mon-staff'">
      <div v-if="embauches.length === 0" class="empty-state">
        <span style="font-size:2.5rem">👥</span>
        <p>Aucun membre du staff recruté.</p>
        <p style="font-size:.82rem;color:#475569;margin-top:4px">
          Recrute des spécialistes pour augmenter la capacité et l'efficacité des entraînements.
        </p>
        <button class="btn-switch" @click="sousOnglet = 'recruter'">Voir les candidats disponibles</button>
      </div>
      <div v-else class="staff-grid">
        <div v-for="s in embauches" :key="s.staffDisponibleID" class="staff-card">
          <div class="staff-card-header" :style="{ borderColor: roleInfo(s.role).color + '55' }">
            <span class="staff-card-icon">{{ s.icone ?? roleInfo(s.role).icon }}</span>
            <div>
              <div class="staff-card-name">{{ s.prenom }} {{ s.nomFamille }}</div>
              <div class="staff-card-role" :style="{ color: roleInfo(s.role).color }">
                {{ roleInfo(s.role).label }}
              </div>
            </div>
            <div class="staff-card-overall">{{ s.overall }}</div>
          </div>
          <div class="staff-card-body">
            <div v-if="s.nationalite" class="staff-nat">🌍 {{ s.nationalite }}</div>
            <div class="staff-stats">
              <div class="stat-mini" title="Striking">
                <span class="stat-mini-label">🥊</span>
                <div class="stat-mini-bar">
                  <div :style="{ width: s.compStriking + '%', background: statColor(s.compStriking) }" />
                </div>
                <span class="stat-mini-val" :style="{ color: statColor(s.compStriking) }">{{ s.compStriking }}</span>
              </div>
              <div class="stat-mini" title="Lutte">
                <span class="stat-mini-label">🤼</span>
                <div class="stat-mini-bar">
                  <div :style="{ width: s.compLutte + '%', background: statColor(s.compLutte) }" />
                </div>
                <span class="stat-mini-val" :style="{ color: statColor(s.compLutte) }">{{ s.compLutte }}</span>
              </div>
              <div class="stat-mini" title="Grappling">
                <span class="stat-mini-label">⛩️</span>
                <div class="stat-mini-bar">
                  <div :style="{ width: s.compGrappling + '%', background: statColor(s.compGrappling) }" />
                </div>
                <span class="stat-mini-val" :style="{ color: statColor(s.compGrappling) }">{{ s.compGrappling }}</span>
              </div>
              <div class="stat-mini" title="Conditionnement">
                <span class="stat-mini-label">🏋️</span>
                <div class="stat-mini-bar">
                  <div :style="{ width: s.compConditioning + '%', background: statColor(s.compConditioning) }" />
                </div>
                <span class="stat-mini-val" :style="{ color: statColor(s.compConditioning) }">{{ s.compConditioning }}</span>
              </div>
              <div class="stat-mini" title="Mental">
                <span class="stat-mini-label">🧠</span>
                <div class="stat-mini-bar">
                  <div :style="{ width: s.compMental + '%', background: statColor(s.compMental) }" />
                </div>
                <span class="stat-mini-val" :style="{ color: statColor(s.compMental) }">{{ s.compMental }}</span>
              </div>
            </div>
            <div class="staff-card-footer">
              <span class="staff-salary">{{ fmt(s.salaireMensuel) }} €/mois</span>
              <button class="btn-licencier" @click="licencier(s)">Licencier</button>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Recruter -->
    <div v-else-if="sousOnglet === 'recruter'">
      <div v-if="disponibles.length === 0" class="empty-state">
        <span style="font-size:2rem">✅</span>
        <p>Tout le staff disponible est déjà dans ton équipe.</p>
      </div>
      <div v-else class="staff-grid">
        <div v-for="s in disponibles" :key="s.staffDisponibleID" class="staff-card recruit-card">
          <div class="staff-card-header" :style="{ borderColor: roleInfo(s.role).color + '55' }">
            <span class="staff-card-icon">{{ s.icone ?? roleInfo(s.role).icon }}</span>
            <div>
              <div class="staff-card-name">{{ s.prenom }} {{ s.nomFamille }}</div>
              <div class="staff-card-role" :style="{ color: roleInfo(s.role).color }">
                {{ roleInfo(s.role).label }}
              </div>
            </div>
            <div class="staff-card-overall">{{ s.overall }}</div>
          </div>
          <div class="staff-card-body">
            <div v-if="s.description" class="staff-desc">{{ s.description }}</div>
            <div v-if="s.nationalite" class="staff-nat">🌍 {{ s.nationalite }}</div>
            <div class="staff-stats">
              <div class="stat-mini" title="Striking">
                <span class="stat-mini-label">🥊</span>
                <div class="stat-mini-bar">
                  <div :style="{ width: s.compStriking + '%', background: statColor(s.compStriking) }" />
                </div>
                <span class="stat-mini-val" :style="{ color: statColor(s.compStriking) }">{{ s.compStriking }}</span>
              </div>
              <div class="stat-mini" title="Lutte">
                <span class="stat-mini-label">🤼</span>
                <div class="stat-mini-bar">
                  <div :style="{ width: s.compLutte + '%', background: statColor(s.compLutte) }" />
                </div>
                <span class="stat-mini-val" :style="{ color: statColor(s.compLutte) }">{{ s.compLutte }}</span>
              </div>
              <div class="stat-mini" title="Grappling">
                <span class="stat-mini-label">⛩️</span>
                <div class="stat-mini-bar">
                  <div :style="{ width: s.compGrappling + '%', background: statColor(s.compGrappling) }" />
                </div>
                <span class="stat-mini-val" :style="{ color: statColor(s.compGrappling) }">{{ s.compGrappling }}</span>
              </div>
              <div class="stat-mini" title="Conditionnement">
                <span class="stat-mini-label">🏋️</span>
                <div class="stat-mini-bar">
                  <div :style="{ width: s.compConditioning + '%', background: statColor(s.compConditioning) }" />
                </div>
                <span class="stat-mini-val" :style="{ color: statColor(s.compConditioning) }">{{ s.compConditioning }}</span>
              </div>
              <div class="stat-mini" title="Mental">
                <span class="stat-mini-label">🧠</span>
                <div class="stat-mini-bar">
                  <div :style="{ width: s.compMental + '%', background: statColor(s.compMental) }" />
                </div>
                <span class="stat-mini-val" :style="{ color: statColor(s.compMental) }">{{ s.compMental }}</span>
              </div>
            </div>
            <div class="staff-card-footer">
              <span class="staff-salary">{{ fmt(s.salaireMensuel) }} €/mois</span>
              <button class="btn-embaucher" @click="embaucher(s)">
                ➕ Embaucher
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>

    <v-snackbar v-model="snackbar" :color="snackbarColor" timeout="3000" location="bottom right">
      {{ snackbarMsg }}
    </v-snackbar>

  </div>
</template>

<style scoped>
.staff-tab { max-width: 900px; margin: 0 auto; }

.staff-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 8px;
  margin-bottom: 20px;
  padding: 16px 20px;
  background: rgba(99,102,241,.08);
  border: 1px solid rgba(99,102,241,.2);
  border-radius: 16px;
}
.staff-title { display: block; font-size: 1.15rem; font-weight: 700; color: #e2e8f0; }
.staff-sub   { display: block; font-size: .82rem; color: #94a3b8; margin-top: 2px; }
.staff-count-badge {
  font-size: .78rem;
  padding: 4px 12px;
  border-radius: 20px;
  background: rgba(99,102,241,.15);
  color: #a5b4fc;
  align-self: center;
}

/* Sub-tabs */
.sub-tabs {
  display: flex;
  gap: 8px;
  margin-bottom: 20px;
}
.sub-tab {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 8px 18px;
  border-radius: 20px;
  border: 1px solid rgba(255,255,255,.1);
  background: rgba(255,255,255,.04);
  color: #64748b;
  font-size: .85rem;
  cursor: pointer;
  transition: all .18s;
}
.sub-tab:hover { background: rgba(99,102,241,.1); color: #a5b4fc; border-color: rgba(99,102,241,.3); }
.sub-tab.active { background: rgba(99,102,241,.2); color: #a5b4fc; border-color: #6366f1; font-weight: 600; }
.sub-tab-badge {
  font-size: .72rem;
  padding: 1px 7px;
  border-radius: 10px;
  background: rgba(99,102,241,.25);
  color: #a5b4fc;
}

/* Staff grid */
.staff-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(270px, 1fr));
  gap: 14px;
}

/* Staff card */
.staff-card {
  border-radius: 14px;
  border: 1px solid rgba(255,255,255,.07);
  background: rgba(255,255,255,.03);
  overflow: hidden;
  transition: border-color .18s;
}
.staff-card:hover { border-color: rgba(99,102,241,.3); }
.staff-card-header {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 12px 14px;
  background: rgba(0,0,0,.1);
  border-bottom: 1px solid rgba(255,255,255,.05);
  border-left: 3px solid;
}
.staff-card-icon  { font-size: 1.6rem; }
.staff-card-name  { font-size: .9rem; font-weight: 700; color: #e2e8f0; }
.staff-card-role  { font-size: .75rem; font-weight: 600; margin-top: 1px; }
.staff-card-overall {
  margin-left: auto;
  font-size: 1.1rem;
  font-weight: 900;
  color: #a5b4fc;
  min-width: 28px;
  text-align: right;
}
.staff-card-body { padding: 12px 14px; display: flex; flex-direction: column; gap: 8px; }
.staff-desc { font-size: .78rem; color: #64748b; line-height: 1.5; }
.staff-nat  { font-size: .75rem; color: #475569; }

/* Mini stat bars */
.staff-stats { display: flex; flex-direction: column; gap: 4px; }
.stat-mini {
  display: flex;
  align-items: center;
  gap: 6px;
}
.stat-mini-label { font-size: .8rem; width: 20px; text-align: center; flex-shrink: 0; }
.stat-mini-bar {
  flex: 1;
  height: 5px;
  border-radius: 4px;
  background: rgba(255,255,255,.08);
  overflow: hidden;
}
.stat-mini-bar > div {
  height: 100%;
  border-radius: 4px;
  transition: width .3s;
}
.stat-mini-val { font-size: .72rem; font-weight: 700; width: 24px; text-align: right; flex-shrink: 0; }

/* Card footer */
.staff-card-footer {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding-top: 4px;
  border-top: 1px solid rgba(255,255,255,.05);
  margin-top: 2px;
}
.staff-salary { font-size: .78rem; color: #64748b; }
.btn-licencier {
  padding: 5px 14px;
  border-radius: 16px;
  border: 1px solid rgba(239,68,68,.3);
  background: rgba(239,68,68,.08);
  color: #f87171;
  font-size: .78rem;
  cursor: pointer;
  transition: all .18s;
}
.btn-licencier:hover { background: rgba(239,68,68,.2); }
.btn-embaucher {
  padding: 5px 14px;
  border-radius: 16px;
  border: none;
  background: linear-gradient(135deg, #6366f1, #818cf8);
  color: white;
  font-size: .78rem;
  font-weight: 700;
  cursor: pointer;
  transition: all .18s;
}
.btn-embaucher:hover { transform: translateY(-1px); box-shadow: 0 3px 12px rgba(99,102,241,.4); }

/* Empty state */
.empty-state { text-align: center; padding: 48px 24px; color: #475569; }
.empty-state p { margin-top: 8px; }
.btn-switch {
  margin-top: 16px;
  padding: 8px 22px;
  border-radius: 20px;
  border: 1px solid rgba(99,102,241,.4);
  background: transparent;
  color: #6366f1;
  font-size: .85rem;
  cursor: pointer;
  transition: all .18s;
}
.btn-switch:hover { background: rgba(99,102,241,.1); }
</style>
