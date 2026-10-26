<script setup lang="ts">
import { ref } from 'vue'
import DatePicker from 'vue3-persian-datetime-picker'

import BaseModal from '@/components/BaseModal.vue'
import ConfirmDialog from '@/components/ConfirmDialog.vue'
import EntryItemsHeader from '@/components/EntryItemsHeader.vue'
import GoodsIssueDestinationSelect from '@/components/selectors/GoodsIssueDestinationSelect.vue'
import PersonSelect, { type PersonSelectOption } from '@/components/selectors/PersonSelect.vue'
import ProductSelect from '@/components/selectors/ProductSelect.vue'
import { entryFieldAttrs } from '@/utils/entryFocus'

import { useGoodsIssueForm, type ConfirmOptions } from './useGoodsIssueForm'

defineProps<{ people: PersonSelectOption[] }>()
const emit = defineEmits<{ saved: [reason: 'saved' | 'deleted'] }>()
const confirmDialog = ref<{ ask: (options: ConfirmOptions) => Promise<boolean> } | null>(null)

const {
  permissions, open, editingId, saving, formError, form, errors, rows, mode,
  setMode, addRow, removeRow, itemHasContent, isNewIssuer, isNewDestination,
  rowError, clearRowError, openCreate, openEdit, save, remove, removeDocument,
  selectProduct, onProductText, clearProduct,
  selectDestination, onDestinationText, clearDestination,
  advanceFromProductTitle, advanceFromProductCode,
} = useGoodsIssueForm({
  confirm: options => confirmDialog.value?.ask(options) ?? Promise.resolve(false),
  onSaved: reason => emit('saved', reason),
})
const { canDelete, canManageMasterData } = permissions

defineExpose({ openCreate, openEdit, removeDocument })
</script>

<template>
  <ConfirmDialog ref="confirmDialog" />

  <BaseModal v-model="open" size="xl" persistent :busy="saving" modal-class="goods-issue-modal"
             :title="editingId ? 'ویرایش خروج کالا' : 'ثبت خروج کالا'">
    <form id="goods-issue-form" novalidate @submit.prevent="save">
      <div v-if="formError" class="alert alert-danger py-2">{{ formError }}</div>

      <div class="row">
        <div class="col-md-4 form-group">
          <label>کد یا شماره خروج <span class="text-danger">*</span></label>
          <input v-model="form.issueNumber" class="form-control" :class="{ 'is-invalid': errors.get('issueNumber') }"
                 maxlength="100" :disabled="saving" @input="errors.clear('issueNumber')" />
          <small v-if="errors.get('issueNumber')" class="invalid-feedback d-block">{{ errors.get('issueNumber') }}</small>
        </div>
        <div class="col-md-4 form-group">
          <label>تاریخ خروج <span class="text-danger">*</span></label>
          <DatePicker v-model="form.issuedAtLocal" type="date" format="YYYY-MM-DDTHH:mm:ss"
                      display-format="jYYYY/jMM/jDD HH:mm" placeholder="تاریخ و ساعت را انتخاب کنید"
                      :editable="true" :clearable="true" :disabled="saving" color="#2563eb"
                      :class="{ 'is-invalid': errors.get('issuedAtLocal') }"
                      @input="errors.clear('issuedAtLocal')" />
          <small v-if="errors.get('issuedAtLocal')" class="invalid-feedback d-block">{{ errors.get('issuedAtLocal') }}</small>
        </div>
        <div class="col-md-4 form-group">
          <PersonSelect :model-value="form.issuerId || null" :text="form.issuerText" :options="people"
                        label="خارج‌کننده" placeholder="انتخاب یا ورود خارج‌کننده" required
                        :allow-create="canManageMasterData" :disabled="saving" :error="errors.get('issuer')"
                        @update:model-value="form.issuerId = $event ?? ''"
                        @update:text="form.issuerText = $event; errors.clear('issuer')"
                        @select="errors.clear('issuer')" @clear="errors.clear('issuer')" />
          <small v-if="isNewIssuer()" class="goods-issue-new-master-data"
                 :class="{ 'goods-issue-new-master-data--denied': !canManageMasterData }">
            <i :class="canManageMasterData ? 'las la-plus-circle' : 'las la-exclamation-circle'" aria-hidden="true"></i>
            {{ canManageMasterData ? 'خارج‌کننده جدید هنگام ثبت اضافه می‌شود.' : 'مجوز افزودن خارج‌کننده جدید را ندارید؛ از فهرست انتخاب کنید.' }}
          </small>
        </div>
        <div class="col-12 form-group">
          <label>توضیحات کلی</label>
          <textarea v-model="form.description" class="form-control" rows="2" maxlength="1000"
                    :class="{ 'is-invalid': errors.get('description') }" :disabled="saving"
                    placeholder="توضیحات کلی سند خروج" @input="errors.clear('description')"></textarea>
          <small v-if="errors.get('description')" class="invalid-feedback d-block">{{ errors.get('description') }}</small>
        </div>
      </div>

      <EntryItemsHeader title="اقلام خروج" :mode="mode" :disabled="saving" @update:mode="setMode" @add="addRow" />
      <small v-if="errors.get('rows')" class="invalid-feedback d-block mb-2">{{ errors.get('rows') }}</small>

      <div v-if="mode === 'list'" class="goods-issue-list-entry">
        <div class="goods-issue-list-table-scroll">
          <table class="goods-issue-list-table table-striped" aria-label="ثبت جدولی اقلام خروج">
            <thead>
              <tr>
                <th class="goods-issue-list-row-number">#</th>
                <th class="goods-issue-list-product-cell">عنوان کالا</th>
                <th class="goods-issue-list-code-cell">کد کالا <span class="text-danger">*</span></th>
                <th class="goods-issue-list-product-unit-cell">واحد کالا</th>
                <th class="goods-issue-list-quantity-cell">تعداد <span class="text-danger">*</span></th>
                <th class="goods-issue-list-asset-cell">شماره اموال</th>
                <th class="goods-issue-list-destination-cell">مقصد <span class="text-danger">*</span></th>
                <th class="goods-issue-list-reason-cell">علت خروج <span class="text-danger">*</span></th>
                <th class="goods-issue-list-description-cell">توضیحات</th>
                <th class="goods-issue-list-delivering-unit-cell">واحد تحویل‌دهنده <span class="text-danger">*</span></th>
                <th class="goods-issue-list-return-cell">تاریخ عودت</th>
                <th class="goods-issue-list-action-column" aria-label="عملیات"></th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(item, index) in rows" :key="item.key"
                  :class="{ 'goods-issue-list-row--empty': !itemHasContent(item) }">
                <td class="goods-issue-list-row-number">{{ index + 1 }}</td>
                <td class="goods-issue-list-product-cell">
                  <ProductSelect :model-value="item.productId" :text="item.productTitle" size="sm"
                                 search-by="title" placeholder="" aria-label="عنوان کالا" :disabled="saving"
                                 :error="rowError(item, 'product')"
                                 @update:model-value="item.productId = $event"
                                 @update:text="onProductText(item, 'title', $event)"
                                 @select="selectProduct(item, $event)" @clear="clearProduct(item, 'title')"
                                 @enter="advanceFromProductTitle(item)" />
                </td>
                <td class="goods-issue-list-code-cell">
                  <ProductSelect :model-value="item.productId" :text="item.productCode" size="sm"
                                 search-by="code" placeholder="" aria-label="کد کالا" :disabled="saving"
                                 :error="rowError(item, 'code')" v-bind="entryFieldAttrs(item.key, 'code')"
                                 @update:model-value="item.productId = $event"
                                 @update:text="onProductText(item, 'code', $event)"
                                 @select="selectProduct(item, $event)" @clear="clearProduct(item, 'code')"
                                 @enter="advanceFromProductCode(item)" />
                </td>
                <td class="goods-issue-list-product-unit-cell">
                  <input :value="item.productUnitOfMeasureTitle" class="form-control form-control-sm"
                         aria-label="واحد کالا" readonly />
                </td>
                <td class="goods-issue-list-quantity-cell">
                  <input v-model.number="item.quantity" class="form-control form-control-sm"
                         :class="{ 'is-invalid': rowError(item, 'quantity') }" type="number" min="0" step="any"
                         inputmode="decimal" aria-label="تعداد" :disabled="saving"
                         v-bind="entryFieldAttrs(item.key, 'quantity')" @input="clearRowError(item, 'quantity')" />
                  <small v-if="rowError(item, 'quantity')" class="invalid-feedback d-block goods-issue-cell-error">{{ rowError(item, 'quantity') }}</small>
                </td>
                <td class="goods-issue-list-asset-cell">
                  <input v-model="item.assetNumber" class="form-control form-control-sm" maxlength="100"
                         aria-label="شماره اموال" :disabled="saving" />
                </td>
                <td class="goods-issue-list-destination-cell">
                  <GoodsIssueDestinationSelect :model-value="item.destinationId" :text="item.destinationTitle"
                                               size="sm" placeholder="" aria-label="مقصد"
                                               :allow-create="canManageMasterData" :disabled="saving"
                                               :error="rowError(item, 'destination')"
                                               @update:model-value="item.destinationId = $event"
                                               @update:text="onDestinationText(item, $event)"
                                               @select="selectDestination(item, $event)" @clear="clearDestination(item)" />
                  <small v-if="isNewDestination(item) && !canManageMasterData && !rowError(item, 'destination')"
                         class="goods-issue-cell-note goods-issue-new-master-data--denied">بدون مجوز افزودن</small>
                </td>
                <td class="goods-issue-list-reason-cell">
                  <textarea v-model="item.exitReason" class="form-control form-control-sm goods-issue-list-textarea"
                            :class="{ 'is-invalid': rowError(item, 'exitReason') }" rows="1" maxlength="1000"
                            aria-label="علت خروج" :disabled="saving" @input="clearRowError(item, 'exitReason')"></textarea>
                </td>
                <td class="goods-issue-list-description-cell">
                  <textarea v-model="item.description" class="form-control form-control-sm goods-issue-list-textarea"
                            rows="1" maxlength="1000" aria-label="توضیحات" :disabled="saving"></textarea>
                </td>
                <td class="goods-issue-list-delivering-unit-cell">
                  <input v-model="item.deliveringUnit" class="form-control form-control-sm" maxlength="200"
                         :class="{ 'is-invalid': rowError(item, 'deliveringUnit') }" aria-label="واحد تحویل‌دهنده"
                         :disabled="saving" @input="clearRowError(item, 'deliveringUnit')" />
                </td>
                <td class="goods-issue-list-return-cell">
                  <DatePicker v-if="item.productCode.trim()" v-model="item.returnAtLocal" type="date"
                              format="YYYY-MM-DD" display-format="jYYYY/jMM/jDD" locale="fa"
                              placeholder="انتخاب تاریخ" :editable="true" :clearable="true" :auto-submit="true"
                              :disabled="saving" color="#2563eb" @input="clearRowError(item, 'returnAtLocal')" />
                  <small v-if="rowError(item, 'returnAtLocal')" class="invalid-feedback d-block goods-issue-cell-error">{{ rowError(item, 'returnAtLocal') }}</small>
                </td>
                <td class="goods-issue-list-action-column">
                  <button class="master-data-row-action goods-issue-remove-item" type="button" title="پاک‌کردن ردیف"
                          aria-label="پاک‌کردن ردیف" :disabled="saving" @click="removeRow(index)">
                    <i class="las la-trash"></i>
                  </button>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <div v-else class="goods-issue-items">
        <div v-for="(item, index) in rows" :key="item.key" class="goods-issue-item">
          <div class="goods-issue-item__head">
            <strong>ردیف {{ index + 1 }}</strong>
            <button class="btn btn-sm btn-link text-danger" type="button" :disabled="saving"
                    title="حذف ردیف" @click="removeRow(index)"><i class="las la-trash"></i></button>
          </div>
          <div class="goods-issue-item__grid">
            <div class="form-group">
              <label>کد کالا <span class="text-danger">*</span></label>
              <ProductSelect :model-value="item.productId" :text="item.productCode" search-by="code"
                             placeholder="ورود یا انتخاب کد" :disabled="saving" :error="rowError(item, 'code')"
                             @update:model-value="item.productId = $event"
                             @update:text="onProductText(item, 'code', $event)" @select="selectProduct(item, $event)"
                             @clear="clearProduct(item, 'code')" @enter="advanceFromProductCode(item)" />
            </div>
            <div class="form-group">
              <label>تعداد <span class="text-danger">*</span></label>
              <input v-model.number="item.quantity" class="form-control" :class="{ 'is-invalid': rowError(item, 'quantity') }"
                     type="number" min="0" step="any" inputmode="decimal" :disabled="saving"
                     v-bind="entryFieldAttrs(item.key, 'quantity')" @input="clearRowError(item, 'quantity')" />
              <small v-if="rowError(item, 'quantity')" class="invalid-feedback d-block">{{ rowError(item, 'quantity') }}</small>
            </div>
            <div class="form-group"><label>شماره اموال</label><input v-model="item.assetNumber" class="form-control" maxlength="100" :disabled="saving" /></div>
            <div class="form-group">
              <GoodsIssueDestinationSelect :model-value="item.destinationId" :text="item.destinationTitle"
                                           label="مقصد" placeholder="تایپ یا انتخاب مقصد" required
                                           :allow-create="canManageMasterData" :disabled="saving"
                                           :error="rowError(item, 'destination')"
                                           @update:model-value="item.destinationId = $event"
                                           @update:text="onDestinationText(item, $event)"
                                           @select="selectDestination(item, $event)" @clear="clearDestination(item)" />
              <small v-if="isNewDestination(item)" :class="canManageMasterData ? 'text-primary' : 'text-danger'">
                {{ canManageMasterData ? 'مقصد جدید هنگام ثبت اضافه می‌شود.' : 'مقصد را از فهرست انتخاب کنید.' }}
              </small>
            </div>
            <div class="form-group">
              <label>واحد تحویل‌دهنده <span class="text-danger">*</span></label>
              <input v-model="item.deliveringUnit" class="form-control"
                     :class="{ 'is-invalid': rowError(item, 'deliveringUnit') }" maxlength="200" :disabled="saving"
                     @input="clearRowError(item, 'deliveringUnit')" />
              <small v-if="rowError(item, 'deliveringUnit')" class="invalid-feedback d-block">{{ rowError(item, 'deliveringUnit') }}</small>
            </div>
            <div class="form-group"><label>تاریخ عودت</label>
              <DatePicker v-model="item.returnAtLocal" type="date" format="YYYY-MM-DD" display-format="jYYYY/jMM/jDD"
                          :editable="true" :clearable="true" :disabled="saving" color="#2563eb"
                          @input="clearRowError(item, 'returnAtLocal')" />
              <small v-if="rowError(item, 'returnAtLocal')" class="invalid-feedback d-block">{{ rowError(item, 'returnAtLocal') }}</small>
            </div>
            <div class="form-group goods-issue-item__wide">
              <label>علت خروج <span class="text-danger">*</span></label>
              <textarea v-model="item.exitReason" class="form-control" :class="{ 'is-invalid': rowError(item, 'exitReason') }"
                        rows="2" maxlength="1000" :disabled="saving" @input="clearRowError(item, 'exitReason')"></textarea>
              <small v-if="rowError(item, 'exitReason')" class="invalid-feedback d-block">{{ rowError(item, 'exitReason') }}</small>
            </div>
            <div class="form-group goods-issue-item__wide"><label>توضیحات</label><textarea v-model="item.description" class="form-control" rows="2" maxlength="1000" :disabled="saving"></textarea></div>
          </div>
        </div>
      </div>
    </form>

    <template #footer="{ close: requestClose }">
      <button v-if="editingId && canDelete" class="btn btn-outline-danger px-4" type="button"
              :disabled="saving" @click="remove"><i class="las la-trash ml-1" aria-hidden="true"></i>حذف خروج</button>
      <button class="btn btn-light px-4" type="button" :disabled="saving" @click="requestClose">انصراف</button>
      <button class="btn btn-primary px-4" type="submit" form="goods-issue-form" :disabled="saving">
        <span v-if="saving" class="spinner-border spinner-border-sm ml-1" aria-hidden="true"></span>
        {{ editingId ? 'ذخیره تغییرات' : 'ثبت خروج کالا' }}
      </button>
    </template>
  </BaseModal>
</template>

<style>
.goods-issue-modal .modal-dialog
 { margin: 1.5rem auto;
   max-height: 85vh; 
   max-width: 1450px; 
   width: calc(100vw - 3rem);
   }
@media (max-width: 575px) {
  .goods-issue-modal .modal-dialog 
  { 
    margin: .25rem auto;
     max-width: calc(100vw - .5rem);
      width: calc(100vw - .5rem);
     }
}
</style>

<style scoped src="./goodsIssueForm.css"></style>
