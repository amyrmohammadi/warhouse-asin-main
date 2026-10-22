<script setup lang="ts">
import Swal, { type SweetAlertIcon } from 'sweetalert2'
import 'sweetalert2/dist/sweetalert2.min.css'

interface ConfirmDialogOptions {
  title: string
  text?: string
  icon?: SweetAlertIcon
  confirmButtonText?: string
  cancelButtonText?: string
  confirmButtonColor?: string
}

async function ask(options: ConfirmDialogOptions): Promise<boolean> {
  const result = await Swal.fire({
    title: options.title,
    text: options.text,
    icon: options.icon ?? 'question',
    showCancelButton: true,
    confirmButtonText: options.confirmButtonText ?? 'تأیید',
    cancelButtonText: options.cancelButtonText ?? 'انصراف',
    confirmButtonColor: options.confirmButtonColor ?? '#1eaae7',
    cancelButtonColor: '#6c757d',
    reverseButtons: true,
    focusCancel: true,
    heightAuto: false,
    customClass: {
      popup: 'app-confirm-dialog',
      title: 'app-confirm-dialog__title',
      htmlContainer: 'app-confirm-dialog__text',
    },
  })

  return result.isConfirmed
}

defineExpose({ ask })
</script>

<template>
  <span class="d-none" aria-hidden="true"></span>
</template>

<style>
.app-confirm-dialog {
  background: var(--surface) !important;
  border: 1px solid var(--border-color);
  color: var(--text-primary) !important;
  direction: rtl;
  font-family: inherit;
}

.app-confirm-dialog__title {
  color: var(--text-primary) !important;
  text-align: center;
}

.app-confirm-dialog__text {
  color: var(--text-secondary) !important;
  text-align: center;
}

.app-confirm-dialog .swal2-actions button:focus {
  box-shadow: 0 0 0 3px color-mix(in srgb, var(--primary) 28%, transparent) !important;
}
</style>
