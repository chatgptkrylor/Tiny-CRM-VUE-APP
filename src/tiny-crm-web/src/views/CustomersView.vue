<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { api } from '../api/client'
import { useAuth } from '../auth'
import { useRouter } from 'vue-router'

interface CustomerListItem {
  id: number; name: string; company: string | null; email: string | null
  phone: string | null; status: string; lastInteractionDate: string | null; interactionCount: number
}

const customers = ref<CustomerListItem[]>([])
const search = ref('')
const status = ref('')
const auth = useAuth()
const router = useRouter()

async function load() {
  const params = new URLSearchParams()
  if (search.value) params.set('search', search.value)
  if (status.value) params.set('status', status.value)
  customers.value = await api<CustomerListItem[]>('/api/customers?' + params.toString())
}

async function signOut() {
  await auth.logout()
  router.push('/login')
}

onMounted(load)
</script>

<template>
  <header>
    <span>{{ auth.state.user?.displayName }}</span>
    <button type="button" @click="signOut">Sign out</button>
  </header>
  <main>
    <h1>Customers</h1>
    <form @submit.prevent="load">
      <input name="search" v-model="search" placeholder="Search" />
      <select name="status" v-model="status" @change="load">
        <option value="">All statuses</option>
        <option value="Lead">Lead</option>
        <option value="Contact">Contact</option>
        <option value="Customer">Customer</option>
      </select>
      <button type="submit">Filter</button>
    </form>

    <table class="table">
      <thead>
        <tr><th>Name</th><th>Company</th><th>Email</th><th>Status</th><th>Interactions</th></tr>
      </thead>
      <tbody>
        <tr v-for="c in customers" :key="c.id">
          <td>{{ c.name }}</td>
          <td>{{ c.company }}</td>
          <td>{{ c.email }}</td>
          <td><span :class="'badge-' + c.status.toLowerCase()">{{ c.status }}</span></td>
          <td>{{ c.interactionCount }}</td>
        </tr>
      </tbody>
    </table>
  </main>
</template>
