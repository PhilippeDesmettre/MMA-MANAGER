<script setup>
import { ref, onMounted } from 'vue'

const props = defineProps({
  authHeaders: { type: Function, required: true },
})

const API      = 'http://localhost:5219/api'
const finances = ref(null)
const loading  = ref(true)

async function charger() {
  loading.value = true
  try {
    const res = await fetch(`${API}/partie/finances`, { headers: props.authHeaders() })
    if (res.ok) finances.value = await res.json()
  } finally {
    loading.value = false
  }
}

function fmt(val) {
  return Number(val ?? 0).toLocaleString('fr-FR', { minimumFractionDigits: 0, maximumFractionDigits: 0 })
}

function soldeColor(val) {
  if (val > 10000) return '#22c55e'
  if (val > 0)     return '#f59e0b'
  return '#ef4444'
}

onMounted(charger)
</script>

<template>
  <div class="finances-tab">

    <div class="finances-header">
      <span class="finances-title">Finances</span>
      <span class="finances-sub">Résumé des dépenses et projection du prochain tour</span>
    </div>

    <div v-if="loading" class="text-center py-12">
      <v-progress-circular indeterminate color="indigo" size="36" />
    </div>

    <div v-else-if="finances" class="finances-body">

      <!-- Solde actuel -->
      <div class="solde-card">
        <div class="solde-label">Solde actuel</div>
        <div class="solde-value" :style="{ color: soldeColor(finances.solde) }">
          {{ fmt(finances.solde) }} €
        </div>
        <div class="solde-hint" v-if="finances.solde < 5000">
          ⚠️ Attention — budget faible. Les dépenses du prochain tour pourraient te mettre en Game Over.
        </div>
      </div>

      <!-- Dépenses du prochain tour -->
      <div class="section-card">
        <div class="section-title">📊 Dépenses du prochain tour</div>

        <!-- Loyer -->
        <div class="depense-row">
          <div class="depense-info">
            <span class="depense-icon">🏢</span>
            <div>
              <div class="depense-name">Loyer de la salle</div>
              <div class="depense-desc">Coût mensuel fixe de l'infrastructure</div>
            </div>
          </div>
          <span class="depense-amount">−{{ fmt(finances.loyer) }} €</span>
        </div>

        <!-- Staff -->
        <div class="depense-row">
          <div class="depense-info">
            <span class="depense-icon">👥</span>
            <div>
              <div class="depense-name">Salaires staff</div>
              <div class="depense-desc">
                {{ finances.staffTotal > 0 ? 'Entraîneurs et préparateurs embauchés' : 'Aucun staff embauché' }}
              </div>
            </div>
          </div>
          <span class="depense-amount" :style="{ color: finances.staffTotal > 0 ? '#ef4444' : '#475569' }">
            {{ finances.staffTotal > 0 ? '−' + fmt(finances.staffTotal) + ' €' : '0 €' }}
          </span>
        </div>

        <!-- Salaires combattants -->
        <div class="depense-row">
          <div class="depense-info">
            <span class="depense-icon">👊</span>
            <div>
              <div class="depense-name">Salaires combattants</div>
              <div class="depense-desc">{{ finances.combattants.length }} combattant{{ finances.combattants.length > 1 ? 's' : '' }} dans l'écurie</div>
            </div>
          </div>
          <span class="depense-amount">−{{ fmt(finances.salairesTotaux) }} €</span>
        </div>

        <!-- Liste des combattants -->
        <div v-if="finances.combattants.length > 0" class="combattants-list">
          <div v-for="c in finances.combattants" :key="c.combattantID" class="combattant-row">
            <span class="combattant-name">{{ c.prenom }} {{ c.nomFamille }}</span>
            <span class="combattant-salaire">{{ fmt(c.salaire) }} €/mois</span>
          </div>
        </div>
        <div v-else class="no-fighters">Aucun combattant dans l'écurie — pas de salaires.</div>

        <!-- Total -->
        <div class="total-row">
          <span class="total-label">Total dépenses</span>
          <span class="total-amount">−{{ fmt(finances.depensesTotales) }} €</span>
        </div>
      </div>

      <!-- Projection -->
      <div class="projection-card">
        <div class="projection-label">Solde projeté après le prochain tour</div>
        <div class="projection-value" :style="{ color: soldeColor(finances.soldeApres) }">
          {{ fmt(finances.soldeApres) }} €
        </div>
        <div class="projection-warn" v-if="finances.soldeApres < 0">
          🚨 DANGER — Passer ce tour mettra fin à ta partie (Game Over).
        </div>
        <div class="projection-warn warning" v-else-if="finances.soldeApres < 5000">
          ⚠️ Solde critique prévu. Réduis tes dépenses avant de passer le tour.
        </div>
      </div>

    </div>
  </div>
</template>

<style scoped>
.finances-tab { max-width: 700px; margin: 0 auto; }

.finances-header {
  margin-bottom: 24px;
  padding: 16px 20px;
  background: rgba(220,38,38,.08);
  border: 1px solid rgba(220,38,38,.2);
  border-radius: 16px;
}
.finances-title  { display: block; font-size: 1.15rem; font-weight: 700; color: #e2e8f0; }
.finances-sub    { display: block; font-size: .82rem;  color: #94a3b8; margin-top: 2px; }

.finances-body { display: flex; flex-direction: column; gap: 16px; }

/* Solde actuel */
.solde-card {
  padding: 20px 24px;
  background: rgba(255,255,255,.04);
  border: 1px solid rgba(255,255,255,.08);
  border-radius: 16px;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 6px;
}
.solde-label { font-size: .8rem; text-transform: uppercase; letter-spacing: .08em; color: #64748b; }
.solde-value { font-size: 2.4rem; font-weight: 900; letter-spacing: -.02em; }
.solde-hint  { font-size: .8rem; color: #f59e0b; margin-top: 4px; }

/* Section dépenses */
.section-card {
  padding: 18px 20px;
  background: rgba(255,255,255,.03);
  border: 1px solid rgba(255,255,255,.07);
  border-radius: 16px;
  display: flex;
  flex-direction: column;
  gap: 0;
}
.section-title {
  font-size: .88rem;
  font-weight: 700;
  color: #e2e8f0;
  margin-bottom: 14px;
}

.depense-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 10px 0;
  border-bottom: 1px solid rgba(255,255,255,.05);
}
.depense-info { display: flex; align-items: center; gap: 10px; }
.depense-icon { font-size: 1.2rem; }
.depense-name { font-size: .85rem; font-weight: 600; color: #e2e8f0; }
.depense-desc { font-size: .72rem; color: #64748b; margin-top: 1px; }
.depense-amount { font-size: .9rem; font-weight: 700; color: #ef4444; }

/* Liste combattants */
.combattants-list {
  margin: 6px 0 4px 32px;
  display: flex;
  flex-direction: column;
  gap: 3px;
}
.combattant-row {
  display: flex;
  justify-content: space-between;
  font-size: .78rem;
  color: #64748b;
  padding: 2px 0;
}
.combattant-name   { color: #94a3b8; }
.combattant-salaire { color: #ef4444; font-weight: 600; }
.no-fighters { font-size: .8rem; color: #475569; padding: 4px 0 6px; }

/* Total */
.total-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-top: 10px;
  padding-top: 10px;
  border-top: 1px solid rgba(255,255,255,.1);
}
.total-label  { font-size: .9rem; font-weight: 700; color: #e2e8f0; }
.total-amount { font-size: 1rem; font-weight: 900; color: #ef4444; }

/* Projection */
.projection-card {
  padding: 18px 24px;
  background: rgba(255,255,255,.03);
  border: 1px solid rgba(255,255,255,.07);
  border-radius: 16px;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 6px;
}
.projection-label { font-size: .8rem; text-transform: uppercase; letter-spacing: .08em; color: #64748b; }
.projection-value { font-size: 1.8rem; font-weight: 900; }
.projection-warn  { font-size: .82rem; color: #ef4444; font-weight: 600; margin-top: 4px; }
.projection-warn.warning { color: #f59e0b; }
</style>

