<script setup lang="ts">
import { computed, ref } from 'vue'

import { getStoredTheme, storeTheme, type ColorTheme } from '@/services/theme'

defineOptions({ name: 'AuthThemeSwitch' })

const colorTheme = ref<ColorTheme>(getStoredTheme())
const isDarkTheme = computed(() => colorTheme.value === 'dark')

function toggleTheme(): void {
  colorTheme.value = isDarkTheme.value ? 'light' : 'dark'
  storeTheme(colorTheme.value)
}
</script>

<template>
  <button
    class="auth-theme-switch"
    type="button"
    :aria-label="isDarkTheme ? 'فعال‌سازی تم روشن' : 'فعال‌سازی تم تاریک'"
    :aria-pressed="isDarkTheme"
    :title="isDarkTheme ? 'تم روشن' : 'تم تاریک'"
    @click="toggleTheme"
  >
    <i :class="isDarkTheme ? 'las la-sun' : 'las la-moon'" aria-hidden="true"></i>
  </button>
</template>
