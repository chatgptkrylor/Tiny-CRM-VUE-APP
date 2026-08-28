<script setup lang="ts">
import { reactive, ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { api, ApiError } from '../api/client'
import { setFlash } from '../flash'

interface CustomerDetail {
  name: string; company: string | null; email: string | null; phone: string | null
  status: string; notes: string | null
}

const route = useRoute()
const router = useRouter()

// Same route params.id shape as CustomerDetailsView/CustomerDeleteView: present only
// on /customers/:id/edit, absent on /customers/new.
const id = route.params.id ? Number(route.params.id) : null
const isEdit = id !== null

const form = reactive({
  name: '', company: '', email: '', phone: '', status: 'Lead', notes: '',
})
const errors = ref<Record<string, string[]>>({})
const summaryErrors = ref<string[]>([])
const loaded = ref(!isEdit)

function fieldError(field: string) {
  return errors.value[field]?.[0] ?? ''
}

onMounted(async () => {
  if (isEdit) {
    const customer = await api<CustomerDetail>(`/api/customers/${id}`)
    form.name = customer.name
    form.company = customer.company ?? ''
    form.email = customer.email ?? ''
    form.phone = customer.phone ?? ''
    form.status = customer.status
    form.notes = customer.notes ?? ''
    loaded.value = true
  }
})

async function submit() {
  errors.value = {}
  summaryErrors.value = []
  const payload = {
    name: form.name,
    company: form.company || null,
    email: form.email || null,
    phone: form.phone || null,
    status: form.status,
    notes: form.notes || null,
  }
  try {
    if (isEdit) {
      await api(`/api/customers/${id}`, { method: 'PUT', body: JSON.stringify(payload) })
      setFlash('Customer updated.')
    } else {
      await api('/api/customers', { method: 'POST', body: JSON.stringify(payload) })
      setFlash('Customer added.')
    }
    router.push('/customers')
  } catch (e) {
    if (e instanceof ApiError && e.errors) {
      errors.value = e.errors
    } else {
      summaryErrors.value = ['Something went wrong. Please try again.']
    }
  }
}

const title = computed(() => (isEdit ? 'Edit customer' : 'New customer'))
</script>

<template>
  <div class="card" v-if="loaded">
    <h2>{{ title }}</h2>

    <form class="form" @submit.prevent="submit">
      <div v-if="summaryErrors.length" class="validation-summary">
        <div v-for="(msg, i) in summaryErrors" :key="i">{{ msg }}</div>
      </div>

      <div class="form-grid">
        <div class="field">
          <label for="Name">Name</label>
          <input id="Name" type="text" v-model="form.name" />
          <span class="field-error">{{ fieldError('Name') }}</span>
        </div>

        <div class="field">
          <label for="Company">Company</label>
          <input id="Company" type="text" v-model="form.company" />
          <span class="field-error">{{ fieldError('Company') }}</span>
        </div>

        <div class="field">
          <label for="Email">Email</label>
          <input id="Email" type="text" v-model="form.email" />
          <span class="field-error">{{ fieldError('Email') }}</span>
        </div>

        <div class="field">
          <label for="Phone">Phone</label>
          <input id="Phone" type="text" v-model="form.phone" />
          <span class="field-error">{{ fieldError('Phone') }}</span>
        </div>

        <div class="field">
          <label for="Status">Status</label>
          <select id="Status" v-model="form.status">
            <option value="Lead">Lead</option>
            <option value="Contact">Contact</option>
            <option value="Customer">Customer</option>
          </select>
          <span class="field-error">{{ fieldError('Status') }}</span>
        </div>

        <div class="field full">
          <label for="Notes">Notes</label>
          <textarea id="Notes" v-model="form.notes"></textarea>
          <span class="field-error">{{ fieldError('Notes') }}</span>
        </div>
      </div>

      <div class="actions">
        <button type="submit" class="btn btn-primary">{{ isEdit ? 'Save changes' : 'Save' }}</button>
        <RouterLink to="/customers" class="btn btn-secondary">Cancel</RouterLink>
      </div>
    </form>
  </div>
</template>
