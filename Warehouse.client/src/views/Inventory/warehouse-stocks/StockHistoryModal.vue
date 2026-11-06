<script setup lang="ts">
import { onBeforeUnmount, ref } from 'vue'
import BaseModal from '@/components/BaseModal.vue'
import { getStockHistory, type WarehouseStock, type StockHistoryRow } from '@/services/warehouseStocks'
import { formatPersianDate, formatPersianNumber } from '@/utils/dateTime'
import { isAbortError } from '@/composables/useRequestChannel'
import { useUnauthorizedRedirect } from '@/composables/useUnauthorizedRedirect'

const props = defineProps<{ stock: WarehouseStock; warehouseTitle: string }>()
const emit = defineEmits<{ close: [] }>()
const rows = ref<StockHistoryRow[]>([])
const total = ref(0)
const page = ref(1)
const loading = ref(false)
const error = ref('')
const { handleUnauthorized } = useUnauthorizedRedirect('/dashboard/inventory/warehouses')
let controller: AbortController | undefined
const labels: Record<string, string> = {
  GoodsReceipt: 'ورود کالا', GoodsIssue: 'خروج کالا', DepartmentDelivery: 'تحویل به واحد',
  DepartmentReceipt: 'دریافت از واحد', OpeningBalance: 'موجودی اولیه', Adjustment: 'اصلاح موجودی',
  '1': 'ورود کالا', '2': 'خروج کالا', '3': 'تحویل به واحد', '4': 'دریافت از واحد', '5': 'موجودی اولیه', '6': 'اصلاح موجودی',
}
async function load(target: number): Promise<void> {
  controller?.abort()
  const current = new AbortController()
  controller = current
  loading.value = true
  error.value = ''
  try {
    const result = await getStockHistory(props.stock.warehouseId, props.stock.productId, target, current.signal)
    if (current.signal.aborted) return
    rows.value = result.items
    total.value = result.totalCount
    page.value = target
  } catch (failure) {
    if (!isAbortError(failure) && !await handleUnauthorized(failure))
      error.value = failure instanceof Error ? failure.message : 'دریافت تاریخچه انجام نشد.'
  } finally { if (controller === current) loading.value = false }
}
onBeforeUnmount(() => controller?.abort())
void load(1)
</script>

<template>
  <BaseModal :model-value="true" title="تاریخچه موجودی" size="xl" @update:model-value="emit('close')">
    <dl class="history-summary">
      <div><dt>کالا:</dt><dd>{{ stock.productTitle }}</dd></div>
      <div><dt>کد کالا:</dt><dd dir="ltr">{{ stock.productCode }}</dd></div>
      <div><dt>واحد:</dt><dd>{{ stock.unitOfMeasure }}</dd></div>
      <div><dt>انبار:</dt><dd>{{ warehouseTitle }}</dd></div>
    </dl>
    <div v-if="error" class="alert alert-danger" role="alert">{{ error }}
      <button type="button" class="btn btn-link btn-sm" @click="load(page)">تلاش دوباره</button>
    </div>
    <p v-if="loading" role="status">در حال دریافت تاریخچه…</p>
    <div v-else-if="!error" class="table-responsive">
      <table class="table table-sm table-striped history-table">
        <thead><tr><th>تاریخ</th><th>نوع عملیات</th><th>شماره صفحه ورود</th><th>تأثیر</th><th>توضیحات</th></tr></thead>
        <tbody>
          <tr v-for="row in rows" :key="row.id">
            <td class="history-datetime">{{ formatPersianDate(row.date, '—') }}</td>
            <td>{{ labels[row.movementType] || 'گردش موجودی' }}<span v-if="row.isReversal" class="badge badge-warning ml-1">برگشت عملیات</span></td>
            <td dir="ltr">{{ row.pageNumber || '—' }}</td>
            <td><span class="history-impact" dir="ltr"
                      :class="{ 'history-impact--positive': row.quantityDelta > 0, 'history-impact--negative': row.quantityDelta < 0 }">
              {{ row.quantityDelta > 0 ? '+' : '' }}{{ formatPersianNumber(row.quantityDelta) }}
            </span></td>
            <td class="history-description">{{ row.description || '—' }}</td>
          </tr>
          <tr v-if="!rows.length"><td colspan="5" class="text-center text-muted">گردشی برای این کالا در این انبار ثبت نشده است.</td></tr>
        </tbody>
      </table>
    </div>
    <template #footer>
      <button type="button" class="btn btn-light btn-sm" :disabled="loading || page <= 1" @click="load(page - 1)">قبلی</button>
      <span>صفحه {{ formatPersianNumber(page) }} از {{ formatPersianNumber(Math.max(1, Math.ceil(total / 20))) }}</span>
      <button type="button" class="btn btn-light btn-sm" :disabled="loading || page * 20 >= total" @click="load(page + 1)">بعدی</button>
      <button type="button" class="btn btn-light" @click="emit('close')">بستن</button>
    </template>
  </BaseModal>
</template>

<style scoped>
.history-summary {
  background: #f2f3ff; border: 1px solid var(--border-color); border-radius: .65rem;
  display: grid; grid-template-columns: 2fr 1fr 1fr 1.5fr; gap: .75rem 1.5rem;
  margin-bottom: calc(2.25rem + 30px); padding: .5rem .75rem;
}
:global(body[data-app-theme='dark'] .history-summary) { background: var(--body-bg, transparent); }
.history-summary > div { display: flex; align-items: baseline; gap: .4rem; min-width: 0; }
.history-summary dt { flex-shrink: 0; color: var(--text-secondary); font-size: .75rem; font-weight: normal; }
.history-summary dd {
  margin: 0; overflow-wrap: anywhere; color: var(--text-primary); font-size: 1rem;
  font-family: "iransans-bold", sans-serif; font-weight: 700; line-height: 1.8;
}
.history-summary > div:first-child dd { font-size: 1.125rem; }
@media (max-width: 767px) { .history-summary { grid-template-columns: repeat(2, minmax(0, 1fr)); } }
@media (max-width: 399px) { .history-summary { grid-template-columns: minmax(0, 1fr); } }
.history-description { min-width: 10rem; white-space: pre-wrap; overflow-wrap: anywhere; }
.history-table th, .history-table td { padding: .35rem .5rem; vertical-align: middle; }
.history-datetime { white-space: nowrap; }
.history-impact { display: inline-block; border-radius: .35rem; padding: .15rem .45rem; white-space: nowrap; }
.history-impact--positive { background: rgba(40, 167, 69, .14); color: var(--success, #218838); }
.history-impact--negative { background: rgba(220, 53, 69, .14); color: var(--danger, #c82333); }
</style>
