<script setup lang="ts">
import { dismissToast, toastMessages, type ToastType } from '@/services/toast'

defineOptions({ name: 'ToastContainer' })

const iconClasses: Record<ToastType, string> = {
  success: 'las la-check-circle',
  error: 'las la-times-circle',
  warning: 'las la-exclamation-triangle',
  info: 'las la-info-circle',
}
</script>

<template>
  <Teleport to="body">
    <section class="app-toast-container" aria-live="polite" aria-relevant="additions">
      <TransitionGroup name="app-toast-list">
        <article
          v-for="item in toastMessages"
          :key="item.id"
          class="app-toast"
          :class="`app-toast--${item.type}`"
          :role="item.type === 'error' ? 'alert' : 'status'"
          :style="{ '--app-toast-duration': `${item.duration}ms` }"
          aria-atomic="true"
        >
          <i class="app-toast__icon" :class="iconClasses[item.type]" aria-hidden="true"></i>
          <div class="app-toast__content">
            <strong class="app-toast__title">{{ item.title }}</strong>
            <p class="app-toast__message">{{ item.message }}</p>
          </div>
          <button
            class="app-toast__close"
            type="button"
            aria-label="بستن اعلان"
            @click="dismissToast(item.id)"
          >
            <i class="las la-times" aria-hidden="true"></i>
          </button>
          <span class="app-toast__progress" aria-hidden="true"></span>
        </article>
      </TransitionGroup>
    </section>
  </Teleport>
</template>
