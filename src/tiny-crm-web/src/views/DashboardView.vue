<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { api } from '../api/client'

interface RecentInteraction {
  id: number; customerId: number; customerName: string
  type: string; subject: string; interactionDate: string
}
interface DashboardResponse {
  totalCustomers: number
  totalInteractions: number
  customersByStatus: Record<string, number>
  interactionsByType: Record<string, number>
  recentInteractions: RecentInteraction[]
  needsFollowUps: number
}

// Fixed display order, matching the CustomerStatus / InteractionType enum
// declaration order - JSON object key order isn't a format guarantee.
const STATUSES = ['Lead', 'Contact', 'Customer']
const TYPES = ['Call', 'Email', 'Meeting', 'Note']

const data = ref<DashboardResponse | null>(null)

function pct(count: number, total: number) {
  return Math.floor((count * 100) / (total || 1))
}

onMounted(async () => {
  data.value = await api<DashboardResponse>('/api/dashboard')
})
</script>

<template>
  <template v-if="data">
    <div class="page-header">
      <h1>Dashboard</h1>
      <div class="actions">
        <RouterLink to="/customers/new" class="btn btn-primary">New customer</RouterLink>
      </div>
    </div>

    <div class="grid">
      <div class="stat">
        <div class="label">Total customers</div>
        <div class="value">{{ data.totalCustomers }}</div>
      </div>
      <div class="stat">
        <div class="label">Total interactions</div>
        <div class="value">{{ data.totalInteractions }}</div>
      </div>
      <div class="stat">
        <div class="label">Needs follow-up</div>
        <div class="value">{{ data.needsFollowUps }}</div>
      </div>
    </div>

    <div class="card">
      <h2>Customers by status</h2>
      <div class="bar-row" v-for="status in STATUSES" :key="status">
        <div class="bar-label">{{ status }}</div>
        <div class="bar-track">
          <div class="bar-fill" :style="{ width: pct(data.customersByStatus[status] ?? 0, data.totalCustomers) + '%' }"></div>
        </div>
        <div class="bar-value">{{ data.customersByStatus[status] ?? 0 }}</div>
      </div>
    </div>

    <div class="card">
      <h2>Interactions by type</h2>
      <div class="bar-row" v-for="type in TYPES" :key="type">
        <div class="bar-label">{{ type }}</div>
        <div class="bar-track">
          <div class="bar-fill" :style="{ width: pct(data.interactionsByType[type] ?? 0, data.totalInteractions) + '%' }"></div>
        </div>
        <div class="bar-value">{{ data.interactionsByType[type] ?? 0 }}</div>
      </div>
    </div>

    <div class="card">
      <h2>Recent interactions</h2>
      <div v-if="data.recentInteractions.length === 0" class="empty">No interactions yet.</div>
      <table v-else class="table">
        <thead>
          <tr>
            <th>Date</th>
            <th>Type</th>
            <th>Subject</th>
            <th>Customer</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="i in data.recentInteractions" :key="i.id">
            <td>{{ i.interactionDate.slice(0, 10) }}</td>
            <td>{{ i.type }}</td>
            <td>{{ i.subject }}</td>
            <td><RouterLink :to="`/customers/${i.customerId}`">{{ i.customerName }}</RouterLink></td>
          </tr>
        </tbody>
      </table>
    </div>
  </template>
</template>
