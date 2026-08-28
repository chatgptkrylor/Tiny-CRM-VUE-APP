import { createRouter, createWebHistory } from 'vue-router'
import { useAuth } from './auth'
import LoginView from './views/LoginView.vue'
import DashboardView from './views/DashboardView.vue'
import CustomersView from './views/CustomersView.vue'
import ReportsView from './views/ReportsView.vue'
import CustomerFormView from './views/CustomerFormView.vue'
import CustomerDetailsView from './views/CustomerDetailsView.vue'
import CustomerDeleteView from './views/CustomerDeleteView.vue'
import InteractionCreateView from './views/InteractionCreateView.vue'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', redirect: '/customers' },
    { path: '/login', component: LoginView, meta: { public: true } },
    { path: '/dashboard', component: DashboardView },
    { path: '/customers', component: CustomersView },
    { path: '/customers/new', component: CustomerFormView },
    { path: '/customers/:id/edit', component: CustomerFormView },
    { path: '/customers/:id/delete', component: CustomerDeleteView },
    { path: '/customers/:id', component: CustomerDetailsView },
    { path: '/interactions/new', component: InteractionCreateView },
    { path: '/reports', component: ReportsView },
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
