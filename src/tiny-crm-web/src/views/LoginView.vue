<script setup lang="ts">
import { ref } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useAuth } from '../auth'

const username = ref('')
const password = ref('')
const error = ref<string | null>(null)
const auth = useAuth()
const router = useRouter()
const route = useRoute()

async function submit() {
  error.value = await auth.login(username.value, password.value)
  if (!error.value) {
    const target = (route.query.returnUrl as string) || '/customers'
    router.push(target)
  }
}
</script>

<template>
  <main class="login-card">
    <h1>Tiny CRM</h1>
    <form @submit.prevent="submit">
      <label for="Username">Username</label>
      <input id="Username" name="Username" v-model="username" autocomplete="username" />

      <label for="Password">Password</label>
      <input id="Password" name="Password" type="password" v-model="password" autocomplete="current-password" />

      <div v-if="error" class="validation-summary">{{ error }}</div>

      <button type="submit">Sign in</button>
    </form>
  </main>
</template>
