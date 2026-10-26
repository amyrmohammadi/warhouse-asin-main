<script setup lang="ts">
import { nextTick, ref } from 'vue'

import BaseModal from '@/components/BaseModal.vue'
import type { InvoiceDetails, InvoiceItem, InvoiceSummary } from '@/services/invoices'
import { formatPersianDate, formatPersianNumber } from '@/utils/dateTime'

defineProps<{
  modelValue: boolean
  invoice: InvoiceSummary | null
  details: InvoiceDetails | null
  loading: boolean
  error: string | null
  canEdit: boolean
  editingItemId: string | null
  saving: boolean
  editError: string | null
}>()

const emit = defineEmits<{
  'update:modelValue': [value: boolean]
  edit: [item: InvoiceItem]
  'cancel-edit': []
  save: [receiptNumber: string | null]
}>()

const receiptNumber = ref('')
const receiptInputs = ref<HTMLInputElement[]>([])

async function startEdit(item: InvoiceItem): Promise<void> {
  receiptNumber.value = item.receiptNumber ?? ''
  emit('edit', item)
  await nextTick()
  receiptInputs.value[0]?.focus()
  receiptInputs.value[0]?.select()
}

function save(): void {
  const value = receiptNumber.value.trim()
  emit('save', value || null)
}

function cancelEdit(): void {
  receiptNumber.value = ''
  emit('cancel-edit')
}
</script>

<template>
  <BaseModal :model-value="modelValue" size="xl" modal-class="invoice-items-modal" :busy="saving"
             :title="`کالاهای فاکتور ${invoice?.invoiceNumber ?? ''}`"
             @update:model-value="$emit('update:modelValue', $event)">
    <div v-if="invoice" class="invoice-items-modal__summary">
      <div><span>شماره فاکتور</span><strong dir="ltr">{{ invoice.invoiceNumber }}</strong></div>
      <div><span>طرف حساب</span><strong>{{ invoice.counterparty || '—' }}</strong></div>
    </div>

    <div v-if="loading" class="invoice-items-modal__state">
      <span class="spinner-border text-primary" aria-hidden="true"></span>
      <span>در حال دریافت کالاهای فاکتور...</span>
    </div>
    <div v-else-if="error" class="alert alert-danger mb-0" role="alert">{{ error }}</div>
    <div v-else-if="details && details.items.length">
      <div v-if="editError" class="alert alert-danger py-2" role="alert">{{ editError }}</div>
      <div class="table-responsive">
      <table class="table table-sm table-striped invoice-items-table mb-0">
        <thead>
          <tr>
            <th class="text-center">ردیف</th>
            <th>عنوان کالا</th>
            <th>کد کالا</th>
            <th>تعداد</th>
            <th>واحد</th>
            <th>انبار</th>
            <th>تاریخ ورود</th>
            <th>شماره صفحه</th>
            <th>شماره رسید</th>
            <th v-if="canEdit" class="text-center">عملیات</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="(item, index) in details.items" :key="item.id">
            <td class="text-center">{{ formatPersianNumber(index + 1) }}</td>
            <td><strong>{{ item.productTitle }}</strong></td>
            <td><span dir="ltr">{{ item.productCode }}</span></td>
            <td>{{ formatPersianNumber(item.quantity) }}</td>
            <td>{{ item.unitOfMeasureTitle }}</td>
            <td>{{ item.warehouseTitle }}</td>
            <td>{{ formatPersianDate(item.enteredAtUtc) }}</td>
            <td><span dir="ltr">{{ item.pageNumber }}</span></td>
            <td>
              <input v-if="editingItemId === item.id" ref="receiptInputs" v-model="receiptNumber"
                     class="form-control form-control-sm invoice-items-table__receipt-input"
                     type="text" maxlength="100" autocomplete="off" dir="ltr"
                     aria-label="شماره رسید" :disabled="saving"
                     @keydown.enter.prevent="save" @keydown.esc.prevent="cancelEdit">
              <template v-else>
                <span v-if="item.receiptNumber" dir="ltr">{{ item.receiptNumber }}</span>
                <span v-else class="badge badge-light text-muted">ثبت نشده</span>
              </template>
            </td>
            <td v-if="canEdit" class="text-center">
              <div v-if="editingItemId === item.id" class="invoice-items-table__inline-actions">
                <button class="btn btn-sm btn-success" type="button" title="ذخیره شماره رسید"
                        aria-label="ذخیره شماره رسید" :disabled="saving" @click="save">
                  <span v-if="saving" class="spinner-border spinner-border-sm" aria-hidden="true"></span>
                  <i v-else class="las la-check" aria-hidden="true"></i>
                </button>
                <button class="btn btn-sm btn-light" type="button" title="انصراف" aria-label="انصراف"
                        :disabled="saving" @click="cancelEdit">
                  <i class="las la-times" aria-hidden="true"></i>
                </button>
              </div>
              <button v-else class="btn invoice-items-table__edit" type="button"
                      :title="item.receiptNumber ? 'ویرایش شماره رسید' : 'ثبت شماره رسید'"
                      :aria-label="item.receiptNumber ? 'ویرایش شماره رسید' : 'ثبت شماره رسید'"
                      :disabled="saving" @click="startEdit(item)">
                <i class="las la-edit" aria-hidden="true"></i>
              </button>
            </td>
          </tr>
        </tbody>
      </table>
      </div>
    </div>
    <div v-else class="invoice-items-modal__state text-muted">کالایی برای این فاکتور ثبت نشده است.</div>
  </BaseModal>
</template>

<style scoped>
.invoice-items-modal__summary {
  background: var(--body-bg); border: 1px solid var(--border-color); border-radius: .65rem;
  display: grid; gap: .75rem 1.5rem; grid-template-columns: repeat(2, minmax(0, 1fr));
  margin-bottom: 1rem; padding: .85rem 1rem;
}
.invoice-items-modal__summary > div { display: flex; flex-direction: column; gap: .25rem; }
.invoice-items-modal__summary span { color: var(--text-secondary); font-size: .75rem; }
.invoice-items-modal__summary strong { color: var(--text-primary); }
.invoice-items-modal__state { align-items: center; display: flex; gap: .7rem; justify-content: center; min-height: 9rem; }
.invoice-items-table th { white-space: nowrap; }
.invoice-items-table td { vertical-align: middle; }
.invoice-items-table__edit {
  align-items: center; background: rgba(var(--primary-rgb, 89, 59, 219), .1);
  border: 0; border-radius: .4rem; color: var(--primary); display: inline-flex;
  font-size: 1.15rem; height: 2rem; justify-content: center; padding: 0; width: 2rem;
}
.invoice-items-table__edit:hover { background: var(--primary); color: #fff; }
.invoice-items-table__receipt-input { min-width: 8rem; text-align: center; }
.invoice-items-table__inline-actions { display: inline-flex; gap: .3rem; }
.invoice-items-table__inline-actions .btn {
  align-items: center; display: inline-flex; font-size: 1.05rem; height: 2rem;
  justify-content: center; padding: 0; width: 2rem;
}
@media (max-width: 575px) {
  .invoice-items-modal__summary { grid-template-columns: 1fr; }
}
</style>
