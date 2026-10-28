<script setup lang="ts">
import { ref } from 'vue'
import DatePicker from 'vue3-persian-datetime-picker'

import BaseModal from '@/components/BaseModal.vue'
import ConfirmDialog from '@/components/ConfirmDialog.vue'
import EntryItemsHeader from '@/components/EntryItemsHeader.vue'
import PersonSelect, { type PersonSelectOption } from '@/components/selectors/PersonSelect.vue'
import ProductSelect from '@/components/selectors/ProductSelect.vue'
import SupplierSelect from '@/components/selectors/SupplierSelect.vue'
import UnitSelect from '@/components/selectors/UnitSelect.vue'
import type { UnitOfMeasure } from '@/services/unitsOfMeasure'
import { entryFieldAttrs } from '@/utils/entryFocus'

import { useGoodsReceiptForm, type ConfirmOptions } from './useGoodsReceiptForm'
import type { WarehouseOption } from './useGoodsReceiptOptions'

defineProps<{
  people: PersonSelectOption[]
  warehouses: WarehouseOption[]
  units: UnitOfMeasure[]
}>()
const emit = defineEmits<{ saved: [reason: 'saved' | 'deleted'] }>()

const confirmDialog = ref<{ ask: (options: ConfirmOptions) => Promise<boolean> } | null>(null)

const {
  permissions, open, editingId, saving, formError, form, errors, rows, mode,
  setMode, addRow, removeRow, itemHasContent, isNewDeliverer, isNewProduct, isNewSupplier, hasInvoiceNumber,
  rowError, clearRowError, openCreate, openEdit, close, save, remove,
  selectProduct, onProductText, clearProduct, onUnitText, onInvoiceStatusChange,
  advanceFromProductTitle, advanceFromProductCode, advanceFromUnit,
} = useGoodsReceiptForm({
  confirm: options => confirmDialog.value?.ask(options) ?? Promise.resolve(false),
  onSaved: reason => emit('saved', reason),
})
const { canInitialEntry, canManageInvoice, canManageReceipt, canManageMasterData, canCreateProducts } = permissions

const productNotFoundText = canCreateProducts
  ? 'کالایی پیدا نشد؛ اطلاعات کالای جدید را تکمیل کنید.'
  : 'کالایی پیدا نشد؛ مجوز افزودن کالای جدید را ندارید.'

defineExpose({ openCreate, openEdit })
</script>

<template>
  <ConfirmDialog ref="confirmDialog" />

  <BaseModal v-model="open" size="xl" persistent :busy="saving" modal-class="goods-receipt-modal"
             :title="editingId ? 'ویرایش ورود کالا' : 'ثبت ورود کالا'">
    <form id="goods-receipt-form" novalidate @submit.prevent="save">
      <div v-if="formError" class="alert alert-danger py-2">{{ formError }}</div>

      <div class="row goods-receipt-header-fields">
        <div class="col-md-3 form-group">
          <label>تاریخ و ساعت ورود <span class="text-danger">*</span></label>
          <DatePicker v-model="form.enteredAtLocal" type="date" format="YYYY-MM-DDTHH:mm:ss"
                      display-format="jYYYY/jMM/jDD HH:mm" placeholder="تاریخ و ساعت را انتخاب کنید"
                      color="#2563eb" :editable="true" :clearable="true" :disabled="saving"
                      :class="{ 'is-invalid': errors.get('enteredAtLocal') }"
                      @input="errors.clear('enteredAtLocal')" />
          <small v-if="errors.get('enteredAtLocal')" class="invalid-feedback d-block">{{ errors.get('enteredAtLocal') }}</small>
        </div>
        <div class="col-md-3 form-group">
          <label>شماره صفحه <span class="text-danger">*</span></label>
          <input v-model="form.pageNumber" class="form-control" :class="{ 'is-invalid': errors.get('pageNumber') }"
                 type="text" maxlength="100" :disabled="saving" @input="errors.clear('pageNumber')" />
          <small v-if="errors.get('pageNumber')" class="invalid-feedback d-block">{{ errors.get('pageNumber') }}</small>
        </div>
        <div class="col-md-3 form-group">
          <PersonSelect :model-value="form.delivererId || null" :text="form.delivererText" :options="people"
                        label="تحویل‌دهنده" placeholder="تایپ یا انتخاب تحویل‌دهنده" :disabled="saving"
                        :allow-create="canManageMasterData" :error="errors.get('deliverer')" required
                        @update:model-value="form.delivererId = $event ?? ''"
                        @update:text="form.delivererText = $event; errors.clear('deliverer')"
                        @select="errors.clear('deliverer')" @clear="errors.clear('deliverer')" />
          <small v-if="isNewDeliverer()" class="receipt-new-master-data"
                 :class="{ 'receipt-new-master-data--denied': !canManageMasterData }">
            <i :class="canManageMasterData ? 'las la-plus-circle' : 'las la-exclamation-circle'" aria-hidden="true"></i>
            {{ canManageMasterData ? 'تحویل‌دهنده جدید هنگام ثبت اضافه می‌شود.' : 'مجوز افزودن تحویل‌دهنده جدید را ندارید؛ از فهرست انتخاب کنید.' }}
          </small>
        </div>
        <div class="col-md-6 form-group">
          <label>توضیحات</label>
          <textarea v-model="form.description" class="form-control" :class="{ 'is-invalid': errors.get('description') }"
                    rows="2" maxlength="1000" :disabled="saving" @input="errors.clear('description')"></textarea>
          <small v-if="errors.get('description')" class="invalid-feedback d-block">{{ errors.get('description') }}</small>
        </div>
      </div>

      <EntryItemsHeader title="کالاها و مقدار" :mode="mode" :disabled="saving" @update:mode="setMode" @add="addRow" />
      <small v-if="errors.get('rows')" class="invalid-feedback d-block mb-2">{{ errors.get('rows') }}</small>

      <!-- ───────────── حالت جدولی ───────────── -->
      <div v-if="mode === 'list'" class="receipt-list-entry">
        <div class="receipt-list-table-scroll">
          <table class="receipt-list-table table-striped" aria-label="ثبت لیستی کالاها">
            <thead>
              <tr>
                <th class="receipt-list-row-number">#</th>
                <th class="receipt-list-product-cell">عنوان کالا <span class="text-danger">*</span></th>
                <th class="receipt-list-code-cell">کد کالا <span class="text-danger">*</span></th>
                <th class="receipt-list-unit-cell">واحد <span class="text-danger">*</span></th>
                <th class="receipt-list-quantity-cell">مقدار <span class="text-danger">*</span></th>
                <th class="receipt-list-warehouse-cell">انبار <span class="text-danger">*</span></th>
                <th class="receipt-list-invoice-status-cell">فاکتور</th>
                <th class="receipt-list-invoice-number-cell">شماره فاکتور</th>
                <th class="receipt-list-supplier-cell">طرف حساب</th>
                <th class="receipt-list-receipt-number-cell">شماره رسید</th>
                <th class="receipt-list-issue-number-cell">شماره خروج</th>
                <th class="receipt-list-request-cell">شماره درخواست</th>
                <th class="receipt-list-description-cell">توضیحات کالا</th>
                <th class="receipt-list-action-column" aria-label="عملیات"></th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(item, index) in rows" :key="item.key"
                  :class="{ 'receipt-list-row--empty': !itemHasContent(item), 'receipt-list-row--new-product': isNewProduct(item) }">
                <td class="receipt-list-row-number">{{ index + 1 }}</td>

                <td class="receipt-list-product-cell">
                  <ProductSelect :model-value="item.productId" :text="item.productText" size="sm" search-by="title"
                                 placeholder="" aria-label="عنوان کالا" :not-found-text="productNotFoundText"
                                 :error="rowError(item, 'product')" error-class="receipt-list-cell-error" :disabled="saving"
                                 @update:model-value="item.productId = $event"
                                 @update:text="onProductText(item, 'title', $event)"
                                 @select="selectProduct(item, $event)" @clear="clearProduct(item, 'title')"
                                 @enter="advanceFromProductTitle(item)" />
                  <small v-if="isNewProduct(item) && !canCreateProducts && !rowError(item, 'product')"
                         class="receipt-list-cell-note receipt-new-master-data--denied">بدون مجوز افزودن</small>
                </td>

                <td class="receipt-list-code-cell">
                  <ProductSelect :model-value="item.productId" :text="item.productCode" size="sm" search-by="code"
                                 placeholder="" aria-label="کد کالا" :not-found-text="productNotFoundText"
                                 :error="rowError(item, 'code')" error-class="receipt-list-cell-error" :disabled="saving"
                                 v-bind="entryFieldAttrs(item.key, 'code')"
                                 @update:model-value="item.productId = $event"
                                 @update:text="onProductText(item, 'code', $event)"
                                 @select="selectProduct(item, $event)" @clear="clearProduct(item, 'code')"
                                 @enter="advanceFromProductCode(item)" />
                </td>

                <td class="receipt-list-unit-cell">
                  <UnitSelect :model-value="item.unitOfMeasureId" :text="item.unitOfMeasureTitle" :units="units"
                              size="sm" borderless aria-label="واحد اندازه‌گیری" :readonly="Boolean(item.productId)"
                              :disabled="saving" :error="rowError(item, 'unit')"
                              v-bind="entryFieldAttrs(item.key, 'unit')"
                              @update:model-value="item.unitOfMeasureId = $event"
                              @update:text="onUnitText(item, $event)" @enter="advanceFromUnit(item)" />
                  <small v-if="rowError(item, 'unit')" class="invalid-feedback d-block receipt-list-cell-error">{{ rowError(item, 'unit') }}</small>
                  <small v-else-if="!item.productId && item.unitOfMeasureTitle.trim() && !item.unitOfMeasureId"
                         class="receipt-list-cell-note receipt-new-master-data--denied">واحد نامعتبر</small>
                </td>

                <td class="receipt-list-quantity-cell">
                  <input v-model.number="item.quantity" class="form-control form-control-sm"
                         :class="{ 'is-invalid': rowError(item, 'quantity') }" type="number" step="any" inputmode="decimal"
                         aria-label="مقدار" :disabled="saving" v-bind="entryFieldAttrs(item.key, 'quantity')"
                         @input="clearRowError(item, 'quantity')" />
                  <small v-if="rowError(item, 'quantity')" class="invalid-feedback d-block receipt-list-cell-error">{{ rowError(item, 'quantity') }}</small>
                </td>

                <td class="receipt-list-warehouse-cell">
                  <select v-model="item.warehouseId" class="form-control form-control-sm"
                          :class="{ 'is-invalid': rowError(item, 'warehouse') }" aria-label="انبار"
                          :disabled="saving || !canInitialEntry" @change="clearRowError(item, 'warehouse')">
                    <option value="" aria-label="بدون انتخاب"></option>
                    <option v-for="warehouse in warehouses" :key="warehouse.id" :value="warehouse.id">{{ warehouse.title }}</option>
                  </select>
                  <small v-if="rowError(item, 'warehouse')" class="invalid-feedback d-block receipt-list-cell-error">{{ rowError(item, 'warehouse') }}</small>
                </td>

                <td class="receipt-list-invoice-status-cell">
                  <select v-model="item.invoiceStatus" class="form-control form-control-sm" aria-label="وضعیت فاکتور"
                          :disabled="saving || !canManageInvoice" @change="onInvoiceStatusChange(item)">
                    <option :value="null" aria-label="بدون انتخاب"></option>
                    <option value="DoesNotHave">ندارد</option>
                    <option value="HasInvoice">دارد</option>
                  </select>
                </td>

                <td class="receipt-list-invoice-number-cell">
                  <input v-model="item.invoiceNumber" class="form-control form-control-sm"
                         :class="{ 'is-invalid': rowError(item, 'invoiceNumber') }" type="text" maxlength="100"
                         aria-label="شماره فاکتور" :disabled="saving || !canManageInvoice || !hasInvoiceNumber(item)"
                         @input="clearRowError(item, 'invoiceNumber')" />
                  <small v-if="rowError(item, 'invoiceNumber')" class="invalid-feedback d-block receipt-list-cell-error">{{ rowError(item, 'invoiceNumber') }}</small>
                </td>

                <td class="receipt-list-supplier-cell">
                  <SupplierSelect :model-value="item.supplierId || null" :text="item.supplierText" size="sm" placeholder="‌"
                                  :error="rowError(item, 'supplier')" error-class="receipt-list-cell-error"
                                  :disabled="saving || !canManageInvoice" allow-create
                                  @update:model-value="item.supplierId = $event ?? ''"
                                  @update:text="item.supplierText = $event; clearRowError(item, 'supplier')"
                                  @clear="clearRowError(item, 'supplier')" />
                  <small v-if="isNewSupplier(item) && !canManageMasterData"
                         class="receipt-list-cell-note receipt-new-master-data--denied">بدون مجوز افزودن</small>
                </td>

                <td class="receipt-list-receipt-number-cell">
                  <input v-model="item.receiptNumber" class="form-control form-control-sm" type="text" maxlength="100"
                         aria-label="شماره رسید" :disabled="saving" :readonly="!canManageReceipt" />
                </td>

                <td class="receipt-list-issue-number-cell">
                  <input v-model="item.issueNumber" class="form-control form-control-sm" type="text" maxlength="100"
                         aria-label="شماره خروج" :disabled="saving || !canInitialEntry"
                         :class="{ 'is-invalid': rowError(item, 'issueNumber') }" @input="clearRowError(item, 'issueNumber')" />
                </td>

                <td class="receipt-list-request-cell">
                  <input v-model="item.requestNumber" class="form-control form-control-sm"
                         :class="{ 'is-invalid': rowError(item, 'requestNumber') }" type="text" maxlength="100"
                         aria-label="شماره درخواست" :disabled="saving" @input="clearRowError(item, 'requestNumber')" />
                  <small v-if="rowError(item, 'requestNumber')" class="invalid-feedback d-block receipt-list-cell-error">{{ rowError(item, 'requestNumber') }}</small>
                </td>

                <td class="receipt-list-description-cell">
                  <textarea v-model="item.description" class="form-control form-control-sm receipt-list-description-input"
                            :class="{ 'is-invalid': rowError(item, 'description') }" rows="1" maxlength="1000"
                            aria-label="توضیحات کالا" :disabled="saving" @input="clearRowError(item, 'description')"></textarea>
                  <small v-if="rowError(item, 'description')" class="invalid-feedback d-block receipt-list-cell-error">{{ rowError(item, 'description') }}</small>
                </td>

                <td class="receipt-list-action-column">
                  <button class="master-data-row-action receipt-remove-item" type="button" title="پاک‌کردن ردیف"
                          aria-label="پاک‌کردن ردیف" :disabled="saving" @click="removeRow(index)">
                    <i class="las la-trash"></i>
                  </button>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <!-- ───────────── حالت کارتی ───────────── -->
      <template v-else>
        <div v-for="(item, index) in rows" :key="item.key" class="receipt-item-row"
             :class="{ 'receipt-item-row--new-product': isNewProduct(item) }">
          <div class="receipt-item-primary-row">
            <div class="form-group mb-0 receipt-product-field">
              <label>عنوان کالا <span class="text-danger">*</span></label>
              <ProductSelect :model-value="item.productId" :text="item.productText" search-by="title"
                             placeholder="تایپ یا انتخاب کالا" aria-label="عنوان کالا" :not-found-text="productNotFoundText"
                             :error="rowError(item, 'product')" :disabled="saving"
                             @update:model-value="item.productId = $event"
                             @update:text="onProductText(item, 'title', $event)" @select="selectProduct(item, $event)"
                             @clear="clearProduct(item, 'title')" @enter="advanceFromProductTitle(item)" />
            </div>
            <div class="form-group mb-0 receipt-code-field">
              <label>کد کالا <span v-if="!item.productId" class="text-danger">*</span></label>
              <ProductSelect :model-value="item.productId" :text="item.productCode" search-by="code"
                             placeholder="انتخاب کد" aria-label="کد کالا" :not-found-text="productNotFoundText"
                             :error="rowError(item, 'code')" :disabled="saving" v-bind="entryFieldAttrs(item.key, 'code')"
                             @update:model-value="item.productId = $event"
                             @update:text="onProductText(item, 'code', $event)" @select="selectProduct(item, $event)"
                             @clear="clearProduct(item, 'code')" @enter="advanceFromProductCode(item)" />
            </div>
            <div class="form-group mb-0 receipt-unit-field">
              <label>واحد <span v-if="!item.productId" class="text-danger">*</span></label>
              <UnitSelect :model-value="item.unitOfMeasureId" :text="item.unitOfMeasureTitle" :units="units"
                          placeholder="انتخاب واحد" :readonly="Boolean(item.productId)" :disabled="saving"
                          :error="rowError(item, 'unit')"
                          v-bind="entryFieldAttrs(item.key, 'unit')"
                          @update:model-value="item.unitOfMeasureId = $event" @update:text="onUnitText(item, $event)"
                          @enter="advanceFromUnit(item)" />
              <small v-if="rowError(item, 'unit')" class="invalid-feedback d-block">{{ rowError(item, 'unit') }}</small>
              <small v-else-if="!item.productId && item.unitOfMeasureTitle.trim() && !item.unitOfMeasureId"
                     class="receipt-new-master-data receipt-new-master-data--denied">
                واحد جدید قابل افزودن نیست؛ از فهرست واحدهای موجود انتخاب کنید.
              </small>
            </div>
            <div class="form-group mb-0 receipt-quantity-field">
              <label>مقدار <span class="text-danger">*</span></label>
              <input v-model.number="item.quantity" class="form-control" :class="{ 'is-invalid': rowError(item, 'quantity') }"
                     type="number" step="any" inputmode="decimal" :disabled="saving"
                     v-bind="entryFieldAttrs(item.key, 'quantity')" @input="clearRowError(item, 'quantity')" />
              <small v-if="rowError(item, 'quantity')" class="invalid-feedback d-block">{{ rowError(item, 'quantity') }}</small>
            </div>
            <div class="form-group mb-0 receipt-contract-field">
              <label>شماره درخواست</label>
              <input v-model="item.requestNumber" class="form-control" :class="{ 'is-invalid': rowError(item, 'requestNumber') }"
                     type="text" maxlength="100" :disabled="saving" @input="clearRowError(item, 'requestNumber')" />
              <small v-if="rowError(item, 'requestNumber')" class="invalid-feedback d-block">{{ rowError(item, 'requestNumber') }}</small>
            </div>
          </div>

          <div class="receipt-item-details-row">
            <div class="form-group mb-0">
              <label>انبار <span class="text-danger">*</span></label>
              <select v-model="item.warehouseId" class="form-control" :class="{ 'is-invalid': rowError(item, 'warehouse') }"
                      :disabled="saving || !canInitialEntry" @change="clearRowError(item, 'warehouse')">
                <option value="" aria-label="بدون انتخاب"></option>
                <option v-for="warehouse in warehouses" :key="warehouse.id" :value="warehouse.id">{{ warehouse.title }}</option>
              </select>
              <small v-if="rowError(item, 'warehouse')" class="invalid-feedback d-block">{{ rowError(item, 'warehouse') }}</small>
            </div>
            <div class="form-group mb-0">
              <label>وضعیت فاکتور</label>
              <select v-model="item.invoiceStatus" class="form-control" :disabled="saving || !canManageInvoice"
                      @change="onInvoiceStatusChange(item)">
                <option :value="null" aria-label="بدون انتخاب"></option>
                <option value="DoesNotHave">ندارد</option>
                <option value="HasInvoice">دارد</option>
              </select>
            </div>
            <div class="form-group mb-0">
              <label>شماره فاکتور <span v-if="hasInvoiceNumber(item)" class="text-danger">*</span></label>
              <input v-model="item.invoiceNumber" class="form-control" :class="{ 'is-invalid': rowError(item, 'invoiceNumber') }"
                     type="text" maxlength="100" :disabled="saving || !canManageInvoice || !hasInvoiceNumber(item)"
                     @input="clearRowError(item, 'invoiceNumber')" />
              <small v-if="rowError(item, 'invoiceNumber')" class="invalid-feedback d-block">{{ rowError(item, 'invoiceNumber') }}</small>
            </div>
            <div class="form-group mb-0">
              <label>طرف حساب</label>
              <SupplierSelect :model-value="item.supplierId || null" :text="item.supplierText"
                              placeholder="تایپ یا انتخاب تأمین‌کننده" :error="rowError(item, 'supplier')"
                              :disabled="saving || !canManageInvoice" allow-create
                              @update:model-value="item.supplierId = $event ?? ''" @update:text="item.supplierText = $event"
                              @clear="clearRowError(item, 'supplier')" />
            </div>
            <div class="form-group mb-0">
              <label>شماره رسید</label>
              <input v-model="item.receiptNumber" class="form-control" type="text" maxlength="100"
                     :disabled="saving" :readonly="!canManageReceipt" />
            </div>
            <div class="form-group mb-0">
              <label>شماره خروج</label>
              <input v-model="item.issueNumber" class="form-control" type="text" maxlength="100"
                     :disabled="saving || !canInitialEntry" :class="{ 'is-invalid': rowError(item, 'issueNumber') }"
                     @input="clearRowError(item, 'issueNumber')" />
            </div>
          </div>

          <small v-if="isNewProduct(item) && !canCreateProducts"
                 class="receipt-new-master-data receipt-new-master-data--denied receipt-new-product-message">
            <i class="las la-exclamation-circle" aria-hidden="true"></i>
            مجوز افزودن کالای جدید را ندارید؛ از فهرست انتخاب کنید.
          </small>

          <div class="receipt-item-secondary-row">
            <div class="form-group mb-0 receipt-item-description">
              <label>توضیحات کالا</label>
              <input v-model="item.description" class="form-control" :class="{ 'is-invalid': rowError(item, 'description') }"
                     type="text" maxlength="1000" :disabled="saving" @input="clearRowError(item, 'description')" />
              <small v-if="rowError(item, 'description')" class="invalid-feedback d-block">{{ rowError(item, 'description') }}</small>
            </div>
            <div class="receipt-item-actions">
              <button class="master-data-row-action receipt-remove-item" type="button" title="حذف کالا" aria-label="حذف کالا"
                      :disabled="saving || rows.length === 1" @click="removeRow(index)">
                <i class="las la-trash"></i>
              </button>
            </div>
          </div>
        </div>
      </template>
    </form>

    <template #footer="{ close: requestClose }">
      <button v-if="editingId && canInitialEntry" class="btn btn-outline-danger px-4" type="button" :disabled="saving" @click="remove">
        <i class="las la-trash ml-1" aria-hidden="true"></i>حذف ورودی
      </button>
      <button class="btn btn-light px-4" type="button" :disabled="saving" @click="requestClose">انصراف</button>
      <button class="btn btn-primary px-4" type="submit" form="goods-receipt-form" :disabled="saving">
        <span v-if="saving" class="spinner-border spinner-border-sm ml-1" aria-hidden="true"></span>
        {{ editingId ? 'ذخیره تغییرات' : 'ثبت ورود کالا' }}
      </button>
    </template>
  </BaseModal>
</template>

<!-- اندازهٔ مودال: BaseModal ریشهٔ scoped ندارد، پس این بلوک عمداً scoped نیست -->
<style>
.goods-receipt-modal .modal-dialog { margin: 1.5rem auto; max-height: 85vh; max-width: 1450px; width: calc(100vw - 3rem); }
@media (max-width: 575px) {
  .goods-receipt-modal .modal-dialog { margin: .25rem auto; max-width: calc(100vw - .5rem); width: calc(100vw - .5rem); }
}
</style>

<style scoped>
.goods-receipt-header-fields :deep(.person-select__label) {
  color: inherit;
  display: inline-block;
  font-size: inherit;
  font-weight: inherit;
  line-height: inherit;
  margin-bottom: .5rem;
}

/* ───────── جدول ورود سریع (حالت list) ───────── */
.receipt-list-entry {
  background: var(--surface);
  border: 1px solid var(--border-color);
  border-radius: .65rem;
  margin-bottom: .75rem;
}

.receipt-list-table-scroll { overflow-x: auto; overflow-y: visible; width: 100%; }

.receipt-list-table {
  --receipt-row-column-width: 2rem;
  --receipt-product-column-width: 14rem;
  --receipt-code-column-width: 100px;
  --receipt-unit-column-width: 70px;
  --receipt-quantity-column-width: 70px;
  --receipt-warehouse-column-width: 80px;
  --receipt-invoice-status-column-width: 70px;
  --receipt-invoice-number-column-width: 100px;
  --receipt-supplier-column-width: 11rem;
  --receipt-number-column-width: 80px;
  --receipt-issue-number-column-width: 100px;
  --receipt-request-column-width: 80px;
  --receipt-description-column-width: 10rem;
  --receipt-action-column-width: var(--receipt-row-column-width);
  border-collapse: separate;
  border-spacing: 0;
  font-size: .74rem;
  table-layout: fixed;
  min-width: calc(var(--receipt-row-column-width) + var(--receipt-product-column-width) + var(--receipt-code-column-width) + var(--receipt-unit-column-width) + var(--receipt-quantity-column-width) + var(--receipt-warehouse-column-width) + var(--receipt-invoice-status-column-width) + var(--receipt-invoice-number-column-width) + var(--receipt-supplier-column-width) + var(--receipt-number-column-width) + var(--receipt-issue-number-column-width) + var(--receipt-request-column-width) + var(--receipt-description-column-width) + var(--receipt-action-column-width));
  width: 100%;
}

.receipt-list-table th {
  background: var(--body-bg);
  border-bottom: 1px solid var(--border-color);
  border-left: 1px solid var(--border-color);
  color: var(--text-secondary);
  font-size: .69rem;
  font-weight: 700;
  padding: .4rem 0;
  position: sticky;
  text-align: center;
  top: 0;
  white-space: nowrap;
  z-index: 4;
}
.receipt-list-table th:last-child { border-left: 0; }

.receipt-list-table td {
  border-bottom: 1px solid var(--border-color);
  border-left: 1px solid var(--border-color);
  padding: 0;
  position: relative;
  vertical-align: top;
}
.receipt-list-table td:last-child { border-left: 0; }
.receipt-list-table tbody tr:last-child td { border-bottom: 0; }

/* height:1px باعث می‌شود input با height:100% کل ارتفاع سلول را بگیرد */
.receipt-list-table tbody .receipt-list-unit-cell,
.receipt-list-table tbody .receipt-list-quantity-cell,
.receipt-list-table tbody .receipt-list-warehouse-cell,
.receipt-list-table tbody .receipt-list-invoice-status-cell,
.receipt-list-table tbody .receipt-list-invoice-number-cell,
.receipt-list-table tbody .receipt-list-receipt-number-cell,
.receipt-list-table tbody .receipt-list-issue-number-cell,
.receipt-list-table tbody .receipt-list-request-cell {
  height: 1px;
}

/* input/select ساده داخل سلول: بدون کادر، بدون گردی، تمام‌قد */
.receipt-list-table td > .form-control,
.receipt-list-table td > .form-control:disabled,
.receipt-list-table td > .form-control[readonly] {
  background-color: transparent !important;
  border-color: transparent;
  border-radius: 0;
  box-sizing: border-box;
  font-size: .68rem;
  height: 100%;
  margin: 0;
  min-height: 1.85rem;
  min-width: 0;
  opacity: 1;
  padding: .25rem .35rem;
  width: 100%;
}
.receipt-list-table td > .form-control:focus {
  background-color: transparent !important;
  border-color: var(--primary);
  box-shadow: 0 0 0 .12rem rgba(37, 99, 235, .12);
}
.receipt-list-table .form-control.is-invalid {
  border-color: #dc3545;
  box-shadow: inset 0 0 0 1px rgba(220, 53, 69, .18);
}

/* منوی باز هر انتخابگر باید روی ردیف‌های پایین‌تر بیاید */
.receipt-list-table tbody tr:has(.unit-select__menu),
.receipt-list-table tbody tr:has(.supplier-select--open),
.receipt-list-table tbody tr:has(.product-select--open) {
  position: relative;
  z-index: 100068;
}
.receipt-list-table td:has(.unit-select__menu),
.receipt-list-table td:has(.supplier-select--open),
.receipt-list-table td:has(.product-select--open) {
  z-index: 100069;
}

/* عرض ستون‌ها */
.receipt-list-table .receipt-list-product-cell { width: var(--receipt-product-column-width); }
.receipt-list-table .receipt-list-code-cell { width: var(--receipt-code-column-width); }
.receipt-list-table .receipt-list-unit-cell { width: var(--receipt-unit-column-width); }
.receipt-list-table .receipt-list-quantity-cell { width: var(--receipt-quantity-column-width); }
.receipt-list-table .receipt-list-warehouse-cell { width: var(--receipt-warehouse-column-width); }
.receipt-list-table .receipt-list-invoice-status-cell { width: var(--receipt-invoice-status-column-width); }
.receipt-list-table .receipt-list-invoice-number-cell { width: var(--receipt-invoice-number-column-width); }
.receipt-list-table .receipt-list-supplier-cell { width: var(--receipt-supplier-column-width); }
.receipt-list-table .receipt-list-receipt-number-cell { width: var(--receipt-number-column-width); }
.receipt-list-table .receipt-list-issue-number-cell { width: var(--receipt-issue-number-column-width); }
.receipt-list-table .receipt-list-request-cell { width: var(--receipt-request-column-width); }
.receipt-list-table .receipt-list-description-cell { width: auto; }

/*
 * انتخابگر کالا و طرف حساب در جدول بدون کادر و با پس‌زمینهٔ شفاف است.
 * این مقادیر پیش‌فرض کامپوننت‌ها نیستند و از بیرون بازنویسی می‌شوند.
 * (UnitSelect با prop «borderless» خودش همین را انجام می‌دهد.)
 */
.receipt-list-table .receipt-list-product-cell :deep(.product-select),
.receipt-list-table .receipt-list-code-cell :deep(.product-select),
.receipt-list-table .receipt-list-supplier-cell :deep(.supplier-select) {
  --supplier-select-input-background: transparent;
  --supplier-select-input-border-color: transparent;
  --supplier-select-input-border-width: 0;
  --supplier-select-input-radius: 0;
  --product-select-input-background: transparent;
  --product-select-input-border-color: transparent;
  --product-select-input-border-width: 0;
  --product-select-input-radius: 0;
}
.receipt-list-table .receipt-list-code-cell :deep(.product-select) {
  --product-select-menu-width: var(--receipt-product-column-width);
}
.receipt-list-table .receipt-list-product-cell :deep(.product-select__control .form-control),
.receipt-list-table .receipt-list-code-cell :deep(.product-select__control .form-control),
.receipt-list-table .receipt-list-supplier-cell :deep(.supplier-select__control .form-control) {
  font-size: .68rem;
}
.receipt-list-table .receipt-list-product-cell :deep(.product-select .form-control:focus),
.receipt-list-table .receipt-list-code-cell :deep(.product-select .form-control:focus),
.receipt-list-table .receipt-list-supplier-cell :deep(.supplier-select .form-control:focus) {
  background-color: transparent;
}

.receipt-list-row-number {
  color: var(--text-secondary);
  text-align: center !important;
  width: var(--receipt-row-column-width);
}
.receipt-list-action-column {
  background: var(--surface);
  text-align: center !important;
  width: var(--receipt-action-column-width);
}
.receipt-list-action-column .master-data-row-action { margin-inline: auto; }

/* textarea توضیحات: رشد خودکار با field-sizing (در مرورگرهای قدیمی‌تر یک‌خطی با اسکرول می‌ماند) */
.receipt-list-table td > .receipt-list-description-input {
  box-sizing: border-box;
  field-sizing: content;
  height: auto;
  line-height: 1.35;
  min-height: 1.85rem !important;
  overflow: hidden;
  resize: none;
  white-space: pre-wrap;
}

.receipt-list-row--empty { opacity: .68; }
.receipt-list-row--empty:focus-within { opacity: 1; }
.receipt-list-row--new-product td { background-color: rgba(245, 158, 11, .12); }
:global(body[data-app-theme='dark'] .receipt-list-row--new-product td) { background-color: rgba(251, 191, 36, .11); }

.receipt-list-cell-note {
  color: var(--primary);
  display: block;
  font-size: .67rem;
  line-height: 1.2;
  margin: .2rem .35rem 0;
}
.receipt-list-cell-error { font-size: .65rem; line-height: 1.2; margin: .15rem .3rem .25rem; }

/* ───────── حالت کارتی ───────── */
.receipt-item-row {
  padding: .75rem;
  margin-bottom: .65rem;
  border: 1px solid var(--border, #e6e6e6);
  border-radius: .65rem;
  background-color: #fbfbfb;
}
.receipt-item-row--new-product { background-color: rgba(245, 158, 11, .12); border-color: rgba(245, 158, 11, .35); }
:global(body[data-app-theme='dark'] .receipt-item-row) { background-color: #1e2536; border-color: #334155; color: #f1f5f9; }
:global(body[data-app-theme='dark'] .receipt-item-row--new-product) { background-color: rgba(251, 191, 36, .11); border-color: rgba(251, 191, 36, .3); }

.receipt-item-primary-row,
.receipt-item-secondary-row {
  display: grid;
  grid-template-columns: minmax(13rem, 1fr) minmax(6.5rem, .25fr) minmax(6.5rem, .25fr) minmax(5.5rem, .2fr) minmax(9rem, .38fr);
  gap: .65rem;
}
.receipt-item-details-row {
  display: grid;
  gap: .65rem;
  grid-template-columns: repeat(5, minmax(8rem, 1fr));
  margin-top: .65rem;
}
.receipt-item-primary-row { align-items: start; }
.receipt-item-secondary-row { margin-top: .65rem; align-items: end; }
.receipt-item-description { grid-column: 1; }
.receipt-item-actions { grid-column: 5; align-self: end; justify-self: end; padding-bottom: .15rem; }

.receipt-code-field :deep(input),
.receipt-unit-field :deep(input),
.receipt-quantity-field input { text-align: center; }
.receipt-code-field :deep(input[readonly]),
.receipt-unit-field :deep(input[readonly]) { background-color: rgba(0, 0, 0, .035); }

/* ───────── مشترک ───────── */
.receipt-new-master-data {
  align-items: center;
  color: var(--primary);
  display: flex;
  font-size: .76rem;
  gap: .3rem;
  margin-top: .35rem;
}
.receipt-new-master-data--denied { color: var(--error, #dc3545); }
.receipt-new-product-message { margin-bottom: -.25rem; margin-top: .55rem; }

.receipt-remove-item { color: #dc3545 !important; border-color: rgba(220, 53, 69, .45) !important; }
.receipt-remove-item:hover:not(:disabled) { color: #fff !important; background-color: #dc3545 !important; border-color: #dc3545 !important; }

@media (max-width: 991px) {
  .receipt-item-primary-row, .receipt-item-secondary-row { grid-template-columns: minmax(0, 1fr) minmax(7rem, .35fr); }
  .receipt-item-details-row { grid-template-columns: repeat(2, minmax(0, 1fr)); }
  .receipt-item-description { grid-column: 1; }
  .receipt-item-actions { grid-column: 2; }
}
@media (max-width: 575px) {
  .receipt-item-primary-row { grid-template-columns: 1fr; }
  .receipt-item-details-row { grid-template-columns: 1fr; }
  .receipt-item-secondary-row { grid-template-columns: minmax(0, 1fr) auto; }
  .receipt-item-description { grid-column: 1; }
  .receipt-item-actions { grid-column: 2; }
}
</style>
