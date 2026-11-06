<script setup lang="ts">
import { computed, nextTick, ref, useId, watch } from 'vue'

const props = defineProps<{
  modelValue: string
  options: Array<{ id: string; title: string }>
  disabled?: boolean
  placeholder?: string
}>()
const emit = defineEmits<{ 'update:modelValue': [value: string] }>()
const open = ref(false)
const root = ref<HTMLElement | null>(null)
const trigger = ref<HTMLButtonElement | null>(null)
const menuId = useId()
const title = computed(() => props.options.find(option => option.id === props.modelValue)?.title)
watch(() => props.disabled, disabled => { if (disabled) open.value = false })
function choose(value: string): void {
  emit('update:modelValue', value)
  open.value = false
  trigger.value?.focus()
}
function leave(event: FocusEvent): void {
  if (!root.value?.contains(event.relatedTarget as Node | null)) open.value = false
}
async function navigate(event: KeyboardEvent): Promise<void> {
  if (event.key === 'Escape') {
    event.preventDefault()
    open.value = false
    trigger.value?.focus()
  }
  if (!['ArrowDown', 'ArrowUp', 'Home', 'End'].includes(event.key)) return
  event.preventDefault()
  if (props.disabled) return
  open.value = true
  await nextTick()
  const buttons = Array.from(root.value?.querySelectorAll<HTMLButtonElement>('[role="menuitemradio"]') ?? [])
  const current = buttons.indexOf(document.activeElement as HTMLButtonElement)
  const index = event.key === 'Home' ? 0 : event.key === 'End' ? buttons.length - 1
    : (current + (event.key === 'ArrowDown' ? 1 : -1) + buttons.length) % buttons.length
  buttons[index]?.focus()
}
</script>

<template>
  <div ref="root" class="warehouse-select" @focusout="leave" @keydown="navigate">
    <button ref="trigger" type="button" class="custom-select custom-select-sm warehouse-select__trigger"
            aria-label="انبار" aria-haspopup="menu" :aria-expanded="open" :aria-controls="menuId"
            :disabled="disabled" @click="open = !open">
      {{ title || placeholder || 'انتخاب انبار' }}
    </button>
    <div v-if="open" :id="menuId" class="warehouse-select__menu" role="menu" aria-label="انبار">
      <button type="button" role="menuitemradio" :aria-checked="!modelValue" @click="choose('')">انتخاب انبار</button>
      <button v-for="option in options" :key="option.id" type="button" role="menuitemradio"
              :aria-checked="option.id === modelValue" @click="choose(option.id)">{{ option.title }}</button>
    </div>
  </div>
</template>

<style scoped>
.warehouse-select { position: relative; width: 100%; font-family: "iransans-medium", sans-serif; }
.warehouse-select__trigger { width: 100%; height: 2.25rem; text-align: right; font-family: inherit; }
.warehouse-select__menu {
  position: absolute; z-index: 1060; top: 100%; right: 0; left: 0; margin-top: .25rem;
  max-height: 16rem; overflow-y: auto; padding: .25rem;
  background: var(--surface, white); border: 1px solid var(--border-color); border-radius: .35rem;
  box-shadow: 0 .4rem 1rem rgba(0, 0, 0, .12);
}
.warehouse-select__menu button {
  display: block; width: 100%; border: 0; background: transparent; padding: .5rem .75rem;
  text-align: right; font-family: inherit; font-size: .8125rem; color: var(--text-primary);
}
.warehouse-select__menu button:hover, .warehouse-select__menu button:focus-visible,
.warehouse-select__menu button[aria-checked="true"] { background: var(--primary-light); color: var(--primary); }
</style>
