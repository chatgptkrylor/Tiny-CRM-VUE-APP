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
  <div class="login-wrap">
    <div class="login-card">
      <h1>Tiny CRM</h1>
      <p class="subtitle">Sign in to continue</p>

      <form class="form" @submit.prevent="submit">
        <div v-if="error" class="validation-summary">{{ error }}</div>

        <div class="field">
          <label for="Username">Username</label>
          <input id="Username" name="Username" type="text" v-model="username" autocomplete="username" autofocus />
        </div>

        <div class="field">
          <label for="Password">Password</label>
          <input id="Password" name="Password" type="password" v-model="password" autocomplete="current-password" />
        </div>

        <button type="submit" class="btn btn-primary btn-block">Sign in</button>
      </form>

      <div class="hint">
        <strong>Demo accounts:</strong><br />
        admin / admin123<br />
        demo / demo123
      </div>
    </div>
  </div>
</template>
