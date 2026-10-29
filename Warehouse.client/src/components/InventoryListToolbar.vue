<script setup lang="ts">
import { ref } from 'vue'

import type { ToggleableColumn } from '@/composables/useColumnVisibility'

defineProps<{
  mode: string
  modes: { value: string; label: string }[]
  /** مثلاً «ورودی کالا» / «خروجی کالا» برای عنوان دکمه‌ها */
  entityLabel: string
  createLabel: string
  canCreate: boolean
  canImport?: boolean
  canExport?: boolean
  createDisabled?: boolean
  exporting?: boolean
  columns: ToggleableColumn[]
  isColumnVisible: (key: string) => boolean
  columnMenuOpen: boolean
}>()

const emit = defineEmits<{
  'update:mode': [mode: string]
  create: []
  export: []
  import: [file: File]
  'toggle-column': [column: ToggleableColumn]
  'toggle-column-menu': []
}>()

const fileInput = ref<HTMLInputElement | null>(null)

function onFileChange(event: Event): void {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = ''
  if (file) emit('import', file)
}
</script>

<template>
  <div class="inventory-toolbar">
    <label v-if="modes.length > 1" class="inventory-toolbar__mode mb-0">
      <span>نوع نمایش</span>
      <select class="form-control form-control-sm" aria-label="نوع نمایش جدول" :value="mode"
              @change="emit('update:mode', ($event.target as HTMLSelectElement).value)">
        <option v-for="item in modes" :key="item.value" :value="item.value">{{ item.label }}</option>
      </select>
    </label>

    <input v-if="canImport" ref="fileInput" type="file" accept=".xlsx,.xlsm" hidden @change="onFileChange" />

    <div class="btn-group inventory-toolbar__group" role="group" aria-label="ابزارهای لیست" @click.stop>
      <button v-if="canImport" class="btn btn-outline-primary btn-sm inventory-toolbar__icon inventory-toolbar__import"
              type="button" :title="`ایمپورت ${entityLabel}`" :aria-label="`ایمپورت ${entityLabel}`"
              @click="fileInput?.click()">
        <i class="las la-file-import" aria-hidden="true"></i>
      </button>
      <button v-if="canExport" class="btn btn-outline-success btn-sm inventory-toolbar__icon inventory-toolbar__export"
              type="button" :title="`اکسپورت ${entityLabel}`" :aria-label="`اکسپورت ${entityLabel}`"
              :disabled="exporting" @click="emit('export')">
        <span v-if="exporting" class="spinner-border spinner-border-sm" aria-hidden="true"></span>
        <i v-else class="las la-file-excel" aria-hidden="true"></i>
      </button>
      <button class="btn btn-outline-primary btn-sm inventory-toolbar__icon inventory-toolbar__columns" type="button"
              title="تنظیم ستون‌ها" aria-label="تنظیم ستون‌ها" :aria-expanded="columnMenuOpen"
              @click="emit('toggle-column-menu')">
        <i class="las la-columns" aria-hidden="true"></i>
      </button>
      <div v-if="columnMenuOpen" class="inventory-toolbar__menu">
        <div class="inventory-toolbar__menu-title">ستون‌های قابل نمایش</div>
        <label v-for="column in columns" :key="column.key" class="inventory-toolbar__menu-option">
          <input type="checkbox" :checked="isColumnVisible(column.key)" @change="emit('toggle-column', column)" />
          <span>{{ column.label }}</span>
        </label>
      </div>
    </div>

    <div v-if="$slots['create-actions']" class="inventory-toolbar__extra">
      <slot name="create-actions" />
    </div>
    <button v-if="canCreate" class="btn btn-primary btn-sm mobile-icon-button inventory-toolbar__add" type="button"
            :title="createLabel" :disabled="createDisabled" @click="emit('create')">
      <i class="las la-plus ml-1" aria-hidden="true"></i>
      <span class="mobile-button-label">{{ createLabel }}</span>
    </button>
  </div>

  <div v-if="canCreate || canExport || canImport || $slots['create-actions']" class="inventory-mobile-actions" role="toolbar"
       :aria-label="`عملیات ${entityLabel}`">
    <slot name="create-actions" />
    <button v-if="canCreate" class="btn btn-primary" type="button" :disabled="createDisabled" @click="emit('create')">
      <i class="las la-plus" aria-hidden="true"></i><span>{{ createLabel }}</span>
    </button>
    <button v-if="canExport" class="btn btn-outline-success" type="button" :disabled="exporting" @click="emit('export')">
      <span v-if="exporting" class="spinner-border spinner-border-sm" aria-hidden="true"></span>
      <i v-else class="las la-file-excel" aria-hidden="true"></i><span>خروجی Excel</span>
    </button>
    <button v-if="canImport" class="btn btn-outline-primary" type="button" @click="fileInput?.click()">
      <i class="las la-file-import" aria-hidden="true"></i><span>ایمپورت</span>
    </button>
  </div>
</template>

<style scoped>
.inventory-toolbar { --toolbar-h: 2.1rem; align-items: center; display: flex; gap: .65rem; }

.inventory-toolbar__mode {
  align-items: center; background: var(--body-bg); border: 1px solid var(--border-color);
  border-radius: .55rem; box-sizing: border-box; display: flex; gap: .5rem; padding: .25rem .35rem .25rem .6rem;
}
.inventory-toolbar__mode > span { color: var(--text-secondary); font-size: .76rem; white-space: nowrap; }
.inventory-toolbar__mode .form-control { background-color: var(--surface); border: 0; box-shadow: none; min-width: 9rem; }

.inventory-toolbar__group { height: var(--toolbar-h); position: relative; }
.inventory-toolbar__group > .btn:first-of-type { border-radius: 0 .35rem .35rem 0 !important; }
.inventory-toolbar__group > .btn:last-of-type { border-radius: .35rem 0 0 .35rem !important; }
.inventory-toolbar__group > .btn:only-of-type { border-radius: .35rem !important; }

.inventory-toolbar__icon { align-items: center; display: inline-flex; height: 100%; justify-content: center; padding: 0; width: 2.1rem; }
.inventory-toolbar__icon i { font-size: 1.15rem; margin: 0; }
.inventory-toolbar > .mobile-icon-button { align-items: center; display: inline-flex; height: var(--toolbar-h); justify-content: center; }

.inventory-toolbar__menu {
  background: var(--surface); border: 1px solid var(--border-color); border-radius: .65rem;
  box-shadow: 0 .75rem 1.75rem rgba(15, 23, 42, .18); left: 0; max-height: 22rem; min-width: 14rem;
  overflow-y: auto; padding: .4rem; position: absolute; top: calc(100% + .4rem); z-index: 1080;
}
.inventory-toolbar__menu-title {
  border-bottom: 1px solid var(--border-color); color: var(--text-secondary); font-size: .75rem;
  font-weight: 600; margin-bottom: .25rem; padding: .4rem .5rem .55rem;
}
.inventory-toolbar__menu-option {
  align-items: center; border-radius: .4rem; color: var(--text-primary); cursor: pointer; display: flex;
  font-size: .82rem; gap: .55rem; margin: 0; padding: .45rem .5rem; white-space: nowrap;
}
.inventory-toolbar__menu-option:hover { background: var(--primary-light); color: var(--primary-dark); }
.inventory-toolbar__menu-option input { accent-color: var(--primary); flex: 0 0 auto; }

.inventory-mobile-actions { display: none; }

@media (max-width: 767px) {
  .inventory-toolbar { display: grid; gap: .5rem; grid-template-columns: minmax(0, 1fr) auto; width: 100%; }
  .inventory-toolbar__mode { min-width: 0; padding: .2rem .35rem; width: 100%; }
  .inventory-toolbar__mode .form-control { flex: 1 1 auto; min-width: 0; width: 100%; }
  .inventory-toolbar__group { justify-self: end; }
  .inventory-toolbar__import, .inventory-toolbar__export { display: none; }
  .inventory-toolbar__columns { border-radius: .35rem !important; }
  .inventory-toolbar__add { display: none !important; }
  .inventory-toolbar__extra { display: none; }

  .inventory-mobile-actions {
    background: var(--surface); background: color-mix(in srgb, var(--surface) 94%, transparent);
    border-top: 1px solid var(--border-color); bottom: 0; box-shadow: 0 -.5rem 1.5rem rgba(15, 23, 42, .12);
    display: grid; gap: .6rem; grid-auto-columns: minmax(0, 1fr); grid-auto-flow: column; left: 0;
    padding: .65rem .75rem calc(.65rem + env(safe-area-inset-bottom)); position: fixed; right: 0; z-index: 3;
  }
  .inventory-mobile-actions .btn {
    align-items: center; display: inline-flex; font-size: .7rem; gap: .2rem; justify-content: center;
    min-height: 2.2rem; padding: .25rem;
  }
}
@media (max-width: 575px) {
  .inventory-toolbar__mode > span { display: none; }
  .inventory-toolbar__mode .form-control { min-width: 8.25rem; }
}
</style>
