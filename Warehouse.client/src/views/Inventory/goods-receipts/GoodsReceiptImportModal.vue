<script setup lang="ts">
import { ref } from 'vue'

import BaseModal from '@/components/BaseModal.vue'
import { isAbortError, useRequestChannel } from '@/composables/useRequestChannel'
import { useUnauthorizedRedirect } from '@/composables/useUnauthorizedRedirect'
import { importGoodsReceipts, type GoodsReceiptImportSummary } from '@/services/goodsReceipts'
import { toast } from '@/services/toast'

const props = defineProps<{ modelValue: boolean; file: File | null }>()
const emit = defineEmits<{ 'update:modelValue': [open: boolean]; imported: [summary: GoodsReceiptImportSummary] }>()

const importing = ref(false)
const channel = useRequestChannel()
const { handleUnauthorized } = useUnauthorizedRedirect('/dashboard/inventory/goods-receipts')

async function start(): Promise<void> {
  if (!props.file || importing.value) return
  const controller = channel.begin()
  importing.value = true
  try {
    const summary = await importGoodsReceipts(props.file, controller.signal)
    emit('update:modelValue', false)
    emit('imported', summary)
  } catch (error) {
    if (isAbortError(error)) return
    if (await handleUnauthorized(error)) return
    toast.error(error instanceof Error ? error.message : 'ایمپورت ورود کالا انجام نشد.')
  } finally {
    if (channel.isCurrent(controller)) { channel.end(controller); importing.value = false }
  }
}
</script>

<template>
  <BaseModal :model-value="modelValue" title="ایمپورت ورود کالا" :busy="importing"
             @update:model-value="emit('update:modelValue', $event)">
    <p>فایل انتخاب‌شده: <bdi>{{ file?.name }}</bdi></p>
    <p>ردیف‌های دارای انبار و شمارهٔ صفحهٔ یکسان، یک سند می‌شوند. تاریخ، تحویل‌دهنده و اطلاعات رسید و فاکتور آن‌ها باید یکسان باشد.</p>
    <p class="mb-0">فقط تحویل‌دهندهٔ جدید ساخته می‌شود؛ سایر مفروضات باید از قبل ثبت شده باشند. در صورت وجود خطا، کل فایل ثبت نمی‌شود.</p>
    <template #footer="{ close }">
      <button type="button" class="btn btn-light" :disabled="importing" @click="close">انصراف</button>
      <button type="button" class="btn btn-primary" :disabled="importing" @click="start">
        <span v-if="importing" class="spinner-border spinner-border-sm ml-1" aria-hidden="true"></span>
        {{ importing ? 'در حال بررسی و ثبت...' : 'شروع ایمپورت' }}
      </button>
    </template>
  </BaseModal>
</template>
