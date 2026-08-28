<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { api } from '../api/client'
import { setFlash } from '../flash'

const route = useRoute()
const router = useRouter()
const id = Number(route.params.id)
const customer = ref<{ id: number; name: string } | null>(null)

onMounted(async () => {
  customer.value = await api<{ id: number; name: string }>(`/api/customers/${id}`)
})

async function confirmDelete() {
  await api(`/api/customers/${id}`, { method: 'DELETE' })
  setFlash('Customer deleted.')
  router.push('/customers')
}
</script>

<template>
  <div class="card" v-if="customer">
    <h2>Delete customer</h2>
    <p>Are you sure you want to delete <strong>{{ customer.name }}</strong>? This will also delete all of their interactions.</p>

    <div class="actions">
      <button type="button" class="btn btn-danger" @click="confirmDelete">Delete</button>
      <RouterLink to="/customers" class="btn btn-secondary">Cancel</RouterLink>
    </div>
  </div>
</template>
