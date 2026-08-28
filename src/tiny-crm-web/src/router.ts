import { createRouter, createWebHistory } from 'vue-router'
import { useAuth } from './auth'
import LoginView from './views/LoginView.vue'
import CustomersView from './views/CustomersView.vue'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', redirect: '/customers' },
    { path: '/login', component: LoginView, meta: { public: true } },
    { path: '/customers', component: CustomersView },
  ],
})

router.beforeEach(async (to) => {
  const auth = useAuth()
  if (!auth.state.ready) await auth.refresh()
  if (!to.meta.public && !auth.state.user) {
    return { path: '/login', query: { returnUrl: to.fullPath } }
  }
  if (to.path === '/login' && auth.state.user) return { path: '/customers' }
  return true
})

export default router
