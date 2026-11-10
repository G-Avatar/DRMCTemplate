import { createRouter, createWebHistory } from 'vue-router'
import useApiClient from '@/composables/useApiClient'
import type { IAccount } from '@/interfaces/global/IAccount'
import { signedIn, toPortal } from '@/lib/auth'
import HomePage from '@/pages/HomePage.vue'
import Page from '@/stores/page'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    { path: '/', name: 'home', component: HomePage },
    { path: '/:pathMatch(.*)*', redirect: '/' },
  ],
})

// Asks the API on every navigation. A new route needs a login automatically.
router.beforeEach(async () => {
  const { request, get } = useApiClient<undefined, IAccount>()
  await get('account/me')
  Page.user = request.status === 200 ? (request.data ?? null) : null
  if (Page.user) signedIn()

  // 401: useApiClient is already sending the browser to the portal.
  if (request.status === 401) return false
  if (!Page.user) {
    toPortal()
    return false
  }
})

export default router
