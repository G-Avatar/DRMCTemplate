<script setup lang="ts">
import { House, LoaderCircle } from '@lucide/vue'
import ThemeToggle from '@/components/ThemeToggle.vue'
import { Button } from '@/components/ui/button'
import {
  Sidebar,
  SidebarContent,
  SidebarGroup,
  SidebarGroupContent,
  SidebarHeader,
  SidebarInset,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  SidebarProvider,
  SidebarRail,
  SidebarTrigger,
} from '@/components/ui/sidebar'
import drmcLogo from '@/images/drmc-logo.png'
import Page from '@/stores/page'

// Add a page here and in the router to show it in the sidebar.
// shortcut: on phones the sidebar stays open after tapping a link; close it with useSidebar() once there is more than one page.
const menu = [{ title: 'Home', to: '/', icon: House }]
</script>

<template>
  <!-- Only once the router has confirmed the login, so the layout doesn't flash before a redirect to the portal. -->
  <SidebarProvider v-if="Page.user">
    <Sidebar collapsible="icon">
      <SidebarHeader>
        <SidebarMenu>
          <SidebarMenuItem>
            <SidebarMenuButton size="lg" as-child>
              <RouterLink to="/">
                <img :src="drmcLogo" alt="" class="size-8 shrink-0 object-contain" />
                <div class="grid flex-1 text-left text-sm leading-tight">
                  <span class="truncate font-semibold text-navy">Payroll Management</span>
                  <span class="truncate text-[10px] font-medium tracking-[0.15em] text-subtle uppercase">DRMC</span>
                </div>
              </RouterLink>
            </SidebarMenuButton>
          </SidebarMenuItem>
        </SidebarMenu>
      </SidebarHeader>

      <SidebarContent>
        <SidebarGroup>
          <SidebarGroupContent>
            <SidebarMenu>
              <SidebarMenuItem v-for="item in menu" :key="item.to">
                <SidebarMenuButton
                  as-child
                  :is-active="$route.path === item.to"
                  :tooltip="item.title"
                  class="data-[active=true]:bg-sky-100 data-[active=true]:text-brand dark:data-[active=true]:bg-sky-950"
                >
                  <RouterLink :to="item.to">
                    <component :is="item.icon" aria-hidden="true" />
                    <span>{{ item.title }}</span>
                  </RouterLink>
                </SidebarMenuButton>
              </SidebarMenuItem>
            </SidebarMenu>
          </SidebarGroupContent>
        </SidebarGroup>
      </SidebarContent>

      <SidebarRail />
    </Sidebar>

    <SidebarInset class="bg-canvas text-navy">
      <header class="flex h-14 shrink-0 items-center gap-2 border-b bg-card px-4">
        <SidebarTrigger class="-ml-1" />
        <div class="ml-auto flex items-center gap-2">
          <ThemeToggle />
          <span class="grid size-9 place-items-center rounded-full bg-sky-100 text-xs font-semibold text-brand dark:bg-sky-950">
            {{ Page.user?.fullName.slice(0, 2).toUpperCase() }}
          </span>
          <span class="hidden text-sm font-semibold sm:block">{{ Page.user?.fullName }}</span>
          <Button as="a" href="/api/account/portal" variant="outline" size="sm">Back to portal</Button>
        </div>
      </header>

      <div class="flex-1 p-4">
        <RouterView />
      </div>
    </SidebarInset>
  </SidebarProvider>

  <!-- While the router checks the login, and while the browser is leaving for the portal. -->
  <div v-else role="status" aria-label="Loading" class="grid min-h-screen place-items-center bg-canvas">
    <LoaderCircle class="size-8 text-brand motion-safe:animate-spin" aria-hidden="true" />
  </div>
</template>
