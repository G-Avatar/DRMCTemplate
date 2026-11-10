<script setup lang="ts">
import { ref } from 'vue'
import { Moon, Sun } from '@lucide/vue'
import { Button } from '@/components/ui/button'

// index.html sets the class before the page draws; this only reads it and changes it.
const isDark = ref(document.documentElement.classList.contains('dark'))

const toggle = () => {
    isDark.value = !isDark.value
    document.documentElement.classList.toggle('dark', isDark.value)
    try {
        localStorage.setItem('theme', isDark.value ? 'dark' : 'light')
    } catch {
        // Storage can be blocked; the theme still changes for this page.
    }
}
</script>

<template>
    <Button variant="ghost" size="icon" :aria-label="isDark ? 'Switch to light mode' : 'Switch to dark mode'" @click="toggle">
        <component :is="isDark ? Sun : Moon" aria-hidden="true" />
    </Button>
</template>
