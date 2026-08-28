<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { api } from '../api/client'
import { takeFlash } from '../flash'

interface InteractionItem {
  id: number; customerId: number; type: string; subject: string
  notes: string | null; interactionDate: string; createdAt: string
}
interface CustomerDetail {
  id: number; name: string; company: string | null; email: string | null; phone: string | null
  status: string; notes: string | null; createdAt: string; lastInteractionDate: string | null
  interactions: InteractionItem[]
}

const route = useRoute()
const id = Number(route.params.id)
const customer = ref<CustomerDetail | null>(null)
const successMessage = ref<string | null>(null)

async function load() {
  customer.value = await api<CustomerDetail>(`/api/customers/${id}`)
}

async function deleteInteraction(interactionId: number) {
  await api(`/api/interactions/${interactionId}`, { method: 'DELETE' })
  successMessage.value = 'Interaction deleted.'
  await load()
}

function notesPreview(notes: string | null) {
  if (!notes) return notes
  return notes.length > 80 ? notes.slice(0, 80) + '…' : notes
}

// .NET DateTime serialises as ISO 8601; original formatted these with .ToString(...).
function formatDateTime(value: string) {
  return value.replace('T', ' ').slice(0, 16)
}
function formatDate(value: string) {
  return value.slice(0, 10)
}

onMounted(async () => {
  successMessage.value = takeFlash()
  await load()
})
</script>

<template>
  <template v-if="customer">
    <div v-if="successMessage" class="alert alert-success">{{ successMessage }}</div>

    <div class="page-header">
      <h1>{{ customer.name }}</h1>
      <div class="actions">
        <RouterLink :to="`/customers/${customer.id}/edit`" class="btn btn-secondary">Edit</RouterLink>
        <RouterLink to="/customers" class="btn btn-secondary">Back to list</RouterLink>
      </div>
    </div>

    <div class="card">
      <div class="form-grid">
        <div class="field">
          <label>Company</label>
          <div>{{ customer.company ?? '—' }}</div>
        </div>
        <div class="field">
          <label>Email</label>
          <div>{{ customer.email ?? '—' }}</div>
        </div>
        <div class="field">
          <label>Phone</label>
          <div>{{ customer.phone ?? '—' }}</div>
        </div>
        <div class="field">
          <label>Status</label>
          <div><span :class="['badge', 'badge-' + customer.status.toLowerCase()]">{{ customer.status }}</span></div>
        </div>
        <div class="field">
          <label>Created</label>
          <div>{{ formatDateTime(customer.createdAt) }}</div>
        </div>
        <div class="field">
          <label>Last interaction</label>
          <div>{{ customer.lastInteractionDate ? formatDate(customer.lastInteractionDate) : '—' }}</div>
        </div>
        <div class="field full">
          <label>Notes</label>
          <div>{{ customer.notes && customer.notes.trim() ? customer.notes : '—' }}</div>
        </div>
      </div>
    </div>

    <div class="page-header">
      <h2>Interactions</h2>
      <RouterLink :to="`/interactions/new?customerId=${customer.id}`" class="btn btn-primary">Log interaction</RouterLink>
    </div>

    <div v-if="customer.interactions.length === 0" class="empty">No interactions yet.</div>
    <table v-else class="table">
      <thead>
        <tr>
          <th>Date</th>
          <th>Type</th>
          <th>Subject</th>
          <th>Notes</th>
          <th>Actions</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="i in customer.interactions" :key="i.id">
          <td>{{ formatDate(i.interactionDate) }}</td>
          <td>{{ i.type }}</td>
          <td>{{ i.subject }}</td>
          <td>{{ notesPreview(i.notes) }}</td>
          <td>
            <button type="button" class="btn btn-danger btn-sm" @click="deleteInteraction(i.id)">Delete</button>
          </td>
        </tr>
      </tbody>
    </table>
  </template>
</template>
