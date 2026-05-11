<script setup>
import { ref, watch, onMounted } from 'vue'

const props = defineProps({
  authHeaders: { type: Function, required: true },
  partie:      { type: Object, required: true }
})

const API = 'http://localhost:5219/api'

const mode         = ref('orga')
const loading      = ref(false)
const classement   = ref([])
const orgTitle     = ref('')

const organisations = ref([])
const categories    = ref([])

const selectedOrg   = ref(null)
const selectedGenre = ref('H')
const selectedCat   = ref(null)

async function chargerOrganisations() {
  const res = await fetch(`${API}/rankings/organisations`, { headers: props.authHeaders() })
  if (res.ok) {
    organisations.value = await res.json()
    if (organisations.value.length) selectedOrg.value = organisations.value[0].organisationID
  }
}

async function chargerCategories() {
  let url, res
  if (mode.value === 'orga' && selectedOrg.value) {
    url = `${API}/rankings/organisations/${selectedOrg.value}/categories`
    res = await fetch(url, { headers: props.authHeaders() })
    if (res.ok) {
      const all = await res.json()
      categories.value = all
        .filter(c => c.genre === selectedGenre.value)
        .map(c => ({ categorieID: c.categorieID, nom: c.nom }))
    }
  } else {
    url = `${API}/rankings/categories?genre=${selectedGenre.value}`
    res = await fetch(url, { headers: props.authHeaders() })
    if (res.ok) categories.value = await res.json()
  }
  if (categories.value.length) {
    const lw = categories.value.find(c => c.categorieID === 5)
    selectedCat.value = lw?.categorieID ?? categories.value[0]?.categorieID ?? null
  }
}

async function chargerClassement() {
  if (!selectedCat.value && mode.value !== 'p4p') return
  loading.value = true
  classement.value = []
  try {
    let url
    if (mode.value === 'orga') {
      if (!selectedOrg.value) return
      url = `${API}/rankings/orga/${selectedOrg.value}?genre=${selectedGenre.value}&categorieId=${selectedCat.value}`
      const res = await fetch(url, { headers: props.authHeaders() })
      if (res.ok) {
        const data = await res.json()
        classement.value = data.classement ?? []
        orgTitle.value = data.organisationNom
      }
    } else if (mode.value === 'mondial') {
      url = `${API}/rankings/mondial?genre=${selectedGenre.value}&categorieId=${selectedCat.value}`
      const res = await fetch(url, { headers: props.authHeaders() })
      if (res.ok) classement.value = await res.json()
    } else {
      const res = await fetch(`${API}/rankings/p4p`, { headers: props.authHeaders() })
      if (res.ok) classement.value = await res.json()
    }
  } finally {
    loading.value = false
  }
}

watch(mode, async () => {
  if (mode.value !== 'p4p') await chargerCategories()
  await chargerClassement()
})

watch(selectedOrg, async () => {
  if (mode.value === 'orga') {
    await chargerCategories()
    await chargerClassement()
  }
})

watch(selectedGenre, async () => {
  await chargerCategories()
  await chargerClassement()
})

onMounted(async () => {
  await chargerOrganisations()
  await chargerCategories()
  await chargerClassement()
})
</script>

<template>
  <div class="rankings-page">
    <h2 class="page-title">🏆 Classements</h2>

    <div class="rankings-filters">
      <div class="filter-tabs">
        <button class="filter-tab" :class="{ active: mode === 'orga' }" @click="mode = 'orga'">Par organisation</button>
        <button class="filter-tab" :class="{ active: mode === 'mondial' }" @click="mode = 'mondial'">Mondial</button>
        <button class="filter-tab" :class="{ active: mode === 'p4p' }" @click="mode = 'p4p'">Pound-for-Pound</button>
      </div>

      <div v-if="mode !== 'p4p'" class="filter-row">
        <select v-if="mode === 'orga'" v-model="selectedOrg" class="filter-select" @change="chargerClassement">
          <option v-for="o in organisations" :key="o.organisationID" :value="o.organisationID">
            {{ o.nom }} {{ '⭐'.repeat(o.prestige) }}
          </option>
        </select>
        <select v-model="selectedGenre" class="filter-select">
          <option value="H">Hommes</option>
          <option value="F">Femmes</option>
        </select>
        <select v-if="categories.length" v-model="selectedCat" class="filter-select" @change="chargerClassement">
          <option v-for="cat in categories" :key="cat.categorieID" :value="cat.categorieID">{{ cat.nom }}</option>
        </select>
      </div>
    </div>

    <div class="rankings-table">
      <div v-if="loading" class="rankings-loading">
        <v-progress-circular indeterminate color="red" size="24" />
      </div>
      <div v-else-if="classement.length === 0" class="rankings-empty">
        Aucun combattant classé pour le moment.
        <br><span class="rankings-empty-hint">Les classements se remplissent après les premiers combats.</span>
      </div>
      <template v-else>
        <div
          v-for="entry in classement"
          :key="entry.combattantID"
          class="ranking-row"
          :class="{ 'is-champion': entry.estChampion }"
        >
          <span class="rank-badge" :class="{ 'rank-champ': entry.estChampion }">
            {{ entry.estChampion ? '🏆' : '#' + entry.rang }}
          </span>
          <span class="rank-name">{{ entry.nomComplet }}</span>
          <span class="rank-overall">{{ entry.overall }}</span>
          <span class="rank-record">{{ entry.victoires }}-{{ entry.defaites }}-{{ entry.nuls }}</span>
          <span v-if="entry.estChampion && entry.nbDefenses > 0" class="rank-defenses">
            {{ entry.nbDefenses }} défense{{ entry.nbDefenses > 1 ? 's' : '' }}
          </span>
        </div>
      </template>
    </div>
  </div>
</template>

<style scoped>
.rankings-page { padding: 0; }
.page-title { font-size: 1.3rem; font-weight: 700; margin-bottom: 16px; color: #e2e8f0; }

.rankings-filters { margin-bottom: 16px; }
.filter-tabs { display: flex; gap: 4px; margin-bottom: 12px; flex-wrap: wrap; }
.filter-tab {
  padding: 8px 16px;
  border-radius: 6px;
  border: 1px solid rgba(255,255,255,.06);
  background: rgba(255,255,255,.03);
  color: #94a3b8;
  font-size: .8rem;
  font-weight: 600;
  cursor: pointer;
  transition: all .15s;
}
.filter-tab.active { background: rgba(220,38,38,.12); color: #dc2626; border-color: #dc2626; }
.filter-tab:hover:not(.active) { background: rgba(255,255,255,.06); color: #e2e8f0; }

.filter-row { display: flex; gap: 8px; flex-wrap: wrap; }
.filter-select {
  padding: 8px 12px;
  border-radius: 6px;
  border: 1px solid rgba(255,255,255,.08);
  background: rgba(255,255,255,.03);
  color: #e2e8f0;
  font-size: .8rem;
  cursor: pointer;
}

.rankings-table { margin-top: 8px; }
.rankings-loading { display: flex; justify-content: center; padding: 32px; }

.ranking-row {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 10px 16px;
  border-bottom: 1px solid rgba(255,255,255,.04);
  border-radius: 6px;
  transition: background .12s;
}
.ranking-row:hover { background: rgba(255,255,255,.02); }
.ranking-row.is-champion {
  background: rgba(245,158,11,.06);
  border: 1px solid rgba(245,158,11,.15);
  border-radius: 8px;
  margin-bottom: 8px;
}

.rank-badge { width: 44px; text-align: center; font-weight: 700; font-size: .85rem; color: #94a3b8; flex-shrink: 0; }
.rank-champ { color: #f59e0b; font-size: 1.1rem; }
.rank-name { flex: 1; font-weight: 600; font-size: .9rem; color: #e2e8f0; }
.rank-overall { font-weight: 700; font-size: .85rem; color: #f59e0b; width: 36px; text-align: right; }
.rank-record { font-size: .8rem; color: #94a3b8; width: 70px; text-align: right; }
.rank-defenses { font-size: .7rem; color: #22c55e; }

.rankings-empty {
  text-align: center;
  color: #475569;
  padding: 48px 16px;
  font-size: .9rem;
}
.rankings-empty-hint { font-size: .8rem; color: #334155; }
</style>
