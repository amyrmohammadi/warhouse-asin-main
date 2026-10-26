<script setup lang="ts">
import { nextTick, onBeforeUnmount, ref, watch } from 'vue'

import { useModalStack } from '@/composables/useModalStack'

const props = withDefaults(defineProps<{
  modelValue: boolean
  title?: string
  size?: 'sm' | 'md' | 'lg' | 'xl'
  /** با کلیک روی پس‌زمینه یا Esc بسته نشود. */
  persistent?: boolean
  /** در حال ذخیره: بستن ممنوع. */
  busy?: boolean
  /** مثلاً تأیید «تغییرات ذخیره‌نشده». اگر false برگرداند مودال بسته نمی‌شود. */
  closeGuard?: () => boolean | Promise<boolean>
  /** کلاس اضافه روی ریشه برای استایل مخصوص هر صفحه. */
  modalClass?: string
}>(), { size: 'md' })

const emit = defineEmits<{
  'update:modelValue': [value: boolean]
  closed: []
}>()

const FOCUSABLE =
  'a[href],button:not([disabled]),input:not([disabled]):not([type=hidden]),select:not([disabled]),textarea:not([disabled]),[tabindex]:not([tabindex="-1"])'

const dialog = ref<HTMLElement | null>(null)
const titleId = `modal-title-${Math.random().toString(36).slice(2, 9)}`
const { push, remove, isTop } = useModalStack()
let previouslyFocused: HTMLElement | null = null

async function requestClose(): Promise<void> {
  if (props.busy) return
  if (props.closeGuard && !(await props.closeGuard())) return
  emit('update:modelValue', false)
}

function onBackdrop(): void {
  if (!props.persistent) void requestClose()
}

function onKeydown(event: KeyboardEvent): void {
  if (!isTop.value) return
  if (event.key === 'Escape') {
    if (!props.persistent) { event.stopPropagation(); void requestClose() }
    return
  }
  if (event.key !== 'Tab' || !dialog.value) return
  const items = [...dialog.value.querySelectorAll<HTMLElement>(FOCUSABLE)]
  if (!items.length) return
  const first = items[0]
  const last = items[items.length - 1]
  if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last.focus() }
  else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus() }
}

function release(): void {
  document.removeEventListener('keydown', onKeydown)
  remove()
}

watch(() => props.modelValue, async open => {
  if (open) {
    previouslyFocused = document.activeElement as HTMLElement | null
    push()
    document.addEventListener('keydown', onKeydown)
    await nextTick()
    dialog.value?.querySelector<HTMLElement>('[autofocus],input:not([readonly]),textarea,select')?.focus()
  } else {
    release()
    previouslyFocused?.focus()
    emit('closed')
  }
}, { immediate: true })

onBeforeUnmount(release)
</script>

<template>
  <Teleport to="body">
    <template v-if="modelValue">
      <div class="modal show d-block"
           :class="modalClass"
           tabindex="-1"
           role="dialog"
           aria-modal="true"
           :aria-labelledby="titleId"
           @mousedown.self="onBackdrop">
        <div ref="dialog"
             class="modal-dialog modal-dialog-centered modal-dialog-scrollable"
             :class="size !== 'md' && `modal-${size}`"
             role="document">
          <div class="modal-content">
            <div class="modal-header">
              <slot name="header">
                <h5 :id="titleId" class="modal-title">{{ title }}</h5>
              </slot>
              <button type="button" class="close" aria-label="بستن" :disabled="busy" @click="requestClose">
                <span aria-hidden="true">&times;</span>
              </button>
            </div>
            <slot name="content">
              <div class="modal-body"><slot /></div>
              <div v-if="$slots.footer" class="modal-footer"><slot name="footer" :close="requestClose" /></div>
            </slot>
          </div>
        </div>
      </div>
      <div class="modal-backdrop show"></div>
    </template>
  </Teleport>
</template>
