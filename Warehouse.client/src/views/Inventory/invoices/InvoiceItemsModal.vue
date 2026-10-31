<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import DatePicker from 'vue3-persian-datetime-picker'

import BaseModal from '@/components/BaseModal.vue'
import SupplierSelect from '@/components/selectors/SupplierSelect.vue'
import type { InvoiceDetails, InvoiceProcessingStatus, UpdateInvoiceRequest } from '@/services/invoices'
import { formatPersianDate, formatPersianNumber } from '@/utils/dateTime'
import InvoiceStatusChip from './InvoiceStatusChip.vue'

const props = defineProps<{
  modelValue: boolean
  details: InvoiceDetails | null
  loading: boolean
  error: string | null
  canEdit: boolean
  canEditInvoice: boolean
  canEditReceipt: boolean
  canReopen: boolean
  canApprove: boolean
  editMode: boolean
  saving: boolean
}>()

const emit = defineEmits<{
  'update:modelValue': [value: boolean]
  'update:editMode': [value: boolean]
  save: [values: UpdateInvoiceRequest]
  'change-status': [status: InvoiceProcessingStatus]
}>()

const form = reactive({
  invoiceNumber: '',
  supplierId: null as string | null,
  supplierText: '',
  description: '',
  invoiceDate: '',
  financeDeliveryDate: '',
  processingStatus: 'UnderReview' as InvoiceProcessingStatus,
  price: null as number | null,
  items: [] as Array<{ id: string; receiptNumber: string; price: number | null; goodsReceiptConcurrencyStamp: string }>,
})
const formError = ref('')
const showItemPrices = false
const manualPrice = ref(false)
const calculatedPrice = computed(() => {
  if (!form.items.some(item => item.price != null)) return null
  const total = form.items.reduce((sum, item) => sum + (item.price ?? 0), 0)
  return Math.round((total + Number.EPSILON) * 100) / 100
})

function setItemPrice(index: number, event: Event): void {
  const input = event.target as HTMLInputElement
  form.items[index].price = input.value === '' ? null : input.valueAsNumber
  if (!manualPrice.value) form.price = calculatedPrice.value
}

function setInvoicePrice(event: Event): void {
  const input = event.target as HTMLInputElement
  manualPrice.value = input.value !== ''
  form.price = manualPrice.value ? input.valueAsNumber : calculatedPrice.value
}

function useCalculatedPrice(): void {
  manualPrice.value = false
  form.price = calculatedPrice.value
}

watch([() => props.editMode, () => props.details], ([editing, details]) => {
  if (!editing || !details) return
  formError.value = ''
  form.invoiceNumber = details.invoiceNumber
  form.supplierId = details.supplierId
  form.supplierText = details.counterparty ?? ''
  form.description = details.description ?? ''
  form.invoiceDate = details.invoiceDate ?? ''
  form.financeDeliveryDate = details.financeDeliveryDate ?? ''
  form.processingStatus = details.processingStatus
  form.items = details.items.map(item => ({
    id: item.id,
    receiptNumber: item.receiptNumber ?? '',
    price: item.price,
    goodsReceiptConcurrencyStamp: item.goodsReceiptConcurrencyStamp,
  }))
  form.price = details.price
  manualPrice.value = details.price != null && details.price !== calculatedPrice.value
})

function submit(): void {
  if (!props.details || props.details.isLocked || !props.editMode || props.saving) return
  formError.value = ''
  if (props.canEditInvoice && !form.invoiceNumber.trim()) {
    formError.value = 'شماره فاکتور الزامی است.'
    return
  }
  if (props.canEditInvoice && !form.supplierId && form.supplierText.trim()) {
    formError.value = 'طرف حساب را از فهرست انتخاب کنید یا کادر را خالی بگذارید.'
    return
  }
  emit('save', {
    price: props.canEditInvoice ? form.price : props.details.price,
    invoiceNumber: props.canEditInvoice ? form.invoiceNumber.trim() : props.details.invoiceNumber,
    supplierId: props.canEditInvoice ? form.supplierId : props.details.supplierId,
    description: props.canEditInvoice ? (form.description.trim() || null) : props.details.description,
    invoiceDate: props.canEditInvoice ? (form.invoiceDate || null) : props.details.invoiceDate,
    financeDeliveryDate: props.canEditInvoice ? (form.financeDeliveryDate || null) : props.details.financeDeliveryDate,
    processingStatus: props.canEditInvoice ? form.processingStatus : props.details.processingStatus,
    items: form.items.map(item => ({
      id: item.id,
      price: props.canEditInvoice ? item.price : (props.details?.items.find(value => value.id === item.id)?.price ?? null),
      receiptNumber: props.canEditReceipt ? (item.receiptNumber.trim() || null) :
        (props.details?.items.find(value => value.id === item.id)?.receiptNumber ?? null),
      goodsReceiptConcurrencyStamp: item.goodsReceiptConcurrencyStamp,
    })),
  })
}
</script>

<template>
  <BaseModal :model-value="modelValue" size="xl" modal-class="invoice-items-modal" :busy="saving"
             :title="`${editMode ? 'ویرایش' : 'مشاهده'} فاکتور ${details?.invoiceNumber ?? ''}`"
             @update:model-value="$emit('update:modelValue', $event)">
    <div v-if="loading" class="invoice-items-modal__state">
      <span class="spinner-border text-primary" aria-hidden="true"></span>
      <span>در حال دریافت فاکتور...</span>
    </div>
    <div v-else-if="!details" class="alert alert-danger mb-0" role="alert">{{ error || 'فاکتور پیدا نشد.' }}</div>
    <form v-else id="invoice-details-form" @submit.prevent="submit">
      <div v-if="error" class="alert alert-danger py-2" role="alert">{{ error }}</div>
      <div v-if="formError" class="alert alert-danger py-2" role="alert">{{ formError }}</div>
      <div v-if="details.isLocked" class="alert alert-info py-2" role="status">
        <i class="las la-lock" aria-hidden="true"></i>
        فاکتور و ورودی‌های مرتبط قفل هستند. برای ویرایش، ابتدا با مجوز برگشت وضعیت، فاکتور را به «در حال رسیدگی» برگردانید.
      </div>

      <div v-if="editMode" class="row">
        <div class="col-md-3 form-group">
          <label for="invoice-number">شماره فاکتور</label>
          <input id="invoice-number" v-model="form.invoiceNumber" class="form-control" type="text"
                 maxlength="100" required dir="ltr" :disabled="saving" :readonly="!canEditInvoice" />
        </div>
        <div class="col-md-3 form-group">
          <SupplierSelect class="invoice-items-modal__supplier" :model-value="form.supplierId"
                          :text="form.supplierText" label="طرف حساب"
                          placeholder="انتخاب طرف حساب" :disabled="saving || !canEditInvoice"
                          @update:model-value="form.supplierId = $event"
                          @update:text="form.supplierText = $event" />
        </div>
        <div class="col-md-3 form-group">
          <label>تاریخ فاکتور</label>
          <DatePicker v-model="form.invoiceDate" type="date" format="YYYY-MM-DD"
                      display-format="jYYYY/jMM/jDD" placeholder="تاریخ فاکتور را انتخاب کنید"
                      color="#2563eb" :editable="canEditInvoice" :clearable="canEditInvoice"
                      :disabled="saving || !canEditInvoice" />
        </div>
        <div class="col-md-3 form-group">
          <label for="invoice-processing-status">وضعیت فاکتور</label>
          <select id="invoice-processing-status" v-model="form.processingStatus" class="form-control"
                  :disabled="saving || !canEditInvoice">
            <option value="UnderReview">در حال رسیدگی</option>
            <option value="SentToFinance">ارسال به واحد مالی</option>
            <option value="FinanceApproved">تأیید واحد مالی</option>
          </select>
        </div>
        <div class="col-md-4 form-group">
          <label>تاریخ تحویل به مالی</label>
          <DatePicker v-model="form.financeDeliveryDate" type="date" format="YYYY-MM-DD"
                      display-format="jYYYY/jMM/jDD" placeholder="انتخاب تاریخ تحویل به مالی"
                      color="#2563eb" :editable="canEditInvoice" :clearable="canEditInvoice"
                      :disabled="saving || !canEditInvoice" />
        </div>
        <div class="col-md-4 form-group">
          <label for="invoice-price">قیمت فاکتور</label>
          <input id="invoice-price" :value="form.price" class="form-control" type="number"
                 min="0" max="9999999999999999.99" step="0.01" dir="ltr"
                 :disabled="saving" :readonly="!canEditInvoice" @input="setInvoicePrice" />
          <small class="text-muted d-block">قیمت فاکتور را می‌توانید دستی وارد یا تغییر دهید.</small>
          <small v-if="showItemPrices" class="text-muted d-block">جمع قیمت کل اقلام: {{ calculatedPrice == null ? '' : formatPersianNumber(calculatedPrice) }}</small>
          <button v-if="showItemPrices && canEditInvoice" type="button" class="btn btn-link btn-sm p-0" :disabled="saving"
                  @click="useCalculatedPrice">محاسبه از جمع اقلام</button>
        </div>
        <div class="col-12 form-group">
          <label for="invoice-description">توضیحات</label>
          <textarea id="invoice-description" v-model="form.description" class="form-control"
                    rows="2" maxlength="1000" :disabled="saving" :readonly="!canEditInvoice"></textarea>
        </div>
      </div>
      <div v-else class="invoice-items-modal__summary">
        <div><span>شماره فاکتور</span><strong dir="ltr">{{ details.invoiceNumber }}</strong></div>
        <div><span>طرف حساب</span><strong>{{ details.counterparty || '' }}</strong></div>
        <div><span>تاریخ تحویل به مالی</span><strong>{{ formatPersianDate(details.financeDeliveryDate) }}</strong></div>
        <div><span>تاریخ فاکتور</span><strong>{{ formatPersianDate(details.invoiceDate) }}</strong></div>
        <div><span>وضعیت فاکتور</span><InvoiceStatusChip :status="details.processingStatus" /></div>
        <div><span>قیمت فاکتور</span><strong>{{ details.price == null ? '' : formatPersianNumber(details.price) }}</strong></div>
        <div class="invoice-items-modal__description"><span>توضیحات</span><strong>{{ details.description || '' }}</strong></div>
      </div>

      <h6 class="mb-2">اقلام و رسیدهای فاکتور</h6>
      <div v-if="!details.items.length" class="invoice-items-modal__state text-muted">کالایی برای این فاکتور ثبت نشده است.</div>
      <div v-else class="table-responsive">
        <table class="table table-sm table-striped invoice-items-table mb-0">
          <thead>
            <tr>
              <th class="text-center">ردیف</th><th>عنوان کالا</th><th>کد کالا</th><th>تعداد</th>
              <th>واحد</th><th v-if="showItemPrices">قیمت کل قلم</th><th>انبار</th><th>تاریخ ورود</th><th>شماره صفحه</th><th>شماره رسید</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="(item, index) in details.items" :key="item.id">
              <td class="text-center">{{ formatPersianNumber(index + 1) }}</td>
              <td><strong>{{ item.productTitle }}</strong></td>
              <td dir="ltr">{{ item.productCode }}</td>
              <td>{{ formatPersianNumber(item.quantity) }}</td>
              <td>{{ item.unitOfMeasureTitle }}</td>
              <td v-if="showItemPrices">
                <input v-if="editMode" :value="form.items[index].price"
                       class="form-control form-control-sm invoice-items-table__price-input"
                       type="number" min="0" max="9999999999999999.99" step="0.01" dir="ltr"
                       :disabled="saving" :readonly="!canEditInvoice" :aria-label="`قیمت کل قلم ردیف ${index + 1}`"
                       @input="setItemPrice(index, $event)" />
                <span v-else>{{ item.price == null ? '' : formatPersianNumber(item.price) }}</span>
              </td>
              <td>{{ item.warehouseTitle }}</td>
              <td>{{ formatPersianDate(item.enteredAtUtc) }}</td>
              <td dir="ltr">{{ item.pageNumber }}</td>
              <td>
                <input v-if="editMode" v-model="form.items[index].receiptNumber"
                       class="form-control form-control-sm invoice-items-table__receipt-input"
                       type="text" maxlength="100" dir="ltr" :disabled="saving" :readonly="!canEditReceipt"
                       :aria-label="`شماره رسید ردیف ${index + 1}`" />
                <span v-else-if="item.receiptNumber" dir="ltr">{{ item.receiptNumber }}</span>
                
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </form>
    <template #footer="{ close }">
      <button v-if="editMode" class="btn btn-light" type="button" :disabled="saving"
              @click="$emit('update:editMode', false)">انصراف از ویرایش</button>
      <button v-else class="btn btn-light" type="button" @click="close">بستن</button>
      <button v-if="editMode" class="btn btn-primary" type="submit" form="invoice-details-form" :disabled="saving">
        <span v-if="saving" class="spinner-border spinner-border-sm mr-1" aria-hidden="true"></span>
        ذخیره تغییرات
      </button>
      <button v-if="!editMode && details?.isLocked && canReopen" class="btn btn-outline-warning" type="button" :disabled="saving"
              @click="$emit('change-status', 'UnderReview')">برگشت به در حال رسیدگی</button>
      <button v-if="!editMode && details?.processingStatus === 'SentToFinance' && canApprove" class="btn btn-success" type="button" :disabled="saving"
              @click="$emit('change-status', 'FinanceApproved')">تأیید واحد مالی</button>
      <button v-if="!editMode && canEdit && details && !details.isLocked" class="btn btn-primary" type="button"
              @click="$emit('update:editMode', true)">ویرایش</button>
    </template>
  </BaseModal>
</template>

<style scoped>
.invoice-items-modal__summary {
  background: #f2f3ff; border: 1px solid var(--border-color); border-radius: .65rem;
  display: grid; gap: .75rem 1.5rem; grid-template-columns: repeat(4, minmax(0, 1fr));
  margin-bottom: 1rem; padding: .85rem 1rem;
}
:global(body[data-app-theme='dark'] .invoice-items-modal__summary) { background: var(--body-bg, transparent); }
.invoice-items-modal__summary > div { display: flex; flex-direction: column; gap: .25rem; }
.invoice-items-modal__summary span { color: var(--text-secondary); font-size: .75rem; }
.invoice-items-modal__summary strong { color: var(--text-primary); }
.invoice-items-modal__description { grid-column: 1 / -1; white-space: pre-wrap; }
.invoice-items-modal__state { align-items: center; display: flex; gap: .7rem; justify-content: center; min-height: 9rem; }
.invoice-items-table th { white-space: nowrap; }
.invoice-items-table td { vertical-align: middle; }
.invoice-items-table__receipt-input { min-width: 8rem; text-align: center; }
.invoice-items-table__price-input { min-width: 9rem; }
.invoice-items-modal__supplier :deep(.supplier-select__label) {
  color: inherit;
  font-size: inherit;
  font-weight: 400;
  line-height: inherit;
  margin-bottom: .5rem;
  padding: 0;
}
.invoice-items-modal__supplier :deep(.supplier-select__control .form-control) {
  font-family: inherit;
  font-size: .875rem;
  font-weight: 400;
  height: calc(1.5em + .75rem + 2px);
  line-height: 1.5;
  padding: .375rem .75rem;
}
.invoice-items-modal__supplier :deep(.supplier-select__arrow) { display: none; }
.invoice-items-modal__supplier :deep(.supplier-select__option) {
  font-size: .875rem;
  padding: .375rem .75rem;
}
@media (max-width: 575px) {
  .invoice-items-modal__summary { grid-template-columns: 1fr; }
}
@media (min-width: 576px) and (max-width: 991px) {
  .invoice-items-modal__summary { grid-template-columns: repeat(2, minmax(0, 1fr)); }
}
</style>
