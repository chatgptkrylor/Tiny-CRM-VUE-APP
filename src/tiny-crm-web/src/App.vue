<script setup lang="ts">
import { useRouter } from 'vue-router'
import { useAuth } from './auth'

const auth = useAuth()
const router = useRouter()

async function signOut() {
  await auth.logout()
  router.push('/login')
}
</script>

<template>
  <template v-if="auth.state.user">
    <header class="topbar">
      <div class="topbar-inner">
        <div class="brand"><RouterLink to="/customers">Tiny CRM</RouterLink></div>
        <nav class="nav">
          <a href="#" class="disabled" aria-disabled="true" tabindex="-1" @click.prevent>Dashboard</a>
          <RouterLink to="/customers" active-class="active">Customers</RouterLink>
          <a href="#" class="disabled" aria-disabled="true" tabindex="-1" @click.prevent>Reports</a>
        </nav>
        <div class="user-menu">
          <span class="user-name">{{ auth.state.user.displayName }}</span>
          <button type="button" class="btn btn-link" @click="signOut">Sign out</button>
        </div>
      </div>
    </header>

    <main class="container">
      <RouterView />
    </main>

    <footer class="footer">
      <div class="container">Tiny CRM &mdash; ASP.NET Core 10 / Vue 3 / EF Core 10</div>
    </footer>
  </template>
  <template v-else>
    <RouterView />
  </template>
</template>
