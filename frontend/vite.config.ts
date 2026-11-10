import { fileURLToPath, URL } from 'node:url'

import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import vueDevTools from 'vite-plugin-vue-devtools'
import tailwindcss from '@tailwindcss/vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    vue(),
    vueDevTools(),
    tailwindcss(),
  ],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    // DRMCAppManagement only redirects back to the URL registered for this app, so the port must not drift.
    port: 5174,
    strictPort: true,
    // Same origin in dev too, so the session cookie works. The API also runs the sign-in pages. 7201 is the WebApi https profile.
    proxy: { '^/(api|callback)': { target: 'https://localhost:7201', secure: false } },
  },
  build: {
    outDir: '../WebApi/wwwroot',
    emptyOutDir: true,
    rollupOptions: {
      output: {
        manualChunks(id) {
          if (id.includes('node_modules')) {
            if (id.includes('primevue')) return 'primevue'
            if (id.includes('vue')) return 'vue'
            if (id.includes('zod')) return 'validation'
            if (id.includes('axios')) return 'network'
            return 'vendor'
          }
        }
      }
    }
  }
})
