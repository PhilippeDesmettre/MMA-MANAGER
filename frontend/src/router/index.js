import { createRouter, createWebHistory } from 'vue-router'
import { useAuth } from '../composables/useAuth'

const routes = [
  { path: '/',              redirect: '/home' },
  { path: '/auth',          component: () => import('../views/AuthView.vue'),          meta: { public: true  } },
  { path: '/home',          component: () => import('../views/HomeView.vue'),          meta: { public: false } },
  { path: '/create-trainer',component: () => import('../views/CreateTrainerView.vue'),meta: { public: false } },
  { path: '/game',          component: () => import('../views/GameView.vue'),          meta: { public: false } },
]

const router = createRouter({
  history: createWebHistory(),
  routes
})

router.beforeEach((to) => {
  const { isAuthenticated } = useAuth()
  if (!to.meta.public && !isAuthenticated.value) return '/auth'
  if (to.path === '/auth' && isAuthenticated.value) return '/home'
})

export default router
