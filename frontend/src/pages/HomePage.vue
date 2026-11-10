<script setup lang="ts">
import Page from '@/stores/page'
</script>

<template>
  <h1>Welcome, {{ Page.user?.fullName }}</h1>
</template>
