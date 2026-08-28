<script setup lang="ts">
import { reactive, ref, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { api, ApiError } from '../api/client'
import { setFlash } from '../flash'

const route = useRoute()
const router = useRouter()
const customerId = Number(route.query.customerId)
const customerName = ref<string | null>(null)

function todayLocal() {
  const d = new Date()
  return new Date(d.getTime() - d.getTimezoneOffset() * 60000).toISOString().slice(0, 10)
}

const form = reactive({
  type: 'Call',
  interactionDate: todayLocal(),
  subject: '',
  notes: '',
})
const errors = ref<Record<string, string[]>>({})
const summaryErrors = ref<string[]>([])

function fieldError(field: string) {
  return errors.value[field]?.[0] ?? ''
}

onMounted(async () => {
  const customer = await api<{ name: string }>(`/api/customers/${customerId}`)
  customerName.value = customer.name
})

async function submit() {
  errors.value = {}
  summaryErrors.value = []
  try {
    await api('/api/interactions', {
      method: 'POST',
      body: JSON.stringify({
        customerId,
        type: form.type,
        interactionDate: form.interactionDate,
        subject: form.subject,
        notes: form.notes || null,
      }),
    })
    setFlash('Interaction logged.')
    router.push(`/customers/${customerId}`)
  } catch (e) {
    if (e instanceof ApiError && e.errors) {
      errors.value = e.errors
    } else {
      summaryErrors.value = ['Something went wrong. Please try again.']
    }
  }
}
</script>

<template>
  <div class="page-header">
    <h1>Log interaction</h1>
    <RouterLink :to="`/customers/${customerId}`" class="btn btn-secondary">Back to customer</RouterLink>
  </div>

  <div class="card">
    <h2>{{ customerName ? 'For ' + customerName : 'New interaction' }}</h2>

    <form class="form" @submit.prevent="submit">
      <div v-if="summaryErrors.length" class="validation-summary">
        <div v-for="(msg, i) in summaryErrors" :key="i">{{ msg }}</div>
      </div>

      <div class="form-grid">
        <div class="field">
          <label for="Type">Type</label>
          <select id="Type" v-model="form.type">
            <option value="Call">Call</option>
            <option value="Email">Email</option>
            <option value="Meeting">Meeting</option>
            <option value="Note">Note</option>
          </select>
          <span class="field-error">{{ fieldError('Type') }}</span>
        </div>

        <div class="field">
          <label for="InteractionDate">Date</label>
          <input id="InteractionDate" type="date" v-model="form.interactionDate" />
          <span class="field-error">{{ fieldError('InteractionDate') }}</span>
        </div>

        <div class="field">
          <label for="Subject">Subject</label>
          <input id="Subject" type="text" v-model="form.subject" />
          <span class="field-error">{{ fieldError('Subject') }}</span>
        </div>

        <div class="field full">
          <label for="Notes">Notes</label>
          <textarea id="Notes" v-model="form.notes"></textarea>
          <span class="field-error">{{ fieldError('Notes') }}</span>
        </div>
      </div>

      <div class="field">
        <button type="submit" class="btn btn-primary">Save</button>
        <RouterLink :to="`/customers/${customerId}`" class="btn btn-secondary">Cancel</RouterLink>
      </div>
    </form>
  </div>
</template>
