<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { api } from '../api/client'

interface StatusSummaryItem { status: string; count: number }
interface InteractionTypeSummaryItem { type: string; count: number }
interface CustomerReportItem {
  id: number; name: string; company: string | null; status: string
  interactionCount: number; lastInteractionDate: string | null
}
interface ReportsResponse {
  statusSummary: StatusSummaryItem[]
  interactionTypeSummary: InteractionTypeSummaryItem[]
  customers: CustomerReportItem[]
}

const data = ref<ReportsResponse | null>(null)

function lastInteraction(c: CustomerReportItem) {
  return c.lastInteractionDate ? c.lastInteractionDate.slice(0, 10) : '—'
}

onMounted(async () => {
  data.value = await api<ReportsResponse>('/api/reports')
})
</script>

<template>
  <template v-if="data">
    <div class="page-header">
      <h1>Reports</h1>
      <div class="actions">
        <a href="/api/reports/customers.csv" class="btn btn-primary">Export customers (CSV)</a>
      </div>
    </div>

    <div class="card">
      <h2>Status summary</h2>
      <table class="table">
        <thead>
          <tr>
            <th>Status</th>
            <th>Count</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="row in data.statusSummary" :key="row.status">
            <td><span :class="['badge', 'badge-' + row.status.toLowerCase()]">{{ row.status }}</span></td>
            <td>{{ row.count }}</td>
          </tr>
        </tbody>
      </table>
    </div>

    <div class="card">
      <h2>Interactions by type</h2>
      <table class="table">
        <thead>
          <tr>
            <th>Type</th>
            <th>Count</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="row in data.interactionTypeSummary" :key="row.type">
            <td>{{ row.type }}</td>
            <td>{{ row.count }}</td>
          </tr>
        </tbody>
      </table>
    </div>

    <div class="card">
      <h2>Customers overview</h2>
      <table class="table">
        <thead>
          <tr>
            <th>Name</th>
            <th>Company</th>
            <th>Status</th>
            <th>Interactions</th>
            <th>Last interaction</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="row in data.customers" :key="row.id">
            <td>{{ row.name }}</td>
            <td>{{ row.company }}</td>
            <td><span :class="['badge', 'badge-' + row.status.toLowerCase()]">{{ row.status }}</span></td>
            <td>{{ row.interactionCount }}</td>
            <td>{{ lastInteraction(row) }}</td>
          </tr>
        </tbody>
      </table>
    </div>
  </template>
</template>
