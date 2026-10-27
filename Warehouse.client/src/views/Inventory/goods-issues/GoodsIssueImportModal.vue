<script setup lang="ts">
import { ref } from 'vue'

import BaseModal from '@/components/BaseModal.vue'
import { isAbortError, useRequestChannel } from '@/composables/useRequestChannel'
import { useUnauthorizedRedirect } from '@/composables/useUnauthorizedRedirect'
import { importGoodsIssues, type GoodsIssueImportSummary } from '@/services/goodsIssues'
import { toast } from '@/services/toast'

const props = defineProps<{ modelValue: boolean; file: File | null }>()
const emit = defineEmits<{ 'update:modelValue': [open: boolean]; imported: [summary: GoodsIssueImportSummary] }>()
const importing = ref(false)
const channel = useRequestChannel()
const { handleUnauthorized } = useUnauthorizedRedirect('/dashboard/inventory/goods-issues')

async function start(): Promise<void> {
  if (!props.file || importing.value) return
  const controller = channel.begin()
  importing.value = true
  try {
    const summary = await importGoodsIssues(props.file, controller.signal)
    emit('update:modelValue', false)
    emit('imported', summary)
  } catch (error) {
    if (isAbortError(error)) return
    if (await handleUnauthorized(error)) return
    toast.error(error instanceof Error ? error.message : 'ایمپورت خروج کالا انجام نشد.')
  } finally {
    if (channel.isCurrent(controller)) { channel.end(controller); importing.value = false }
  }
}
</script>

<template>
  <BaseModal :model-value="modelValue" title="ایمپورت خروج کالا" :busy="importing"
             @update:model-value="emit('update:modelValue', $event)">
    <p>فایل انتخاب‌شده: <bdi>{{ file?.name }}</bdi></p>
    <p>تمام شیت‌ها خوانده می‌شوند. ردیف‌های دارای شمارهٔ خروج یکسان در یک سند قرار می‌گیرند و ترتیب آن‌ها حفظ می‌شود.</p>
    <p class="mb-0">کالا و واحد باید از قبل ثبت شده باشند. مقصدِ واردشده نیز باید موجود باشد؛ خانه‌های خالی همان‌طور خالی می‌مانند. خارج‌کنندهٔ جدید ساخته می‌شود. اگر حتی یک ردیف نامعتبر باشد، هیچ سندی ثبت نمی‌شود.</p>
    <template #footer="{ close }">
      <button type="button" class="btn btn-light" :disabled="importing" @click="close">انصراف</button>
      <button type="button" class="btn btn-primary" :disabled="importing" @click="start">
        <span v-if="importing" class="spinner-border spinner-border-sm ml-1" aria-hidden="true"></span>
        {{ importing ? 'در حال بررسی و ثبت...' : 'شروع ایمپورت' }}
      </button>
    </template>
  </BaseModal>
</template>
