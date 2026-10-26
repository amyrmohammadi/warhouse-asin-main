<script setup lang="ts">
import type { EntryMode } from '@/composables/useEntryRows'

defineProps<{ title: string; mode: EntryMode; disabled?: boolean }>()
const emit = defineEmits<{ 'update:mode': [mode: EntryMode]; add: [] }>()
</script>

<template>
  <div class="entry-items-header mb-2">
    <h6 class="mb-0">{{ title }}</h6>
    <div class="entry-items-header__actions">
      <div class="btn-group btn-group-sm" role="group" aria-label="روش ثبت کالا">
        <button class="btn" :class="mode === 'cards' ? 'btn-primary' : 'btn-outline-primary'" type="button"
                :disabled="disabled" title="ثبت کارتی" aria-label="ثبت کارتی" :aria-pressed="mode === 'cards'"
                @click="emit('update:mode', 'cards')">
          <i class="las la-stream" aria-hidden="true"></i>
        </button>
        <button class="btn" :class="mode === 'list' ? 'btn-primary' : 'btn-outline-primary'" type="button"
                :disabled="disabled" title="ثبت جدولی" aria-label="ثبت جدولی" :aria-pressed="mode === 'list'"
                @click="emit('update:mode', 'list')">
          <i class="las la-table" aria-hidden="true"></i>
        </button>
      </div>
      <button class="btn btn-outline-primary btn-sm" type="button" :disabled="disabled" @click="emit('add')">
        <i class="las la-plus ml-1" aria-hidden="true"></i> افزودن ردیف
      </button>
    </div>
  </div>
</template>

<style scoped>
.entry-items-header { align-items: center; display: flex; gap: 1rem; justify-content: space-between; }
.entry-items-header__actions { align-items: center; display: flex; flex-wrap: wrap; gap: .55rem; }
.entry-items-header .btn-group .btn { align-items: center; display: inline-flex; justify-content: center; }
@media (max-width: 575px) {
  .entry-items-header { align-items: stretch; flex-direction: column; }
  .entry-items-header__actions { justify-content: space-between; }
  .entry-items-header .btn-group { display: flex; flex: 1 1 auto; }
  .entry-items-header .btn-group .btn { flex: 1 1 50%; min-width: 0; }
}
</style>
