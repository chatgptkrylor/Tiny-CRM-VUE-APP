<script setup lang="ts">
import { ref, onMounted, onUnmounted, watch } from 'vue'
import { api } from '../api/client'

interface CustomerListItem {
  id: number; name: string; company: string | null; email: string | null
  phone: string | null; status: string; lastInteractionDate: string | null; interactionCount: number
}

const customers = ref<CustomerListItem[]>([])
const search = ref('')
const status = ref('')

async function load() {
  const params = new URLSearchParams()
  if (search.value) params.set('search', search.value)
  if (status.value) params.set('status', status.value)
  customers.value = await api<CustomerListItem[]>('/api/customers?' + params.toString())
}

function lastInteraction(c: CustomerListItem) {
  return c.lastInteractionDate ? c.lastInteractionDate.slice(0, 10) : '—'
}

// SPA filtering: no server round-trip needed, so no submit button - just
// debounce as-you-type search. Enter still works via the form's submit handler.
let debounceTimer: ReturnType<typeof setTimeout> | undefined
watch(search, () => {
  clearTimeout(debounceTimer)
  debounceTimer = setTimeout(load, 300)
})
onUnmounted(() => clearTimeout(debounceTimer))

function submitNow() {
  clearTimeout(debounceTimer)
  load()
}

onMounted(load)
</script>

<template>
  <div class="page-header">
    <h1>Customers</h1>
    <button type="button" class="btn btn-primary" disabled>New customer</button>
  </div>

  <div class="toolbar">
    <form class="form" @submit.prevent="submitNow">
      <label>
        Search
        <input type="text" name="search" v-model="search" placeholder="Search name, email, company" />
      </label>
      <label>
        Status
        <select name="status" v-model="status" @change="load">
          <option value="">All</option>
          <option value="Lead">Lead</option>
          <option value="Contact">Contact</option>
          <option value="Customer">Customer</option>
        </select>
      </label>
    </form>
  </div>

  <div v-if="customers.length === 0" class="empty">No customers found.</div>
  <table v-else class="table">
    <thead>
      <tr>
        <th>Name</th>
        <th>Company</th>
        <th>Email</th>
        <th>Phone</th>
        <th>Status</th>
        <th>Last Interaction</th>
        <th>Actions</th>
      </tr>
    </thead>
    <tbody>
      <tr v-for="c in customers" :key="c.id">
        <td>{{ c.name }}</td>
        <td>{{ c.company }}</td>
        <td>{{ c.email }}</td>
        <td>{{ c.phone }}</td>
        <td><span :class="['badge', 'badge-' + c.status.toLowerCase()]">{{ c.status }}</span></td>
        <td>{{ lastInteraction(c) }}</td>
        <td class="actions">
          <button type="button" class="btn btn-secondary btn-sm" disabled>Edit</button>
          <button type="button" class="btn btn-danger btn-sm" disabled>Delete</button>
        </td>
      </tr>
    </tbody>
  </table>
</template>
