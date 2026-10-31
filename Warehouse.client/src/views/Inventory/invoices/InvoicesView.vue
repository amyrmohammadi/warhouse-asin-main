<script setup lang="ts">
import type { ColumnsConfig, Config } from 'datatables.net'
import DataTablesCore from 'datatables.net-bs4'
import DataTable from 'datatables.net-vue3'
import { computed, nextTick, onBeforeUnmount, ref } from 'vue'

import { highlightTableText } from '@/components/UseTableHighlight'
import DataTableFilters from '@/components/filters/DataTableFilters.vue'
import InventoryListToolbar from '@/components/InventoryListToolbar.vue'
import BaseModal from '@/components/BaseModal.vue'
import { useColumnVisibility } from '@/composables/useColumnVisibility'
import { useCompactScrollbar } from '@/composables/useCompactScrollbar'
import { useDataTableList } from '@/composables/useDataTableList'
import { useFilterState } from '@/composables/useFilterState'
import { usePermissions } from '@/composables/usePermissions'
import { isAbortError, useRequestChannel } from '@/composables/useRequestChannel'
import { useUnauthorizedRedirect } from '@/composables/useUnauthorizedRedirect'
import {
  getInvoice,
  getInvoices,
  updateInvoice,
  printInvoices,
  changeInvoiceStatus,
  type InvoiceDetails,
  type InvoiceListParameters,
  type InvoiceSummary,
  type InvoiceProcessingStatus,
  type UpdateInvoiceRequest,
} from '@/services/invoices'
import { toast } from '@/services/toast'
import type { DataTableFilterDefinition } from '@/types/dataTableFilters'
import { createRowNumberColumn } from '@/utils/dataTable'
import { formatPersianDate, formatPersianNumber } from '@/utils/dateTime'
import { createInventoryTableOptions } from '@/utils/inventoryTable'
import { printPdf } from '@/utils/pdfPrint'
import '@/styles/inventory-list.css'

import InvoiceItemsModal from './InvoiceItemsModal.vue'
import InvoiceStatusChip from './InvoiceStatusChip.vue'

defineOptions({ name: 'InvoicesView' })
DataTable.use(DataTablesCore)

type TableMode = 'invoices'
const VIEW_MODES = [{ value: 'invoices', label: 'فاکتورها' }]
const tableMode = ref<TableMode>('invoices')
const tableContainer = ref<HTMLElement | null>(null)
const detailsOpen = ref(false)
const invoiceDetails = ref<InvoiceDetails | null>(null)
const detailsLoading = ref(false)
const detailsError = ref<string | null>(null)
const editMode = ref(false)
const editSaving = ref(false)

const { has } = usePermissions()
const canViewMissingReceiptCount = has('InvoicesMissingReceiptCountRead')
const canEditInvoice = has('GoodsReceiptsReceiptInvoiceOperations')
const canEditReceipt = has('GoodsReceiptsReceiptRegistration')
const canEdit = canEditInvoice || canEditReceipt
const canPrint = has('InvoicesPrint')
const canReopen = has('InvoicesReopen')
const selectedInvoices = ref(new Map<string, InvoiceSummary>())
const printOpen = ref(false)
const printing = ref(false)
const sendToFinance = ref(false)
const printError = ref('')
const selected = computed(() => [...selectedInvoices.value.values()])
const incompleteInvoices = computed(() => selected.value.filter(invoice => invoice.price == null || !invoice.counterparty?.trim()))
const hasLockedSelection = computed(() => selected.value.some(invoice => invoice.isLocked))

function toggleInvoice(invoice: InvoiceSummary, checked: boolean): void {
  if (checked) selectedInvoices.value.set(invoice.id, invoice)
  else selectedInvoices.value.delete(invoice.id)
}

function openPrint(): void {
  if (!selected.value.length || printing.value) return
  sendToFinance.value = false
  printError.value = ''
  printOpen.value = true
}

async function completeInvoice(invoice: InvoiceSummary): Promise<void> {
  printOpen.value = false
  await openItems(invoice)
  if (invoiceDetails.value && !invoiceDetails.value.isLocked && canEditInvoice) editMode.value = true
}

async function printSelected(): Promise<void> {
  if (!canPrint || printing.value || !selected.value.length || incompleteInvoices.value.length) return
  printing.value = true
  printError.value = ''
  try {
    const pdf = await printInvoices(selected.value.map(invoice => invoice.id), sendToFinance.value)
    // The server has completed the requested status change once it returns the PDF.
    list.reload(false)
    printOpen.value = false
    selectedInvoices.value.clear()
    try {
      await printPdf(pdf, 'چاپ فاکتورهای انتخاب‌شده')
    } catch (error) {
      toast.error(error instanceof Error ? error.message : 'پنجره چاپ باز نشد.')
    }
    if (sendToFinance.value) toast.success('ارسال فاکتورها به واحد مالی با تاریخ امروز ثبت شد.')
  } catch (error) {
    if (await handleUnauthorized(error)) return
    printError.value = error instanceof Error ? error.message : 'چاپ فاکتورها انجام نشد.'
  } finally {
    printing.value = false
  }
}

const { handleUnauthorized } = useUnauthorizedRedirect('/dashboard/inventory/invoices')
const filters = useFilterState(() => list.reload(true))
const { search: quickSearch, applied: appliedFilters } = filters

const filterDefinitions = computed<DataTableFilterDefinition[]>(() => [
  { key: 'invoiceNumber', label: 'شماره فاکتور', type: 'text-comparison', placeholder: 'شماره فاکتور', maxlength: 100 },
  { key: 'counterparty', label: 'طرف حساب', type: 'text-comparison', placeholder: 'نام طرف حساب', maxlength: 256 },
])

function currentParameters(): InvoiceListParameters {
  return {
    search: filters.searchQuery.value,
    invoiceNumber: filters.text('invoiceNumber'),
    counterparty: filters.text('counterparty'),
  }
}

const list = useDataTableList<InvoiceSummary>({
  errorMessage: 'دریافت فهرست فاکتورها انجام نشد.',
  onUnauthorized: handleUnauthorized,
  fetchPage: query => getInvoices({
    ...currentParameters(),
    pageNumber: query.page,
    pageSize: query.pageSize,
    sortBy: query.sortBy ?? 'createdAtUtc',
    sortDescending: query.sortDescending ?? true,
    signal: query.signal,
  }),
})
const { table, loading: listLoading } = list

const columns: Record<TableMode, ColumnsConfig[]> = {
  invoices: [
    ...(canPrint ? [{ data: 'id', name: 'selection', title: 'انتخاب', className: 'all text-center', orderable: false, searchable: false }] : []),
    { ...createRowNumberColumn(), className: 'min-tablet-p text-center data-table-row-number' },
    { data: 'invoiceNumber', name: 'invoiceNumber', title: 'شماره فاکتور', className: 'all text-center' },
    { data: 'financeDeliveryDate', name: 'financeDeliveryDate', title: 'تاریخ تحویل به مالی', className: 'min-tablet-l text-center' },
    { data: 'invoiceDate', name: 'invoiceDate', title: 'تاریخ فاکتور', className: 'min-tablet-l text-center' },
    { data: 'counterparty', name: 'counterparty', title: 'طرف حساب', className: 'all' },
    { data: 'price', name: 'price', title: 'قیمت', className: 'min-tablet-l text-center' },
    { data: 'processingStatus', name: 'processingStatus', title: 'وضعیت', className: 'min-tablet-l text-center' },
    { data: 'description', name: 'description', title: 'توضیحات', className: 'min-desktop', orderable: false },
    {
      data: 'receiptNumbers', name: 'receiptNumbers', title: 'شماره رسیدها',
      className: 'min-tablet-p text-center invoice-receipts-column', orderable: false, searchable: false,
    },
    ...(canViewMissingReceiptCount
      ? [{
          data: 'itemsWithoutReceiptCount', name: 'itemsWithoutReceiptCount', title: 'اقلام بدون رسید',
          className: 'min-tablet-p text-center text-bold', orderable: false, searchable: false,
        }]
      : []),
    {
      data: 'id', name: 'actions', title: 'عملیات', className: 'all text-center actions-cell',
      orderable: false, searchable: false,
    },
  ],
}

const compact = useCompactScrollbar(tableContainer)
const visibility = useColumnVisibility<TableMode>({
  storageKey: 'warehouse.invoices.hidden-columns.v1',
  mode: tableMode,
  columns,
  getApi: () => table.value?.dt,
  lockedNames: ['actions', 'rowNumber', 'selection'],
})
const {
  menuOpen,
  toggleable,
  isVisible: isColumnVisible,
  toggle: toggleColumn,
  toggleMenu,
} = visibility

let highlightFrame: number | null = null
function refreshHighlight(): void {
  if (highlightFrame !== null) cancelAnimationFrame(highlightFrame)
  void nextTick(() => {
    highlightFrame = requestAnimationFrame(() => {
      highlightFrame = null
      highlightTableText(tableContainer.value, quickSearch.value)
    })
  })
}

const tableOptions: Config = createInventoryTableOptions({
  ajax: list.ajax,
  columns: visibility.resolvedColumns.invoices,
  emptyText: 'هنوز فاکتوری ثبت نشده است.',
  loadingText: 'در حال دریافت فاکتورها...',
  zeroText: 'فاکتوری با این مشخصات پیدا نشد.',
  onDraw: () => { compact.schedule(); refreshHighlight() },
  onInit: compact.schedule,
})
// Start with the server's creation order and keep column sorting available.
tableOptions.ordering = true

const detailsChannel = useRequestChannel()
async function openItems(invoice: InvoiceSummary): Promise<void> {
  const controller = detailsChannel.begin()
  invoiceDetails.value = null
  detailsError.value = null
  editMode.value = false
  detailsLoading.value = true
  detailsOpen.value = true
  try {
    invoiceDetails.value = await getInvoice(invoice.id, controller.signal)
  } catch (error) {
    if (isAbortError(error)) return
    if (await handleUnauthorized(error)) return
    detailsError.value = error instanceof Error ? error.message : 'دریافت کالاهای فاکتور انجام نشد.'
  } finally {
    if (detailsChannel.isCurrent(controller)) {
      detailsChannel.end(controller)
      detailsLoading.value = false
    }
  }
}

function closeDetails(value: boolean): void {
  detailsOpen.value = value
  if (!value) {
    detailsChannel.abort()
    detailsLoading.value = false
    editMode.value = false
  }
}

async function saveInvoice(values: UpdateInvoiceRequest): Promise<void> {
  const details = invoiceDetails.value
  if (!details || details.isLocked || editSaving.value) return
  const controller = detailsChannel.begin()
  editSaving.value = true
  detailsError.value = null
  try {
    invoiceDetails.value = await updateInvoice(details.id, values, controller.signal)
    if (selectedInvoices.value.has(details.id)) selectedInvoices.value.set(details.id, {
      ...selectedInvoices.value.get(details.id)!, ...invoiceDetails.value,
      receiptNumbers: invoiceDetails.value.items.map(item => item.receiptNumber).filter((value): value is string => Boolean(value)),
    })
    editMode.value = false
    list.reload(false)
    toast.success('تغییرات با موفقیت ذخیره شد.')
  } catch (error) {
    if (isAbortError(error)) return
    if (await handleUnauthorized(error)) return
    detailsError.value = error instanceof Error ? error.message : 'ویرایش فاکتور انجام نشد.'
  } finally {
    if (detailsChannel.isCurrent(controller)) {
      detailsChannel.end(controller)
      editSaving.value = false
    }
  }
}

async function updateStatus(status: InvoiceProcessingStatus): Promise<void> {
  const details = invoiceDetails.value
  if (!details || editSaving.value) return
  const controller = detailsChannel.begin()
  editSaving.value = true
  detailsError.value = null
  try {
    const updated = await changeInvoiceStatus(details.id, status, details.processingStatus, controller.signal)
    invoiceDetails.value = updated
    if (selectedInvoices.value.has(updated.id)) selectedInvoices.value.set(updated.id, {
      ...selectedInvoices.value.get(updated.id)!, ...updated,
      receiptNumbers: updated.items.map(item => item.receiptNumber).filter((value): value is string => Boolean(value)),
    })
    editMode.value = false
    list.reload(false)
    toast.success(status === 'UnderReview' ? 'فاکتور به در حال رسیدگی برگشت و قفل ویرایش آن برداشته شد.' : 'تأیید واحد مالی ثبت شد.')
  } catch (error) {
    if (isAbortError(error)) return
    if (await handleUnauthorized(error)) return
    detailsError.value = error instanceof Error ? error.message : 'تغییر وضعیت فاکتور انجام نشد.'
  } finally {
    if (detailsChannel.isCurrent(controller)) {
      detailsChannel.end(controller)
      editSaving.value = false
    }
  }
}

onBeforeUnmount(() => {
  if (highlightFrame !== null) cancelAnimationFrame(highlightFrame)
  detailsChannel.abort()
})
</script>

<template>
  <BaseModal v-model="printOpen" title="چاپ فاکتورهای انتخاب‌شده" size="lg" persistent :busy="printing">
    <p>{{ formatPersianNumber(selected.length) }} فاکتور برای چاپ انتخاب شده است.</p>
    <div v-if="printError" class="alert alert-danger invoice-print-error" role="alert">{{ printError }}</div>
    <div v-if="incompleteInvoices.length" class="alert alert-warning" role="alert">
      ابتدا مبلغ و طرف حساب فاکتورهای زیر را تکمیل کنید. تا تکمیل اطلاعات، چاپ انجام نمی‌شود.
    </div>
    <div class="table-responsive invoice-print-selection">
      <table class="table table-sm">
        <thead><tr><th>شماره فاکتور</th><th>طرف حساب</th><th>مبلغ</th><th>اطلاعات ناقص</th></tr></thead>
        <tbody>
          <tr v-for="invoice in selected" :key="invoice.id">
            <td dir="ltr">{{ invoice.invoiceNumber }}</td>
            <td>{{ invoice.counterparty || 'ثبت نشده' }}</td>
            <td>{{ invoice.price == null ? 'ثبت نشده' : formatPersianNumber(invoice.price) }}</td>
            <td>
              <button v-if="invoice.price == null || !invoice.counterparty?.trim()" type="button" class="btn btn-sm btn-outline-primary"
                      :disabled="printing" @click="completeInvoice(invoice)">تکمیل اطلاعات</button>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
    <label class="d-flex align-items-center mt-3">
      <input v-model="sendToFinance" type="checkbox" class="ml-2" :disabled="printing || !canEditInvoice || hasLockedSelection" />
      ارسال به واحد مالی
    </label>
    <p class="text-muted mb-0">با انتخاب این گزینه، پس از آماده‌شدن فایل چاپ، وضعیت فاکتورها «ارسال به واحد مالی» و تاریخ تحویل، امروز ثبت می‌شود.</p>
    <small v-if="!canEditInvoice" class="text-muted">برای ثبت ارسال به مالی، مجوز ثبت و ویرایش فاکتور لازم است.</small>
    <small v-if="hasLockedSelection" class="text-muted d-block">فاکتور ارسال یا تأییدشده فقط بدون تغییر وضعیت چاپ می‌شود.</small>
    <template #footer>
      <button type="button" class="btn btn-secondary" :disabled="printing" @click="printOpen = false">انصراف</button>
      <button type="button" class="btn btn-primary" :disabled="printing || !selected.length || incompleteInvoices.length > 0" @click="printSelected">
        {{ printing ? 'در حال آماده‌سازی چاپ…' : 'چاپ' }}
      </button>
    </template>
  </BaseModal>
  <InvoiceItemsModal :model-value="detailsOpen" :details="invoiceDetails"
                     :loading="detailsLoading" :error="detailsError" :can-edit="canEdit && !invoiceDetails?.isLocked"
                     :can-edit-invoice="canEditInvoice" :can-edit-receipt="canEditReceipt"
                     :can-reopen="canReopen" :can-approve="canEditInvoice"
                     :edit-mode="editMode && !invoiceDetails?.isLocked" :saving="editSaving"
                     @change-status="updateStatus"
                     @update:edit-mode="editMode = $event && !invoiceDetails?.isLocked; detailsError = null"
                     @save="saveInvoice" @update:model-value="closeDetails" />

  <div class="container-fluid users-page invoices-page inventory-data-page">
    <div class="page-titles">
      <ol class="breadcrumb">
        <li class="breadcrumb-item"><RouterLink to="/dashboard">داشبورد</RouterLink></li>
        <li class="breadcrumb-item active"><span>فاکتورها</span></li>
      </ol>
    </div>

    <div class="card">
      <div class="card-header">
        <div>
          <h4 class="card-title"><i class="las la-file-invoice mr-1"></i> فاکتورها</h4>
          <p class="page-description mb-0 mt-1 text-muted">مشاهده فاکتورها و کالاهای ثبت‌شده در هر فاکتور</p>
        </div>
        <InventoryListToolbar :mode="tableMode" :modes="VIEW_MODES" entity-label="فاکتور" create-label=""
                              :can-create="false" :columns="toggleable[tableMode]"
                              :is-column-visible="isColumnVisible" :column-menu-open="menuOpen"
                              @update:mode="tableMode = $event as TableMode"
                              @toggle-column="toggleColumn" @toggle-column-menu="toggleMenu" />
      </div>

      <div class="card-body">
        <div v-if="canPrint" class="d-flex align-items-center mb-3" style="gap: .75rem">
          <button type="button" class="btn btn-primary" :disabled="printing || !selected.length" @click="openPrint">
            <i class="las la-print" aria-hidden="true"></i> چاپ انتخاب‌شده‌ها ({{ formatPersianNumber(selected.length) }})
          </button>
          <button v-if="selected.length" type="button" class="btn btn-outline-secondary" :disabled="printing" @click="selectedInvoices.clear()">لغو انتخاب‌ها</button>
        </div>
        <DataTableFilters v-model="appliedFilters" :definitions="filterDefinitions" :search="quickSearch"
                          :loading="listLoading" search-placeholder="جستجو در شماره فاکتور و طرف حساب..."
                          @update:search="filters.onSearch" @apply="filters.onApply" @clear="filters.onClear"
                          @remove="filters.onRemove" />

        <div ref="tableContainer" class="users-table-container inventory-table-container inventory-table-container--invoices"
             @wheel="compact.onWheel">
          <DataTable ref="table" :options="tableOptions"
                     class="display responsive nowrap table-striped table table-sm users-table inventory-data-table">
            <template #column-selection="{ rowData }">
              <input type="checkbox" :checked="selectedInvoices.has(rowData.id)" :disabled="printing"
                     :aria-label="`انتخاب فاکتور ${rowData.invoiceNumber}`"
                     @change="toggleInvoice(rowData, ($event.target as HTMLInputElement).checked)" />
            </template>
            <template #column-invoiceNumber="{ cellData }">
              <strong class="invoice-number" dir="ltr">{{ cellData }}</strong>
            </template>
            <template #column-counterparty="{ cellData }">{{ cellData ?? '' }}</template>
            <template #column-financeDeliveryDate="{ cellData }">{{ formatPersianDate(cellData) }}</template>
            <template #column-price="{ cellData }">{{ cellData == null ? '' : formatPersianNumber(cellData) }}</template>
            <template #column-invoiceDate="{ cellData }">{{ formatPersianDate(cellData) }}</template>
            <template #column-processingStatus="{ cellData }">
              <InvoiceStatusChip :status="cellData as InvoiceProcessingStatus" />
            </template>
            <template #column-description="{ cellData }">{{ cellData ?? '' }}</template>
            <template #column-receiptNumbers="{ cellData }">
              <div v-if="(cellData as string[]).length" class="invoice-receipt-numbers">
                <span v-for="receiptNumber in (cellData as string[])" :key="receiptNumber"
                      class="badge light badge-info" dir="ltr">{{ receiptNumber }}</span>
              </div>
              <span v-else></span>
            </template>
            <template #column-itemsWithoutReceiptCount="{ cellData }">
              <span class="badge light" :class="cellData > 0 ? 'badge-warning' : 'badge-success'">
                {{ formatPersianNumber(cellData) }} کالا
              </span>
            </template>
            <template #column-actions="{ rowData }">
              <button class="btn users-table__role-action" type="button" title="مشاهده فاکتور"
                      aria-label="مشاهده فاکتور" @click="openItems(rowData)">
                <i class="las la-boxes" aria-hidden="true"></i>
              </button>
            </template>
          </DataTable>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.invoice-print-error { white-space: pre-line; }
.invoice-print-selection { max-height: 340px; overflow-y: auto; }
.invoice-receipt-numbers {
  display: flex;
  flex-wrap: wrap;
  gap: .3rem;
  justify-content: center;
}
.invoice-number {
  color: var(--text-primary, #212529);
  font-weight: 700;
}


</style>
