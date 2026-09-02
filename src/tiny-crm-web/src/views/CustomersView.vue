<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, watch } from 'vue'
import { apiRes } from '../api/client'
import { takeFlash } from '../flash'

interface CustomerListItem {
  id: number; name: string; company: string | null; email: string | null
  phone: string | null; status: string; lastInteractionDate: string | null; interactionCount: number
}

const PAGE_SIZE = 20

const customers = ref<CustomerListItem[]>([])
const search = ref('')
const status = ref('')
const successMessage = ref<string | null>(null)
const page = ref(1)
const total = ref(0)

const totalPages = computed(() => Math.max(1, Math.ceil(total.value / PAGE_SIZE)))
const firstOnPage = computed(() => (total.value === 0 ? 0 : (page.value - 1) * PAGE_SIZE + 1))
const lastOnPage = computed(() => (page.value - 1) * PAGE_SIZE + customers.value.length)

async function load() {
  const params = new URLSearchParams()
  if (search.value) params.set('search', search.value)
  if (status.value) params.set('status', status.value)
  params.set('page', String(page.value))
  params.set('pageSize', String(PAGE_SIZE))
  const res = await apiRes('/api/customers?' + params.toString())
  total.value = Number(res.headers.get('X-Total-Count') ?? 0)
  customers.value = (await res.json()) as CustomerListItem[]
}

// Any change to the filters has to reset the page, or searching while on page 5
// of the old result set lands you past the end of the new one and shows nothing.
function reload() {
  page.value = 1
  load()
}

function goToPage(p: number) {
  page.value = Math.min(Math.max(1, p), totalPages.value)
  load()
}

function lastInteraction(c: CustomerListItem) {
  return c.lastInteractionDate ? c.lastInteractionDate.slice(0, 10) : '—'
}

// SPA filtering: no server round-trip needed, so no submit button - just
// debounce as-you-type search. Enter still works via the form's submit handler.
let debounceTimer: ReturnType<typeof setTimeout> | undefined
watch(search, () => {
  clearTimeout(debounceTimer)
  debounceTimer = setTimeout(reload, 300)
})
onUnmounted(() => clearTimeout(debounceTimer))

function submitNow() {
  clearTimeout(debounceTimer)
  reload()
}

onMounted(() => {
  successMessage.value = takeFlash()
  load()
})
</script>

<template>
  <div v-if="successMessage" class="alert alert-success">{{ successMessage }}</div>

  <div class="page-header">
    <h1>Customers</h1>
    <RouterLink to="/customers/new" class="btn btn-primary">New customer</RouterLink>
  </div>

  <div class="toolbar">
    <form class="form" @submit.prevent="submitNow">
      <label>
        Search
        <input type="text" name="search" v-model="search" placeholder="Search name, email, company" />
      </label>
      <label>
        Status
        <select name="status" v-model="status" @change="reload">
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
        <td><RouterLink :to="`/customers/${c.id}`">{{ c.name }}</RouterLink></td>
        <td>{{ c.company }}</td>
        <td>{{ c.email }}</td>
        <td>{{ c.phone }}</td>
        <td><span :class="['badge', 'badge-' + c.status.toLowerCase()]">{{ c.status }}</span></td>
        <td>{{ lastInteraction(c) }}</td>
        <td class="actions">
          <RouterLink :to="`/customers/${c.id}/edit`" class="btn btn-secondary btn-sm">Edit</RouterLink>
          <RouterLink :to="`/customers/${c.id}/delete`" class="btn btn-danger btn-sm">Delete</RouterLink>
        </td>
      </tr>
    </tbody>
  </table>

  <div v-if="total > 0" class="pagination">
    <span class="pagination-info">
      Showing {{ firstOnPage }}–{{ lastOnPage }} of {{ total }}
    </span>
    <div class="pagination-controls">
      <button type="button" class="btn btn-secondary btn-sm"
              :disabled="page <= 1" @click="goToPage(page - 1)">Previous</button>
      <span class="pagination-page">Page {{ page }} of {{ totalPages }}</span>
      <button type="button" class="btn btn-secondary btn-sm"
              :disabled="page >= totalPages" @click="goToPage(page + 1)">Next</button>
    </div>
  </div>
</template>
