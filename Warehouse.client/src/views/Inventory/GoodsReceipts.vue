<script setup lang="ts">
import type { AjaxCallback, AjaxData, Api, ColumnsConfig, Config } from 'datatables.net'
import DataTablesCore from 'datatables.net-bs4'
import DataTable from 'datatables.net-vue3'
import { computed, nextTick, onBeforeUnmount, onMounted, reactive, ref, shallowRef, watch } from 'vue'
import { useRouter } from 'vue-router'

import ConfirmDialog from '@/components/ConfirmDialog.vue'
import { getSession } from '@/services/auth'
import {
  createGoodsReceipt,
  deleteGoodsReceipt,
  getGoodsReceiptItems,
  getGoodsReceiptOptions,
  getGoodsReceipts,
  GoodsReceiptsApiError,
  type GoodsReceipt,
  type GoodsReceiptItemRow,
  type InvoiceStatus,
} from '@/services/goodsReceipts'
import type { Product } from '@/services/products'
import { toast } from '@/services/toast'
import type { UnitOfMeasure } from '@/services/unitsOfMeasure'
import { createRowNumberColumn } from '@/utils/dataTable'
import DatePicker from 'vue3-persian-datetime-picker'

defineOptions({ name: 'GoodsReceiptsView' })
DataTable.use(DataTablesCore)

interface PersonOption { id: string; fullName: string }
interface WarehouseOption { id: string; title: string }
interface SupplierOption { id: string; title: string }
interface FormItem {
  key: number
  productText: string
  productId: string | null
  productCode: string
  unitOfMeasureId: string | null
  unitOfMeasureTitle: string
  quantity: number | null
  requestNumber: string
  description: string
}

type ProductLookupField = 'title' | 'code'
type GoodsEntryMode = 'cards' | 'list'
type TableMode = 'items' | 'receipts'
type RtlScrollType = 'default' | 'negative' | 'reverse'
interface ColumnSetting { key: string; label: string; index: number }

interface ConfirmDialogInstance {
  ask: (options: {
    title: string
    text?: string
    icon?: 'warning'
    confirmButtonText?: string
    cancelButtonText?: string
    confirmButtonColor?: string
  }) => Promise<boolean>
}

const router = useRouter()
const table = shallowRef<{ dt: Api } | null>(null)
const tableContainer = ref<HTMLElement | null>(null)
const confirmDialog = ref<ConfirmDialogInstance | null>(null)
const modalOpen = ref(false)
const saving = ref(false)
const referenceLoading = ref(false)
const formError = ref('')
const validationErrors = reactive<Record<string, string>>({})
const activeProductMenu = ref<{ itemKey: number; field: ProductLookupField } | null>(null)
const activeUnitMenu = ref<number | null>(null)
const delivererMenuOpen = ref(false)
const supplierMenuOpen = ref(false)
const goodsEntryMode = ref<GoodsEntryMode>('list')
const tableMode = ref<TableMode>('items')
const columnSettingsOpen = ref(false)
const people = ref<PersonOption[]>([])
const warehouses = ref<WarehouseOption[]>([])
const products = ref<Product[]>([])
const suppliers = ref<SupplierOption[]>([])
const units = ref<UnitOfMeasure[]>([])
const sessionPermissions = new Set(getSession()?.permissions ?? [])
const canInitialEntry = sessionPermissions.has('GoodsReceiptsInitialEntry')
const canManageReceiptInvoice = sessionPermissions.has('GoodsReceiptsReceiptInvoiceOperations')
const canManageMasterData = sessionPermissions.has('MasterDataManage')
const referenceDataLoaded = ref(false)
let rowKey = 0
let listRequest: AbortController | null = null
let operationRequest: AbortController | null = null
let referenceRequest: AbortController | null = null
let tableScrollBody: HTMLElement | null = null
let compactHorizontalScrollbar: HTMLDivElement | null = null
let compactHorizontalScrollbarContent: HTMLDivElement | null = null
let tableScrollResizeObserver: ResizeObserver | null = null
let scrollSyncInProgress = false
let rtlScrollType: RtlScrollType | null = null
const listModeMinimumRows = 6

const form = reactive({
  enteredAtLocal: '',
  description: '',
  delivererId: '',
  delivererText: '',
  warehouseId: '',
  invoiceStatus: 'DoesNotHave' as InvoiceStatus,
  invoiceNumber: '',
  supplierId: '',
  supplierText: '',
  receiptNumber: '',
  pageNumber: '',
  items: [] as FormItem[],
})

const receiptColumns: ColumnsConfig[] = [
  createRowNumberColumn(),
  { data: 'enteredAtUtc', name: 'enteredAtUtc', title: 'تاریخ ورود', className: 'all' },
  { data: 'pageNumber', name: 'pageNumber', title: 'شماره صفحه', className: 'min-tablet-l' },
  { data: 'delivererFullName', name: 'delivererFullName', title: 'تحویل‌دهنده', className: 'min-tablet-l' },
  { data: 'warehouseTitle', name: 'warehouseTitle', title: 'انبار', className: 'min-desktop', orderable: false },
  { data: 'items', name: 'itemCount', title: 'تعداد کالا', className: 'min-tablet-l', orderable: false },
  { data: 'invoiceStatus', name: 'invoiceStatus', title: 'فاکتور', className: 'min-desktop' },
  { data: 'invoiceNumber', name: 'invoiceNumber', title: 'شماره فاکتور', className: 'text-nowrap', orderable: false },
  { data: 'counterparty', name: 'counterparty', title: 'طرف حساب', className: 'text-nowrap', orderable: false },
  { data: 'receiptNumber', name: 'receiptNumber', title: 'شماره رسید', className: 'text-nowrap', orderable: false },
  { data: 'description', name: 'description', title: 'توضیحات ورودی', orderable: false },
]

const itemColumns: ColumnsConfig[] = [
  createRowNumberColumn(),
  { data: 'enteredAtUtc', name: 'enteredAtUtc', title: 'تاریخ', className: 'all' },
  { data: 'productTitle', name: 'productTitle', title: 'عنوان کالا', className: 'all' },
  { data: 'productCode', name: 'productCode', title: 'کد کالا', className: 'min-tablet-l' },
  { data: 'quantity', name: 'quantity', title: 'تعداد', className: 'min-tablet-l' },
  { data: 'unitOfMeasureTitle', name: 'unitOfMeasureTitle', title: 'واحد', className: 'min-tablet-l' },
  { data: 'itemDescription', name: 'itemDescription', title: 'توضیحات', orderable: false },
  { data: 'delivererFullName', name: 'delivererFullName', title: 'تحویل‌دهنده', className: 'min-desktop' },
  { data: 'invoiceStatus', name: 'invoiceStatus', title: 'فاکتور', className: 'min-desktop' },
  { data: 'invoiceNumber', name: 'invoiceNumber', title: 'شماره فاکتور', className: 'text-nowrap', orderable: false },
  { data: 'counterparty', name: 'counterparty', title: 'طرف حساب', className: 'text-nowrap', orderable: false },
  { data: 'receiptNumber', name: 'receiptNumber', title: 'شماره رسید', className: 'text-nowrap', orderable: false },
  { data: 'warehouseTitle', name: 'warehouseTitle', title: 'انبار', className: 'min-desktop' },
  { data: 'pageNumber', name: 'pageNumber', title: 'شماره صفحه', className: 'min-tablet-l' },
]

const receiptColumnSettings: ColumnSetting[] = [
  { key: 'enteredAtUtc', label: 'تاریخ ورود', index: 1 },
  { key: 'pageNumber', label: 'شماره صفحه', index: 2 },
  { key: 'delivererFullName', label: 'تحویل‌دهنده', index: 3 },
  { key: 'warehouseTitle', label: 'انبار', index: 4 },
  { key: 'itemCount', label: 'تعداد کالا', index: 5 },
  { key: 'invoiceStatus', label: 'وضعیت فاکتور', index: 6 },
  { key: 'invoiceNumber', label: 'شماره فاکتور', index: 7 },
  { key: 'counterparty', label: 'طرف حساب', index: 8 },
  { key: 'receiptNumber', label: 'شماره رسید', index: 9 },
  { key: 'description', label: 'توضیحات ورودی', index: 10 },
]

const itemColumnSettings: ColumnSetting[] = [
  { key: 'enteredAtUtc', label: 'تاریخ', index: 1 },
  { key: 'productTitle', label: 'عنوان کالا', index: 2 },
  { key: 'productCode', label: 'کد کالا', index: 3 },
  { key: 'quantity', label: 'تعداد', index: 4 },
  { key: 'unitOfMeasureTitle', label: 'واحد', index: 5 },
  { key: 'itemDescription', label: 'توضیحات', index: 6 },
  { key: 'delivererFullName', label: 'تحویل‌دهنده', index: 7 },
  { key: 'invoiceStatus', label: 'وضعیت فاکتور', index: 8 },
  { key: 'invoiceNumber', label: 'شماره فاکتور', index: 9 },
  { key: 'counterparty', label: 'طرف حساب', index: 10 },
  { key: 'receiptNumber', label: 'شماره رسید', index: 11 },
  { key: 'warehouseTitle', label: 'انبار', index: 12 },
  { key: 'pageNumber', label: 'شماره صفحه', index: 13 },
]

const columnVisibilityStorageKey = 'warehouse.goods-receipts.visible-columns.v1'

function loadVisibleColumns(): Record<TableMode, string[]> {
  const defaults: Record<TableMode, string[]> = {
    items: itemColumnSettings.map(column => column.key),
    receipts: receiptColumnSettings.map(column => column.key),
  }

  try {
    const saved = window.localStorage.getItem(columnVisibilityStorageKey)
    if (!saved) return defaults
    const parsed = JSON.parse(saved) as Partial<Record<TableMode, unknown>>
    const savedItems = Array.isArray(parsed.items)
      ? parsed.items.filter((key): key is string => typeof key === 'string')
      : null
    const savedReceipts = Array.isArray(parsed.receipts)
      ? parsed.receipts.filter((key): key is string => typeof key === 'string')
      : null

    return {
      items: savedItems
        ? defaults.items.filter(key => savedItems.includes(key))
        : defaults.items,
      receipts: savedReceipts
        ? defaults.receipts.filter(key => savedReceipts.includes(key))
        : defaults.receipts,
    }
  } catch {
    return defaults
  }
}

const visibleColumns = reactive<Record<TableMode, string[]>>(loadVisibleColumns())

itemColumnSettings.forEach(column => {
  if (itemColumns[column.index]) {
    itemColumns[column.index].visible = visibleColumns.items.includes(column.key)
  }
})
receiptColumnSettings.forEach(column => {
  if (receiptColumns[column.index]) {
    receiptColumns[column.index].visible = visibleColumns.receipts.includes(column.key)
  }
})

const activeColumnSettings = computed(() =>
  tableMode.value === 'items' ? itemColumnSettings : receiptColumnSettings,
)

if (canInitialEntry) {
  receiptColumns.push({
    data: 'id',
    name: 'actions',
    title: 'عملیات',
    className: 'all text-center',
    orderable: false,
    searchable: false,
  })
  itemColumns.push({
    data: 'goodsReceiptId',
    name: 'actions',
    title: 'عملیات',
    className: 'all text-center',
    orderable: false,
    searchable: false,
  })
}

function getRtlScrollType(): RtlScrollType {
  if (rtlScrollType) return rtlScrollType

  const outer = document.createElement('div')
  const inner = document.createElement('div')
  outer.dir = 'rtl'
  outer.style.cssText = 'height:1px;overflow:scroll;position:absolute;top:-1000px;width:4px;'
  inner.style.width = '8px'
  outer.append(inner)
  document.body.append(outer)

  if (outer.scrollLeft > 0) {
    rtlScrollType = 'default'
  } else {
    outer.scrollLeft = 1
    rtlScrollType = outer.scrollLeft === 0 ? 'negative' : 'reverse'
  }

  outer.remove()
  return rtlScrollType
}

function tableNormalizedScrollLeft(element: HTMLElement): number {
  if (window.getComputedStyle(element).direction !== 'rtl') return element.scrollLeft

  const maximum = element.scrollWidth - element.clientWidth
  switch (getRtlScrollType()) {
    case 'negative': return maximum + element.scrollLeft
    case 'reverse': return maximum - element.scrollLeft
    default: return element.scrollLeft
  }
}

function setTableNormalizedScrollLeft(element: HTMLElement, value: number): void {
  if (window.getComputedStyle(element).direction !== 'rtl') {
    element.scrollLeft = value
    return
  }

  const maximum = element.scrollWidth - element.clientWidth
  switch (getRtlScrollType()) {
    case 'negative':
      element.scrollLeft = value - maximum
      break
    case 'reverse':
      element.scrollLeft = maximum - value
      break
    default:
      element.scrollLeft = value
  }
}

function updateCompactHorizontalScrollbar(): void {
  if (!tableScrollBody || !compactHorizontalScrollbar || !compactHorizontalScrollbarContent) return

  const tableMaximum = Math.max(0, tableScrollBody.scrollWidth - tableScrollBody.clientWidth)
  compactHorizontalScrollbar.hidden = tableMaximum === 0
  if (tableMaximum === 0) return

  const scrollbarViewport = compactHorizontalScrollbar.clientWidth || 300
  const contentWidth = scrollbarViewport * (tableScrollBody.scrollWidth / tableScrollBody.clientWidth)
  compactHorizontalScrollbarContent.style.width = `${Math.max(scrollbarViewport + 1, contentWidth)}px`

  const scrollbarMaximum = compactHorizontalScrollbar.scrollWidth - compactHorizontalScrollbar.clientWidth
  const tablePosition = tableNormalizedScrollLeft(tableScrollBody)
  compactHorizontalScrollbar.scrollLeft = scrollbarMaximum * (tablePosition / tableMaximum)
}

function syncCompactScrollbarFromTable(): void {
  if (scrollSyncInProgress || !tableScrollBody || !compactHorizontalScrollbar) return

  const tableMaximum = tableScrollBody.scrollWidth - tableScrollBody.clientWidth
  const scrollbarMaximum = compactHorizontalScrollbar.scrollWidth - compactHorizontalScrollbar.clientWidth
  if (tableMaximum <= 0 || scrollbarMaximum <= 0) return

  scrollSyncInProgress = true
  compactHorizontalScrollbar.scrollLeft = scrollbarMaximum * (tableNormalizedScrollLeft(tableScrollBody) / tableMaximum)
  scrollSyncInProgress = false
}

function syncTableFromCompactScrollbar(): void {
  if (scrollSyncInProgress || !tableScrollBody || !compactHorizontalScrollbar) return

  const tableMaximum = tableScrollBody.scrollWidth - tableScrollBody.clientWidth
  const scrollbarMaximum = compactHorizontalScrollbar.scrollWidth - compactHorizontalScrollbar.clientWidth
  if (tableMaximum <= 0 || scrollbarMaximum <= 0) return

  scrollSyncInProgress = true
  const nextPosition = tableMaximum * (compactHorizontalScrollbar.scrollLeft / scrollbarMaximum)
  setTableNormalizedScrollLeft(tableScrollBody, nextPosition)
  scrollSyncInProgress = false
}

function disconnectCompactHorizontalScrollbar(): void {
  tableScrollResizeObserver?.disconnect()
  tableScrollResizeObserver = null
  tableScrollBody?.removeEventListener('scroll', syncCompactScrollbarFromTable)
  compactHorizontalScrollbar?.removeEventListener('scroll', syncTableFromCompactScrollbar)
  compactHorizontalScrollbar?.remove()
  tableScrollBody = null
  compactHorizontalScrollbar = null
  compactHorizontalScrollbarContent = null
}

function connectCompactHorizontalScrollbar(): void {
  const nextScrollBody = tableContainer.value?.querySelector<HTMLElement>('.dt-layout-table > div') ?? null
  const scrollWrapper = tableContainer.value?.querySelector<HTMLElement>('.dt-layout-table') ?? null
  if (!nextScrollBody || !scrollWrapper) return
  if (nextScrollBody === tableScrollBody && compactHorizontalScrollbar?.isConnected) {
    updateCompactHorizontalScrollbar()
    return
  }

  disconnectCompactHorizontalScrollbar()
  tableScrollBody = nextScrollBody
  compactHorizontalScrollbar = document.createElement('div')
  compactHorizontalScrollbar.className = 'data-table-horizontal-scrollbar'
  compactHorizontalScrollbar.setAttribute('aria-label', 'اسکرول افقی جدول')
  compactHorizontalScrollbar.setAttribute('role', 'scrollbar')
  compactHorizontalScrollbarContent = document.createElement('div')
  compactHorizontalScrollbarContent.className = 'data-table-horizontal-scrollbar__content'
  compactHorizontalScrollbar.append(compactHorizontalScrollbarContent)
  scrollWrapper.insertAdjacentElement('afterend', compactHorizontalScrollbar)

  tableScrollBody.addEventListener('scroll', syncCompactScrollbarFromTable, { passive: true })
  compactHorizontalScrollbar.addEventListener('scroll', syncTableFromCompactScrollbar, { passive: true })
  tableScrollResizeObserver = new ResizeObserver(updateCompactHorizontalScrollbar)
  tableScrollResizeObserver.observe(tableScrollBody)
  updateCompactHorizontalScrollbar()
}

function scheduleCompactHorizontalScrollbar(): void {
  window.requestAnimationFrame(connectCompactHorizontalScrollbar)
}

function createTableOptions(
  columns: ColumnsConfig[],
  orderColumn: number,
  emptyTable: string,
): Config {
  return {
    ajax: loadTableRows,
    columns,
    serverSide: true,
    processing: true,
    searching: false,
    pageLength: 10,
    lengthMenu: [5, 10, 25, 50],
    order: [[orderColumn, 'desc']],
    autoWidth: true,
    pagingType: 'full_numbers',
    responsive: false,
    drawCallback: scheduleCompactHorizontalScrollbar,
    initComplete: scheduleCompactHorizontalScrollbar,
    layout: {
      topStart: null,
      topEnd: null,
      bottomStart: { className: 'users-table-summary', features: ['pageLength', 'info'] },
      bottomEnd: { className: 'users-table-pagination', features: ['paging'] },
    },
    language: {
      emptyTable,
      info: '_START_–_END_ از _TOTAL_',
      infoEmpty: '۰ از ۰',
      infoFiltered: '',
      lengthMenu: '_MENU_',
      loadingRecords: 'در حال دریافت تاریخچه ورود کالا...',
      processing: 'در حال پردازش...',
      zeroRecords: 'ورودی کالایی پیدا نشد.',
      paginate: { first: '<<', last: '>>', next: '>', previous: '<' },
    },
  }
}

const receiptTableOptions = createTableOptions(receiptColumns, 1, 'هنوز ورودی کالایی ثبت نشده است.')
const itemTableOptions = createTableOptions(itemColumns, 1, 'هنوز کالایی وارد انبار نشده است.')

const invoiceHasNumber = computed(() => form.invoiceStatus === 'HasInvoice')

function columnIsVisible(key: string): boolean {
  return visibleColumns[tableMode.value].includes(key)
}

function toggleColumnVisibility(column: ColumnSetting): void {
  const mode = tableMode.value
  const visible = visibleColumns[mode]
  const show = !visible.includes(column.key)
  if (!show && visible.length === 1) return

  visibleColumns[mode] = show
    ? [...visible, column.key]
    : visible.filter(key => key !== column.key)

  try {
    window.localStorage.setItem(columnVisibilityStorageKey, JSON.stringify({
      items: visibleColumns.items,
      receipts: visibleColumns.receipts,
    }))
  } catch {
    // Column visibility still works for the current session when storage is unavailable.
  }

  const columns = mode === 'items' ? itemColumns : receiptColumns
  if (columns[column.index]) columns[column.index].visible = show

  const api = table.value?.dt
  if (!api) return
  api.column(column.index).visible(show, false)
  api.columns.adjust().draw(false)
}

function toggleColumnSettings(): void {
  columnSettingsOpen.value = !columnSettingsOpen.value
}

function closeColumnSettings(): void {
  columnSettingsOpen.value = false
}

function handleTableWheel(event: WheelEvent): void {
  if (!event.ctrlKey || Math.abs(event.deltaY) < Math.abs(event.deltaX)) return

  const scrollBody = tableContainer.value?.querySelector<HTMLElement>('.dt-layout-table > div')
  if (!scrollBody || scrollBody.scrollWidth <= scrollBody.clientWidth) return

  event.preventDefault()
  const maximum = scrollBody.scrollWidth - scrollBody.clientWidth
  const direction = window.getComputedStyle(scrollBody).direction === 'rtl' ? -1 : 1
  const nextPosition = Math.min(maximum, Math.max(0, tableNormalizedScrollLeft(scrollBody) + (event.deltaY * direction)))
  setTableNormalizedScrollLeft(scrollBody, nextPosition)
}

async function loadTableRows(request: AjaxData, callback: AjaxCallback): Promise<void> {
  listRequest?.abort()
  const controller = new AbortController()
  listRequest = controller
  const start = request.start ?? 0
  const pageSize = request.length && request.length > 0 ? request.length : 10
  const orderedColumn = request.order?.[0]
  const columnName = orderedColumn ? request.columns?.[orderedColumn.column]?.name : undefined

  try {
    const parameters = {
      pageNumber: Math.floor(start / pageSize) + 1,
      pageSize,
      sortBy: typeof columnName === 'string' ? columnName : undefined,
      sortDescending: orderedColumn?.dir === 'desc',
      signal: controller.signal,
    }
    const result = tableMode.value === 'items'
      ? await getGoodsReceiptItems(parameters)
      : await getGoodsReceipts(parameters)
    callback({
      draw: request.draw,
      recordsTotal: result.totalCount,
      recordsFiltered: result.filteredCount,
      data: result.items,
    })
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') return
    if (await redirectWhenUnauthorized(error)) return
    toast.error(error instanceof Error ? error.message : 'دریافت اطلاعات ورود کالا انجام نشد.')
    callback({ draw: request.draw, recordsTotal: 0, recordsFiltered: 0, data: [] })
  }
}

function localDateTimeNow(): string {
  const now = new Date()
  now.setMinutes(now.getMinutes() - now.getTimezoneOffset())
  return now.toISOString().slice(0, 16)
}

function newItem(): FormItem {
  return {
    key: ++rowKey,
    productText: '',
    productId: null,
    productCode: '',
    unitOfMeasureId: null,
    unitOfMeasureTitle: '',
    quantity: null,
    requestNumber: '',
    description: '',
  }
}

function resetForm(): void {
  activeProductMenu.value = null
  activeUnitMenu.value = null
  delivererMenuOpen.value = false
  supplierMenuOpen.value = false
  goodsEntryMode.value = 'list'
  Object.assign(form, {
    enteredAtLocal: localDateTimeNow(),
    description: '',
    delivererId: '',
    delivererText: '',
    warehouseId: '',
    invoiceStatus: 'DoesNotHave' as InvoiceStatus,
    invoiceNumber: '',
    supplierId: '',
    supplierText: '',
    receiptNumber: '',
    pageNumber: '',
    items: [newItem()],
  })
  ensureListRows()
  clearValidationErrors()
  formError.value = ''
}

async function loadReferenceData(): Promise<boolean> {
  if (referenceDataLoaded.value) return true

  referenceRequest?.abort()
  const controller = new AbortController()
  referenceRequest = controller
  referenceLoading.value = true
  try {
    const result = await getGoodsReceiptOptions(controller.signal)
    people.value = result.people
    warehouses.value = result.warehouses
    products.value = result.products
    suppliers.value = result.suppliers
    units.value = result.unitsOfMeasure
    referenceDataLoaded.value = true
    return true
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') return false
    if (await redirectWhenUnauthorized(error)) return false
    toast.error(error instanceof Error ? error.message : 'دریافت اطلاعات فرم انجام نشد.')
    return false
  } finally {
    if (referenceRequest === controller) referenceLoading.value = false
  }
}

async function openCreateModal(): Promise<void> {
  if (!await loadReferenceData()) return
  resetForm()
  modalOpen.value = true
}

function closeModal(): void {
  if (saving.value) return
  operationRequest?.abort()
  modalOpen.value = false
  formError.value = ''
}

function addItem(): void {
  form.items.push(newItem())
}

function removeItem(index: number): void {
  if (goodsEntryMode.value === 'cards' && form.items.length === 1) return
  form.items.splice(index, 1)
  if (goodsEntryMode.value === 'list') ensureListRows()
}

function itemHasContent(item: FormItem): boolean {
  return Boolean(
    item.productId ||
    item.productText.trim() ||
    item.productCode.trim() ||
    item.unitOfMeasureId ||
    item.unitOfMeasureTitle.trim() ||
    item.quantity !== null ||
    item.requestNumber.trim() ||
    item.description.trim(),
  )
}

function clearValidationErrors(): void {
  Object.keys(validationErrors).forEach(key => delete validationErrors[key])
}

function fieldError(key: string): string {
  return validationErrors[key] ?? ''
}

function clearFieldError(key: string): void {
  delete validationErrors[key]
}

type ItemValidationField = 'product' | 'code' | 'unit' | 'quantity' | 'requestNumber' | 'description'

function itemFieldKey(item: FormItem, field: ItemValidationField): string {
  return `item.${item.key}.${field}`
}

function itemFieldError(item: FormItem, field: ItemValidationField): string {
  return fieldError(itemFieldKey(item, field))
}

function clearItemFieldError(item: FormItem, field: ItemValidationField): void {
  clearFieldError(itemFieldKey(item, field))
  clearFieldError('items')
}

function itemsForSubmission(): FormItem[] {
  return goodsEntryMode.value === 'list'
    ? form.items.filter(itemHasContent)
    : form.items
}

function ensureListRows(): void {
  if (goodsEntryMode.value !== 'list') return

  while (form.items.length < listModeMinimumRows) form.items.push(newItem())
  const lastItem = form.items.at(-1)
  if (lastItem && itemHasContent(lastItem)) form.items.push(newItem())
}

function setGoodsEntryMode(mode: GoodsEntryMode): void {
  if (goodsEntryMode.value === mode) return

  activeProductMenu.value = null
  activeUnitMenu.value = null
  const populatedItems = form.items.filter(itemHasContent)
  goodsEntryMode.value = mode
  form.items.splice(
    0,
    form.items.length,
    ...(populatedItems.length ? populatedItems : [newItem()]),
  )
  if (mode === 'list') ensureListRows()
}

function handleListCellInput(item?: FormItem, field?: ItemValidationField): void {
  if (item && field) clearItemFieldError(item, field)
  ensureListRows()
}

function handleQuantityInput(item: FormItem): void {
  clearItemFieldError(item, 'quantity')
  ensureListRows()
}

function resizeListDescription(itemKey: number): void {
  const field = document.querySelector<HTMLTextAreaElement>(
    `textarea[data-list-description="${itemKey}"]`,
  )
  if (!field) return

  field.style.height = 'auto'
  field.style.height = `${field.scrollHeight}px`
}

function handleListDescriptionInput(item: FormItem): void {
  clearItemFieldError(item, 'description')
  const itemKey = item.key
  resizeListDescription(itemKey)
  ensureListRows()
}

function productComparisonKey(value: string): string {
  return value
    .replace(/[\s\u200C\u2212\p{Dash_Punctuation}]+/gu, '')
    .toLocaleUpperCase('en-US')
}

function supplierComparisonKey(value: string): string {
  return value
    .replace(/\u200C/gu, ' ')
    .trim()
    .replace(/\s+/gu, ' ')
    .toLocaleUpperCase('fa-IR')
}

function isSameProductValue(first: string, second: string): boolean {
  return productComparisonKey(first) === productComparisonKey(second)
}

function selectProduct(item: FormItem, product: Product): void {
  item.productId = product.id
  item.productText = product.title
  item.productCode = product.code
  item.unitOfMeasureId = product.unitOfMeasureId
  item.unitOfMeasureTitle = product.unitOfMeasureTitle
  item.description = product.description ?? ''
  clearItemFieldError(item, 'product')
  clearItemFieldError(item, 'code')
  clearItemFieldError(item, 'unit')
  ensureListRows()
  void nextTick(() => resizeListDescription(item.key))
}

function productMenuIsOpen(item: FormItem, field: ProductLookupField): boolean {
  return activeProductMenu.value?.itemKey === item.key && activeProductMenu.value.field === field
}

function openProductMenu(item: FormItem, field: ProductLookupField): void {
  activeProductMenu.value = { itemKey: item.key, field }
}

function closeProductMenu(): void {
  activeProductMenu.value = null
}

function filteredProductOptions(item: FormItem, field: ProductLookupField): Product[] {
  const query = productComparisonKey(field === 'title' ? item.productText : item.productCode)

  if (!query) return products.value.slice(0, 8)

  return products.value
    .filter(product => {
      const title = productComparisonKey(product.title)
      const code = productComparisonKey(product.code)
      return field === 'title'
        ? title.includes(query) || code.includes(query)
        : code.includes(query) || title.includes(query)
    })
    .slice(0, 8)
}

function filteredSupplierOptions(): SupplierOption[] {
  const query = supplierComparisonKey(form.supplierText)
  if (!query) return suppliers.value.slice(0, 8)

  return suppliers.value
    .filter(supplier => supplierComparisonKey(supplier.title).includes(query))
    .slice(0, 8)
}

function filteredDelivererOptions(): PersonOption[] {
  const query = productComparisonKey(form.delivererText)
  if (!query) return people.value.slice(0, 8)

  return people.value
    .filter(person => productComparisonKey(person.fullName).includes(query))
    .slice(0, 8)
}

function resolveDeliverer(): void {
  const match = people.value.find(
    person => productComparisonKey(person.fullName) === productComparisonKey(form.delivererText),
  )
  if (match) {
    form.delivererId = match.id
    form.delivererText = match.fullName
    return
  }

  form.delivererId = ''
}

function handleDelivererInput(): void {
  clearFieldError('deliverer')
  delivererMenuOpen.value = true
  resolveDeliverer()
}

function handleDelivererBlur(): void {
  resolveDeliverer()
  window.setTimeout(() => {
    delivererMenuOpen.value = false
  }, 120)
}

function chooseDeliverer(person: PersonOption): void {
  form.delivererId = person.id
  form.delivererText = person.fullName
  clearFieldError('deliverer')
  delivererMenuOpen.value = false
}

function isNewDeliverer(): boolean {
  return !form.delivererId && Boolean(form.delivererText.trim())
}

function resolveSupplier(): void {
  const match = suppliers.value.find(
    supplier => supplierComparisonKey(supplier.title) === supplierComparisonKey(form.supplierText),
  )
  if (match) {
    form.supplierId = match.id
    form.supplierText = match.title
    return
  }

  form.supplierId = ''
}

function handleSupplierInput(): void {
  clearFieldError('supplier')
  supplierMenuOpen.value = true
  resolveSupplier()
}

function handleSupplierBlur(): void {
  resolveSupplier()
  window.setTimeout(() => {
    supplierMenuOpen.value = false
  }, 120)
}

function chooseSupplier(supplier: SupplierOption): void {
  form.supplierId = supplier.id
  form.supplierText = supplier.title
  clearFieldError('supplier')
  supplierMenuOpen.value = false
}

function isNewSupplier(): boolean {
  return !form.supplierId && Boolean(form.supplierText.trim())
}

function isNewProduct(item: FormItem): boolean {
  return !item.productId && Boolean(item.productText.trim() || item.productCode.trim())
}

function usesProductCodeLookup(item: FormItem): boolean {
  return Boolean(item.productId) || !item.productText.trim()
}

function handleNewProductCodeInput(item: FormItem): void {
  clearItemFieldError(item, 'code')
  closeProductMenu()
  ensureListRows()
}

function unitMenuIsOpen(item: FormItem): boolean {
  return activeUnitMenu.value === item.key
}

function openUnitMenu(item: FormItem): void {
  if (!item.productId) activeUnitMenu.value = item.key
}

function closeUnitMenu(item: FormItem): void {
  if (unitMenuIsOpen(item)) activeUnitMenu.value = null
}

function filteredUnitOptions(item: FormItem): UnitOfMeasure[] {
  const query = supplierComparisonKey(item.unitOfMeasureTitle)
  if (!query) return units.value.slice(0, 8)

  return units.value
    .filter(unit => supplierComparisonKey(unit.title).includes(query))
    .slice(0, 8)
}

function resolveUnit(item: FormItem): void {
  if (item.productId) return

  const match = units.value.find(
    unit => supplierComparisonKey(unit.title) === supplierComparisonKey(item.unitOfMeasureTitle),
  )
  if (match) {
    item.unitOfMeasureId = match.id
    item.unitOfMeasureTitle = match.title
    return
  }

  item.unitOfMeasureId = null
}

function handleUnitInput(item: FormItem): void {
  clearItemFieldError(item, 'unit')
  openUnitMenu(item)
  resolveUnit(item)
  ensureListRows()
}

function handleUnitBlur(item: FormItem): void {
  resolveUnit(item)
  window.setTimeout(() => closeUnitMenu(item), 120)
}

function chooseUnit(item: FormItem, unit: UnitOfMeasure): void {
  item.unitOfMeasureId = unit.id
  item.unitOfMeasureTitle = unit.title
  clearItemFieldError(item, 'unit')
  closeUnitMenu(item)
  ensureListRows()
}

function handleProductLookupInput(item: FormItem, field: ProductLookupField): void {
  clearItemFieldError(item, field === 'title' ? 'product' : 'code')
  openProductMenu(item, field)
  if (field === 'title') resolveProductByTitle(item)
  else resolveProductByCode(item)
  ensureListRows()
}

function handleProductLookupBlur(item: FormItem, field: ProductLookupField): void {
  if (field === 'title') resolveProductByTitle(item)
  else resolveProductByCode(item)

  window.setTimeout(() => {
    if (productMenuIsOpen(item, field)) closeProductMenu()
  }, 120)
}

function chooseProduct(item: FormItem, product: Product): void {
  selectProduct(item, product)
  closeProductMenu()
}

function resolveProductByTitle(item: FormItem): void {
  const match = products.value.find(product => isSameProductValue(product.title, item.productText))
  if (match) {
    selectProduct(item, match)
    return
  }

  if (item.productId) {
    item.productId = null
    item.productCode = ''
    item.unitOfMeasureId = null
    item.unitOfMeasureTitle = ''
    item.description = ''
  }
}

function resolveProductByCode(item: FormItem): void {
  const match = products.value.find(product => isSameProductValue(product.code, item.productCode))
  if (match) {
    selectProduct(item, match)
    return
  }

  if (item.productId) {
    item.productId = null
    item.productText = ''
    item.unitOfMeasureId = null
    item.unitOfMeasureTitle = ''
    item.description = ''
  }
}

function validateForm(): boolean {
  clearValidationErrors()
  if (!form.enteredAtLocal) validationErrors.enteredAtLocal = 'تاریخ و ساعت ورود کالا را وارد کنید.'
  if (!form.delivererText.trim()) {
    validationErrors.deliverer = 'تحویل‌دهنده را وارد یا انتخاب کنید.'
  } else if (isNewDeliverer() && !canManageMasterData) {
    validationErrors.deliverer = 'مجوز افزودن تحویل‌دهنده جدید را ندارید؛ از فهرست انتخاب کنید.'
  }
  if (!form.pageNumber.trim()) validationErrors.pageNumber = 'شماره صفحه را وارد کنید.'
  if (!form.warehouseId) validationErrors.warehouse = 'انبار را انتخاب کنید.'
  if (canManageReceiptInvoice) {
    if (invoiceHasNumber.value && !form.invoiceNumber.trim()) {
      validationErrors.invoiceNumber = 'شماره فاکتور را وارد کنید.'
    }
    if (isNewSupplier() && !canManageMasterData) {
      validationErrors.supplier = 'مجوز افزودن تأمین‌کننده جدید را ندارید؛ از فهرست انتخاب کنید.'
    }
  }

  const items = itemsForSubmission()
  if (!items.length) validationErrors.items = 'حداقل یک کالا وارد کنید.'

  items.forEach(item => {
    if (!item.productText.trim()) {
      validationErrors[itemFieldKey(item, 'product')] = 'عنوان کالا را وارد کنید.'
    } else if (!item.productId && !canManageMasterData) {
      validationErrors[itemFieldKey(item, 'product')] = 'کالا را از فهرست انتخاب کنید؛ مجوز افزودن کالای جدید را ندارید.'
    }
    if (!item.quantity || item.quantity <= 0) {
      validationErrors[itemFieldKey(item, 'quantity')] = 'مقدار باید بزرگ‌تر از صفر باشد.'
    }
    if (!item.productId && !item.productCode.trim()) {
      validationErrors[itemFieldKey(item, 'code')] = 'کد کالای جدید را وارد کنید.'
    }
    if (!item.productId && !item.unitOfMeasureId) {
      validationErrors[itemFieldKey(item, 'unit')] = 'واحد را از فهرست موجود انتخاب کنید.'
    }
  })

  const existingProducts = new Map<string, FormItem>()
  items.filter(item => item.productId).forEach(item => {
    const previous = existingProducts.get(item.productId!)
    if (previous) {
      const message = 'این کالا بیش از یک بار در سند وارد شده است.'
      validationErrors[itemFieldKey(previous, 'product')] = message
      validationErrors[itemFieldKey(item, 'product')] = message
    } else {
      existingProducts.set(item.productId!, item)
    }
  })

  const newTitles = new Map<string, FormItem>()
  const newCodes = new Map<string, FormItem>()
  items.filter(item => !item.productId).forEach(item => {
    const titleKey = productComparisonKey(item.productText)
    const codeKey = productComparisonKey(item.productCode)
    if (titleKey) {
      const previousTitle = newTitles.get(titleKey)
      if (previousTitle) {
        const message = 'عنوان کالای جدید تکراری است.'
        validationErrors[itemFieldKey(previousTitle, 'product')] = message
        validationErrors[itemFieldKey(item, 'product')] = message
      } else newTitles.set(titleKey, item)
    }
    if (codeKey) {
      const previousCode = newCodes.get(codeKey)
      if (previousCode) {
        const message = 'کد کالای جدید تکراری است.'
        validationErrors[itemFieldKey(previousCode, 'code')] = message
        validationErrors[itemFieldKey(item, 'code')] = message
      } else newCodes.set(codeKey, item)
    }
  })

  return Object.keys(validationErrors).length === 0
}

function applyApiFieldErrors(error: GoodsReceiptsApiError): boolean {
  let applied = false
  const items = itemsForSubmission()
  Object.entries(error.fieldErrors).forEach(([path, messages]) => {
    const message = messages.find(Boolean)
    if (!message) return
    const normalizedPath = path.toLowerCase()
    let key = ''
    const itemMatch = normalizedPath.match(/items\[(\d+)]\.(.+)$/)
    if (itemMatch) {
      const item = items[Number(itemMatch[1])]
      const property = itemMatch[2] ?? ''
      if (item) {
        if (property.includes('producttitle') || property.includes('productid')) key = itemFieldKey(item, 'product')
        else if (property.includes('productcode')) key = itemFieldKey(item, 'code')
        else if (property.includes('unitofmeasure')) key = itemFieldKey(item, 'unit')
        else if (property.includes('quantity')) key = itemFieldKey(item, 'quantity')
        else if (property.includes('requestnumber')) key = itemFieldKey(item, 'requestNumber')
        else if (property.includes('description')) key = itemFieldKey(item, 'description')
      }
    }
    else if (normalizedPath.includes('enteredatutc')) key = 'enteredAtLocal'
    else if (normalizedPath.includes('deliverer')) key = 'deliverer'
    else if (normalizedPath.includes('warehouse')) key = 'warehouse'
    else if (normalizedPath.includes('pagenumber')) key = 'pageNumber'
    else if (normalizedPath.includes('invoicenumber')) key = 'invoiceNumber'
    else if (normalizedPath.includes('invoicestatus')) key = 'invoiceStatus'
    else if (normalizedPath.includes('supplier')) key = 'supplier'
    else if (normalizedPath.includes('receiptnumber')) key = 'receiptNumber'
    else if (normalizedPath.includes('description')) key = 'description'
    else if (normalizedPath.endsWith('items')) key = 'items'
    if (key) {
      validationErrors[key] = message
      applied = true
    }
  })
  return applied
}

async function saveReceipt(): Promise<void> {
  if (!validateForm()) {
    formError.value = ''
    return
  }

  operationRequest?.abort()
  const controller = new AbortController()
  operationRequest = controller
  saving.value = true
  formError.value = ''
  try {
    const items = itemsForSubmission()
    await createGoodsReceipt({
      enteredAtUtc: new Date(form.enteredAtLocal).toISOString(),
      description: form.description.trim() || null,
      delivererId: form.delivererId || null,
      delivererFullName: form.delivererId ? null : form.delivererText.trim(),
      warehouseId: form.warehouseId,
      invoiceStatus: canManageReceiptInvoice ? form.invoiceStatus : 'DoesNotHave',
      invoiceNumber: canManageReceiptInvoice && invoiceHasNumber.value ? form.invoiceNumber.trim() : null,
      supplierId: canManageReceiptInvoice ? (form.supplierId || null) : null,
      supplierTitle: canManageReceiptInvoice && !form.supplierId ? (form.supplierText.trim() || null) : null,
      receiptNumber: canManageReceiptInvoice ? (form.receiptNumber.trim() || null) : null,
      pageNumber: form.pageNumber.trim(),
      items: items.map(item => ({
        productId: item.productId,
        productTitle: item.productId ? null : item.productText.trim(),
        productCode: item.productId ? null : item.productCode.trim(),
        unitOfMeasureId: item.productId ? null : item.unitOfMeasureId,
        quantity: Number(item.quantity),
        requestNumber: item.requestNumber.trim() || null,
        description: item.description.trim() || null,
      })),
    }, controller.signal)
    modalOpen.value = false
    referenceDataLoaded.value = false
    products.value = []
    people.value = []
    suppliers.value = []
    units.value = []
    toast.success('ورود کالا با موفقیت ثبت شد.')
    table.value?.dt.ajax.reload(undefined, true)
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') return
    if (await redirectWhenUnauthorized(error)) return
    if (error instanceof GoodsReceiptsApiError && applyApiFieldErrors(error)) {
      formError.value = ''
    } else {
      formError.value = error instanceof Error ? error.message : 'ثبت ورود کالا انجام نشد.'
    }
  } finally {
    if (operationRequest === controller) saving.value = false
  }
}

async function removeReceipt(receiptId: string, pageNumber: string): Promise<void> {
  if (!confirmDialog.value) return
  const confirmed = await confirmDialog.value.ask({
    title: 'حذف سند ورود کالا',
    text: `سند صفحه «${pageNumber}» حذف شود؟`,
    icon: 'warning',
    confirmButtonText: 'حذف',
    cancelButtonText: 'انصراف',
    confirmButtonColor: '#dc3545',
  })
  if (!confirmed) return

  try {
    await deleteGoodsReceipt(receiptId)
    toast.success('سند ورود کالا حذف شد.')
    table.value?.dt.ajax.reload(undefined, false)
  } catch (error) {
    if (await redirectWhenUnauthorized(error)) return
    toast.error(error instanceof Error ? error.message : 'حذف سند انجام نشد.')
  }
}

function removeReceiptRow(row: GoodsReceipt | GoodsReceiptItemRow): Promise<void> {
  return removeReceipt('goodsReceiptId' in row ? row.goodsReceiptId : row.id, row.pageNumber)
}

async function redirectWhenUnauthorized(error: unknown): Promise<boolean> {
  const unauthorized = error instanceof GoodsReceiptsApiError && error.status === 401
  if (!unauthorized) return false
  await router.replace({ name: 'login', query: { redirect: '/dashboard/inventory/goods-receipts' } })
  return true
}

function formatDate(value: string): string {
  return new Intl.DateTimeFormat('fa-IR', {
    year: 'numeric', month: '2-digit', day: '2-digit',
  }).format(new Date(value))
}

function formatQuantity(value: number): string {
  return new Intl.NumberFormat('fa-IR', { maximumFractionDigits: 3 }).format(value)
}

watch(modalOpen, value => document.body.classList.toggle('modal-open', value))
watch(tableMode, () => {
  closeColumnSettings()
  disconnectCompactHorizontalScrollbar()
  scheduleCompactHorizontalScrollbar()
})
watch(() => form.invoiceStatus, status => {
  if (status === 'DoesNotHave') form.invoiceNumber = ''
})

onMounted(() => {
  void loadReferenceData()
  document.addEventListener('click', closeColumnSettings)
})
onBeforeUnmount(() => {
  listRequest?.abort()
  operationRequest?.abort()
  referenceRequest?.abort()
  disconnectCompactHorizontalScrollbar()
  document.removeEventListener('click', closeColumnSettings)
  document.body.classList.remove('modal-open')
})
</script>

<template>
  <ConfirmDialog ref="confirmDialog" />

  <div class="container-fluid users-page goods-receipts-page">
    <div class="page-titles">
      <ol class="breadcrumb">
        <li class="breadcrumb-item"><RouterLink to="/dashboard">داشبورد</RouterLink></li>
        <li class="breadcrumb-item active"><span>تاریخچه ورود کالا</span></li>
      </ol>
    </div>

    <div class="card">
      <div class="card-header">
        <div>
          <h4 class="card-title"><i class="las la-dolly-flatbed mr-1"></i> تاریخچه ورود کالا</h4>
          <p class="page-description mb-0 mt-1 text-muted">ثبت اسناد ورود و ردیف‌های مستقل کالا</p>
        </div>
        <div class="goods-receipts-toolbar">
          <label class="goods-receipts-view-mode mb-0">
            <span>نوع نمایش</span>
            <select v-model="tableMode" class="form-control form-control-sm" aria-label="نوع نمایش جدول">
              <option value="items">بر اساس کالا</option>
              <option value="receipts">بر اساس ورودی</option>
            </select>
          </label>
          <div class="goods-receipts-column-settings" @click.stop>
            <button
              class="btn btn-outline-primary btn-sm goods-receipts-column-trigger"
              type="button"
              title="تنظیم ستون‌ها"
              aria-label="تنظیم ستون‌ها"
              :aria-expanded="columnSettingsOpen"
              @click="toggleColumnSettings"
            >
              <i class="las la-columns" aria-hidden="true"></i>
            </button>
            <div v-if="columnSettingsOpen" class="goods-receipts-column-menu">
              <div class="goods-receipts-column-menu__title">ستون‌های قابل نمایش</div>
              <label v-for="column in activeColumnSettings" :key="column.key" class="goods-receipts-column-option">
                <input
                  type="checkbox"
                  :checked="columnIsVisible(column.key)"
                  @change="toggleColumnVisibility(column)"
                />
                <span>{{ column.label }}</span>
              </label>
            </div>
          </div>
          <button
            v-if="canInitialEntry"
            class="btn btn-primary btn-sm mobile-icon-button"
            type="button"
            title="ثبت ورود کالا"
            :disabled="referenceLoading"
            @click="openCreateModal"
          >
            <i class="las la-plus ml-1"></i>
            <span class="mobile-button-label">ثبت ورود کالا</span>
          </button>
        </div>
      </div>

      <div class="card-body">
        <div
          ref="tableContainer"
          :class="['users-table-container', 'goods-receipts-table-container', `goods-receipts-table-container--${tableMode}`]"
          @wheel="handleTableWheel"
        >
          <DataTable
            :key="tableMode"
            ref="table"
            :options="tableMode === 'items' ? itemTableOptions : receiptTableOptions"
            class="display nowrap table-striped table table-sm users-table"
          >
            <template #column-enteredAtUtc="{ cellData }">
              <strong class="text-black">{{ formatDate(cellData) }}</strong>
            </template>
            <template #column-pageNumber="{ cellData }"><span dir="ltr">{{ cellData }}</span></template>
            <template #column-warehouseTitle="{ cellData }">{{ cellData }}</template>
            <template #column-itemCount="{ cellData }">
              <span class="badge light badge-info">{{ (cellData as GoodsReceipt['items']).length }} کالا</span>
            </template>
            <template #column-productTitle="{ cellData }"><strong class="text-black">{{ cellData }}</strong></template>
            <template #column-productCode="{ cellData }"><span dir="ltr">{{ cellData }}</span></template>
            <template #column-quantity="{ cellData }"><strong>{{ formatQuantity(cellData) }}</strong></template>
            <template #column-unitOfMeasureTitle="{ cellData }">{{ cellData }}</template>
            <template #column-requestNumber="{ cellData }"><span dir="ltr">{{ cellData || '—' }}</span></template>
            <template #column-itemDescription="{ cellData }">
              <span class="goods-receipts-description-cell" :title="cellData || ''">{{ cellData || '—' }}</span>
            </template>
            <template #column-invoiceNumber="{ cellData }"><span dir="ltr">{{ cellData || '—' }}</span></template>
            <template #column-counterparty="{ cellData }">{{ cellData || '—' }}</template>
            <template #column-receiptNumber="{ cellData }"><span dir="ltr">{{ cellData || '—' }}</span></template>
            <template #column-description="{ cellData }">
              <span class="goods-receipts-description-cell" :title="cellData || ''">{{ cellData || '—' }}</span>
            </template>
            <template #column-receiptDescription="{ cellData }">
              <span class="goods-receipts-description-cell" :title="cellData || ''">{{ cellData || '—' }}</span>
            </template>
            <template #column-invoiceStatus="{ cellData }">
              <span class="badge light" :class="cellData === 'HasInvoice' ? 'badge-success' : 'badge-secondary'">
                {{ cellData === 'HasInvoice' ? 'دارد' : 'ندارد' }}
              </span>
            </template>
            <template v-if="canInitialEntry" #column-actions="{ rowData }">
              <button class="master-data-row-action receipt-remove-item" type="button" title="حذف سند" aria-label="حذف سند" @click="removeReceiptRow(rowData)">
                <i class="las la-trash" aria-hidden="true"></i>
              </button>
            </template>
            <thead>
              <tr v-if="tableMode === 'items'">
                <th>ردیف</th><th>تاریخ</th><th>عنوان کالا</th><th>کد کالا</th><th>تعداد</th><th>واحد</th><th>توضیحات</th><th>تحویل‌دهنده</th><th>فاکتور</th><th>شماره فاکتور</th><th>طرف حساب</th><th>شماره رسید</th><th>انبار</th><th>شماره صفحه</th><th v-if="canInitialEntry">عملیات</th>
              </tr>
              <tr v-else>
                <th>ردیف</th><th>تاریخ ورود</th><th>شماره صفحه</th><th>تحویل‌دهنده</th><th>انبار</th><th>تعداد کالا</th><th>فاکتور</th><th>شماره فاکتور</th><th>طرف حساب</th><th>شماره رسید</th><th>توضیحات ورودی</th><th v-if="canInitialEntry">عملیات</th>
              </tr>
            </thead>
          </DataTable>
        </div>
      </div>
    </div>
  </div>

  <Teleport to="body">
    <template v-if="modalOpen">
      <div class="modal show d-block goods-receipt-modal" tabindex="-1" role="dialog" aria-modal="true" @click.self="closeModal">
        <div class="modal-dialog modal-xl modal-dialog-centered modal-dialog-scrollable" role="document">
          <div class="modal-content">
            <div class="modal-header">
              <h5 class="modal-title"><i class="las la-dolly-flatbed ml-1"></i> ثبت ورود کالا</h5>
              <button type="button" class="close" aria-label="بستن" :disabled="saving" @click="closeModal"><span>&times;</span></button>
            </div>

            <form novalidate @submit.prevent="saveReceipt">
              <div class="modal-body">
                <div v-if="formError" class="alert alert-danger py-2">{{ formError }}</div>
                <div class="row">
                  <div class="col-md-3 form-group">
                    <label>تاریخ و ساعت ورود <span class="text-danger">*</span></label>

                    <DatePicker v-model="form.enteredAtLocal"
                                @input="clearFieldError('enteredAtLocal')"
                                type="date"
                                format="YYYY-MM-DDTHH:mm:ss"
                                display-format="jYYYY/jMM/jDD HH:mm"
                                placeholder="تاریخ و ساعت را انتخاب کنید"
                                :editable="true"
                                :class="{ 'is-invalid': fieldError('enteredAtLocal') }"
                                :clearable="true"
                                :disabled="saving"
                                />

                    <!--<input v-model="form.enteredAtLocal" class="form-control"
                           :class="{ 'is-invalid': fieldError('enteredAtLocal') }"
                           type="datetime-local" :disabled="saving"
                           @input="clearFieldError('enteredAtLocal')" />-->

                    <small v-if="fieldError('enteredAtLocal')" class="invalid-feedback d-block">{{ fieldError('enteredAtLocal') }}</small>
                  </div>
                  <div class="col-md-3 form-group">
                    <label>انبار <span class="text-danger">*</span></label>
                    <select v-model="form.warehouseId" class="form-control" :class="{ 'is-invalid': fieldError('warehouse') }" :disabled="saving" @change="clearFieldError('warehouse')">
                      <option value="">انتخاب کنید</option>
                      <option v-for="warehouse in warehouses" :key="warehouse.id" :value="warehouse.id">{{ warehouse.title }}</option>
                    </select>
                    <small v-if="fieldError('warehouse')" class="invalid-feedback d-block">{{ fieldError('warehouse') }}</small>
                  </div>
                  <div class="col-md-3 form-group">
                    <label>شماره صفحه <span class="text-danger">*</span></label>
                    <input v-model="form.pageNumber" class="form-control" :class="{ 'is-invalid': fieldError('pageNumber') }" type="text" maxlength="100" :disabled="saving" @input="clearFieldError('pageNumber')" />
                    <small v-if="fieldError('pageNumber')" class="invalid-feedback d-block">{{ fieldError('pageNumber') }}</small>
                  </div>
                  <div class="col-md-3 form-group">
                    <label>تحویل‌دهنده <span class="text-danger">*</span></label>
                    <div class="receipt-product-combobox">
                      <input
                        v-model="form.delivererText"
                        class="form-control"
                        :class="{ 'is-invalid': fieldError('deliverer') }"
                        type="text"
                        role="combobox"
                        aria-autocomplete="list"
                        :aria-expanded="delivererMenuOpen"
                        autocomplete="off"
                        maxlength="200"
                        placeholder="تایپ یا انتخاب تحویل‌دهنده"
                        :disabled="saving"
                        @focus="delivererMenuOpen = true"
                        @input="handleDelivererInput"
                        @blur="handleDelivererBlur"
                        @keydown.esc="delivererMenuOpen = false"
                      />
                      <i class="las la-angle-down" aria-hidden="true"></i>
                      <div v-if="delivererMenuOpen" class="receipt-product-options" role="listbox">
                        <button
                          v-for="person in filteredDelivererOptions()"
                          :key="person.id"
                          class="receipt-product-option"
                          type="button"
                          role="option"
                          @mousedown.prevent="chooseDeliverer(person)"
                        >
                          <span class="receipt-product-option__title">{{ person.fullName }}</span>
                        </button>
                        <div v-if="!filteredDelivererOptions().length" class="receipt-product-empty">
                          تحویل‌دهنده‌ای پیدا نشد.
                        </div>
                      </div>
                    </div>
                    <small v-if="fieldError('deliverer')" class="invalid-feedback d-block">{{ fieldError('deliverer') }}</small>
                    <small
                      v-if="isNewDeliverer()"
                      class="receipt-new-master-data"
                      :class="{ 'receipt-new-master-data--denied': !canManageMasterData }"
                    >
                      <i :class="canManageMasterData ? 'las la-plus-circle' : 'las la-exclamation-circle'" aria-hidden="true"></i>
                      {{ canManageMasterData
                        ? 'تحویل‌دهنده جدید هنگام ثبت اضافه می‌شود.'
                        : 'مجوز افزودن تحویل‌دهنده جدید را ندارید؛ از فهرست انتخاب کنید.' }}
                    </small>
                  </div>
                  <template v-if="canManageReceiptInvoice">
                  <div class="col-md-3 form-group">
                    <label>وضعیت فاکتور</label>
                    <select v-model="form.invoiceStatus" class="form-control" :class="{ 'is-invalid': fieldError('invoiceStatus') }" :disabled="saving" @change="clearFieldError('invoiceStatus'); clearFieldError('invoiceNumber')">
                      <option value="DoesNotHave">ندارد</option>
                      <option value="HasInvoice">دارد</option>
                    </select>
                    <small v-if="fieldError('invoiceStatus')" class="invalid-feedback d-block">{{ fieldError('invoiceStatus') }}</small>
                  </div>
                  <div class="col-md-3 form-group">
                    <label>شماره فاکتور <span v-if="invoiceHasNumber" class="text-danger">*</span></label>
                    <input v-model="form.invoiceNumber" class="form-control" :class="{ 'is-invalid': fieldError('invoiceNumber') }" type="text" maxlength="100" :disabled="saving || !invoiceHasNumber" @input="clearFieldError('invoiceNumber')" />
                    <small v-if="fieldError('invoiceNumber')" class="invalid-feedback d-block">{{ fieldError('invoiceNumber') }}</small>
                  </div>
                  <div class="col-md-3 form-group">
                    <label>طرف حساب</label>
                    <div class="receipt-product-combobox">
                      <input
                        v-model="form.supplierText"
                        class="form-control"
                        :class="{ 'is-invalid': fieldError('supplier') }"
                        type="text"
                        role="combobox"
                        aria-autocomplete="list"
                        :aria-expanded="supplierMenuOpen"
                        autocomplete="off"
                        maxlength="200"
                        placeholder="تایپ یا انتخاب تأمین‌کننده"
                        :disabled="saving"
                        @focus="supplierMenuOpen = true"
                        @input="handleSupplierInput"
                        @blur="handleSupplierBlur"
                        @keydown.esc="supplierMenuOpen = false"
                      />
                      <i class="las la-angle-down" aria-hidden="true"></i>
                      <div v-if="supplierMenuOpen" class="receipt-product-options" role="listbox">
                        <button
                          v-for="supplier in filteredSupplierOptions()"
                          :key="supplier.id"
                          class="receipt-product-option"
                          type="button"
                          role="option"
                          @mousedown.prevent="chooseSupplier(supplier)"
                        >
                          <span class="receipt-product-option__title">{{ supplier.title }}</span>
                        </button>
                        <div v-if="!filteredSupplierOptions().length" class="receipt-product-empty">
                          تأمین‌کننده‌ای پیدا نشد.
                        </div>
                      </div>
                    </div>
                    <small v-if="fieldError('supplier')" class="invalid-feedback d-block">{{ fieldError('supplier') }}</small>
                    <small
                      v-if="isNewSupplier()"
                      class="receipt-new-master-data"
                      :class="{ 'receipt-new-master-data--denied': !canManageMasterData }"
                    >
                      <i :class="canManageMasterData ? 'las la-plus-circle' : 'las la-exclamation-circle'" aria-hidden="true"></i>
                      {{ canManageMasterData
                        ? 'تأمین‌کننده جدید هنگام ثبت اضافه می‌شود.'
                        : 'مجوز افزودن تأمین‌کننده جدید را ندارید؛ از فهرست انتخاب کنید.' }}
                    </small>
                  </div>
                  <div class="col-md-3 form-group">
                    <label>شماره رسید</label>
                    <input v-model="form.receiptNumber" class="form-control" :class="{ 'is-invalid': fieldError('receiptNumber') }" type="text" maxlength="100" :disabled="saving" @input="clearFieldError('receiptNumber')" />
                    <small v-if="fieldError('receiptNumber')" class="invalid-feedback d-block">{{ fieldError('receiptNumber') }}</small>
                  </div>
                  </template>
                  <div class="col-md-12 form-group">
                    <label>توضیحات</label>
                    <textarea v-model="form.description" class="form-control" :class="{ 'is-invalid': fieldError('description') }" rows="2" maxlength="1000" :disabled="saving" @input="clearFieldError('description')"></textarea>
                    <small v-if="fieldError('description')" class="invalid-feedback d-block">{{ fieldError('description') }}</small>
                  </div>
                </div>

                <div class="receipt-items-header mb-2">
                  <div>
                    <h6 class="mb-0">کالاها و مقدار</h6>
                    <small v-if="goodsEntryMode === 'list'" class="text-muted">مانند اکسل اطلاعات را وارد کنید؛ همیشه یک ردیف خالی در انتهای جدول آماده است.</small>
                  </div>
                  <div class="receipt-entry-actions">
                    <div class="btn-group btn-group-sm receipt-entry-mode" role="group" aria-label="روش ثبت کالا">
                      <button
                        class="btn"
                        :class="goodsEntryMode === 'cards' ? 'btn-primary' : 'btn-outline-primary'"
                        type="button"
                        :disabled="saving"
                        @click="setGoodsEntryMode('cards')"
                      >
                        <i class="las la-stream ml-1" aria-hidden="true"></i>
                        ثبت ردیفی
                      </button>
                      <button
                        class="btn"
                        :class="goodsEntryMode === 'list' ? 'btn-primary' : 'btn-outline-primary'"
                        type="button"
                        :disabled="saving"
                        @click="setGoodsEntryMode('list')"
                      >
                        <i class="las la-table ml-1" aria-hidden="true"></i>
                        ثبت لیستی کالا
                      </button>
                    </div>
                    <button class="btn btn-outline-primary btn-sm" type="button" :disabled="saving" @click="addItem"><i class="las la-plus ml-1"></i> افزودن ردیف</button>
                  </div>
                </div>
                <small v-if="fieldError('items')" class="invalid-feedback d-block mb-2">{{ fieldError('items') }}</small>
                <div v-if="goodsEntryMode === 'list'" class="receipt-list-entry">
                  <div class="receipt-list-table-scroll">
                    <table class="receipt-list-table" role="grid" aria-label="ثبت لیستی کالاها">
                      <thead>
                        <tr>
                          <th class="receipt-list-row-number">ردیف</th>
                          <th class="receipt-list-product-cell">عنوان کالا <span class="text-danger">*</span></th>
                          <th class="receipt-list-code-cell">کد کالا <span class="text-danger">*</span></th>
                          <th class="receipt-list-unit-cell">واحد <span class="text-danger">*</span></th>
                          <th class="receipt-list-quantity-cell">مقدار <span class="text-danger">*</span></th>
                          <th class="receipt-list-request-cell">شماره درخواست</th>
                          <th class="receipt-list-description-cell">توضیحات کالا</th>
                          <th class="receipt-list-action-column">عملیات</th>
                        </tr>
                      </thead>
                      <tbody>
                        <tr
                          v-for="(item, index) in form.items"
                          :key="item.key"
                          :class="{
                            'receipt-list-row--empty': !itemHasContent(item),
                            'receipt-list-row--new-product': isNewProduct(item),
                          }"
                        >
                          <td class="receipt-list-row-number">{{ index + 1 }}</td>
                          <td class="receipt-list-product-cell">
                            <div class="receipt-product-combobox">
                              <input
                                v-model="item.productText"
                                class="form-control form-control-sm"
                                :class="{ 'is-invalid': itemFieldError(item, 'product') }"
                                type="text"
                                role="combobox"
                                aria-autocomplete="list"
                                :aria-expanded="productMenuIsOpen(item, 'title')"
                                aria-label="عنوان کالا"
                                autocomplete="off"
                                :disabled="saving"
                                @focus="openProductMenu(item, 'title')"
                                @input="handleProductLookupInput(item, 'title')"
                                @blur="handleProductLookupBlur(item, 'title')"
                                @keydown.esc="closeProductMenu"
                              />
                              <i class="las la-angle-down" aria-hidden="true"></i>
                              <div v-if="productMenuIsOpen(item, 'title')" class="receipt-product-options" role="listbox">
                                <button
                                  v-for="product in filteredProductOptions(item, 'title')"
                                  :key="product.id"
                                  class="receipt-product-option"
                                  type="button"
                                  role="option"
                                  @mousedown.prevent="chooseProduct(item, product)"
                                >
                                  <span class="receipt-product-option__title">{{ product.title }}</span>
                                  <span class="receipt-product-option__meta">
                                    <span dir="ltr">{{ product.code }}</span>
                                    <span>{{ product.unitOfMeasureTitle }}</span>
                                  </span>
                                </button>
                                <div v-if="!filteredProductOptions(item, 'title').length" class="receipt-product-empty">
                                  {{ canManageMasterData
                                    ? 'کالایی پیدا نشد؛ کالای جدید تکمیل می‌شود.'
                                    : 'مجوز افزودن کالای جدید را ندارید.' }}
                                </div>
                              </div>
                            </div>
                            <small v-if="itemFieldError(item, 'product')" class="invalid-feedback d-block receipt-list-cell-error">
                              {{ itemFieldError(item, 'product') }}
                            </small>
                            <small
                              v-if="isNewProduct(item) && !canManageMasterData && !itemFieldError(item, 'product')"
                              class="receipt-list-cell-note receipt-new-master-data--denied"
                            >
                              بدون مجوز افزودن
                            </small>
                          </td>
                          <td class="receipt-list-code-cell">
                            <div v-if="usesProductCodeLookup(item)" class="receipt-product-combobox">
                              <input
                                v-model="item.productCode"
                                class="form-control form-control-sm"
                                :class="{ 'is-invalid': itemFieldError(item, 'code') }"
                                type="text"
                                role="combobox"
                                aria-autocomplete="list"
                                :aria-expanded="productMenuIsOpen(item, 'code')"
                                aria-label="کد کالا"
                                autocomplete="off"
                                maxlength="100"
                                :disabled="saving"
                                @focus="openProductMenu(item, 'code')"
                                @input="handleProductLookupInput(item, 'code')"
                                @blur="handleProductLookupBlur(item, 'code')"
                                @keydown.esc="closeProductMenu"
                              />
                              <i class="las la-angle-down" aria-hidden="true"></i>
                              <div v-if="productMenuIsOpen(item, 'code')" class="receipt-product-options receipt-product-options--code" role="listbox">
                                <button
                                  v-for="product in filteredProductOptions(item, 'code')"
                                  :key="product.id"
                                  class="receipt-product-option"
                                  type="button"
                                  role="option"
                                  @mousedown.prevent="chooseProduct(item, product)"
                                >
                                  <span class="receipt-product-option__title" dir="ltr">{{ product.code }}</span>
                                  <span class="receipt-product-option__meta">
                                    <span>{{ product.title }}</span>
                                    <span>{{ product.unitOfMeasureTitle }}</span>
                                  </span>
                                </button>
                                <div v-if="!filteredProductOptions(item, 'code').length" class="receipt-product-empty">کدی پیدا نشد.</div>
                              </div>
                            </div>
                            <input
                              v-else
                              v-model="item.productCode"
                              class="form-control form-control-sm"
                              :class="{ 'is-invalid': itemFieldError(item, 'code') }"
                              type="text"
                              maxlength="100"
                              aria-label="کد کالای جدید"
                              autocomplete="off"
                              :disabled="saving"
                              @input="handleNewProductCodeInput(item)"
                            />
                            <small v-if="itemFieldError(item, 'code')" class="invalid-feedback d-block receipt-list-cell-error">
                              {{ itemFieldError(item, 'code') }}
                            </small>
                          </td>
                          <td class="receipt-list-unit-cell">
                            <div class="receipt-product-combobox">
                              <input
                                v-model="item.unitOfMeasureTitle"
                                class="form-control form-control-sm"
                                :class="{ 'is-invalid': itemFieldError(item, 'unit') }"
                                type="text"
                                role="combobox"
                                aria-autocomplete="list"
                                :aria-expanded="unitMenuIsOpen(item)"
                                aria-label="واحد اندازه‌گیری"
                                autocomplete="off"
                                maxlength="200"
                                :readonly="Boolean(item.productId)"
                                :disabled="saving"
                                @focus="openUnitMenu(item)"
                                @input="handleUnitInput(item)"
                                @blur="handleUnitBlur(item)"
                                @keydown.esc="closeUnitMenu(item)"
                              />
                              <i class="las la-angle-down" aria-hidden="true"></i>
                              <div v-if="unitMenuIsOpen(item)" class="receipt-product-options" role="listbox">
                                <button
                                  v-for="unit in filteredUnitOptions(item)"
                                  :key="unit.id"
                                  class="receipt-product-option"
                                  type="button"
                                  role="option"
                                  @mousedown.prevent="chooseUnit(item, unit)"
                                >
                                  <span class="receipt-product-option__title">{{ unit.title }}</span>
                                </button>
                                <div v-if="!filteredUnitOptions(item).length" class="receipt-product-empty">فقط واحدهای موجود قابل انتخاب‌اند.</div>
                              </div>
                            </div>
                            <small v-if="itemFieldError(item, 'unit')" class="invalid-feedback d-block receipt-list-cell-error">
                              {{ itemFieldError(item, 'unit') }}
                            </small>
                            <small v-else-if="!item.productId && item.unitOfMeasureTitle.trim() && !item.unitOfMeasureId" class="receipt-list-cell-note receipt-new-master-data--denied">واحد نامعتبر</small>
                          </td>
                          <td class="receipt-list-quantity-cell">
                            <input v-model.number="item.quantity" class="form-control form-control-sm" :class="{ 'is-invalid': itemFieldError(item, 'quantity') }" type="number" step="any" inputmode="decimal" aria-label="مقدار" :disabled="saving" @input="handleQuantityInput(item)" />
                            <small v-if="itemFieldError(item, 'quantity')" class="invalid-feedback d-block receipt-list-cell-error">
                              {{ itemFieldError(item, 'quantity') }}
                            </small>
                          </td>
                          <td class="receipt-list-request-cell">
                            <input v-model="item.requestNumber" class="form-control form-control-sm" :class="{ 'is-invalid': itemFieldError(item, 'requestNumber') }" type="text" maxlength="100" :disabled="saving" @input="handleListCellInput(item, 'requestNumber')" />
                            <small v-if="itemFieldError(item, 'requestNumber')" class="invalid-feedback d-block receipt-list-cell-error">
                              {{ itemFieldError(item, 'requestNumber') }}
                            </small>
                          </td>
                          <td class="receipt-list-description-cell">
                            <textarea
                              v-model="item.description"
                              class="form-control form-control-sm receipt-list-description-input"
                              :class="{ 'is-invalid': itemFieldError(item, 'description') }"
                              rows="1"
                              maxlength="1000"
                              :data-list-description="item.key"
                              :disabled="saving"
                              @input="handleListDescriptionInput(item)"
                            ></textarea>
                            <small v-if="itemFieldError(item, 'description')" class="invalid-feedback d-block receipt-list-cell-error">
                              {{ itemFieldError(item, 'description') }}
                            </small>
                          </td>
                          <td class="receipt-list-action-column">
                            <button class="master-data-row-action receipt-remove-item" type="button" title="پاک‌کردن ردیف" aria-label="پاک‌کردن ردیف" :disabled="saving" @click="removeItem(index)"><i class="las la-trash"></i></button>
                          </td>
                        </tr>
                      </tbody>
                    </table>
                  </div>
                </div>
                <template v-if="goodsEntryMode === 'cards'">
                <div
                  v-for="(item, index) in form.items"
                  :key="item.key"
                  class="receipt-item-row"
                  :class="{ 'receipt-item-row--new-product': isNewProduct(item) }"
                >
                  <div class="receipt-item-primary-row">
                    <div class="form-group mb-0 receipt-product-field">
                      <label>عنوان کالا <span class="text-danger">*</span></label>
                      <div class="receipt-product-combobox">
                        <input
                          v-model="item.productText"
                          class="form-control"
                          :class="{ 'is-invalid': itemFieldError(item, 'product') }"
                          type="text"
                          role="combobox"
                          aria-autocomplete="list"
                          :aria-expanded="productMenuIsOpen(item, 'title')"
                          autocomplete="off"
                          placeholder="تایپ یا انتخاب کالا"
                          :disabled="saving"
                          @focus="openProductMenu(item, 'title')"
                          @input="handleProductLookupInput(item, 'title')"
                          @blur="handleProductLookupBlur(item, 'title')"
                          @keydown.esc="closeProductMenu"
                        />
                        <i class="las la-angle-down" aria-hidden="true"></i>
                        <div v-if="productMenuIsOpen(item, 'title')" class="receipt-product-options" role="listbox">
                          <button
                            v-for="product in filteredProductOptions(item, 'title')"
                            :key="product.id"
                            class="receipt-product-option"
                            type="button"
                            role="option"
                            @mousedown.prevent="chooseProduct(item, product)"
                          >
                            <span class="receipt-product-option__title">{{ product.title }}</span>
                            <span class="receipt-product-option__meta">
                              <span dir="ltr">{{ product.code }}</span>
                              <span>{{ product.unitOfMeasureTitle }}</span>
                            </span>
                          </button>
                          <div v-if="!filteredProductOptions(item, 'title').length" class="receipt-product-empty">
                            <i class="las la-plus-circle" aria-hidden="true"></i>
                            {{ canManageMasterData
                              ? 'کالایی پیدا نشد؛ اطلاعات کالای جدید را تکمیل کنید.'
                              : 'کالایی پیدا نشد؛ مجوز افزودن کالای جدید را ندارید.' }}
                          </div>
                        </div>
                      </div>
                      <small v-if="itemFieldError(item, 'product')" class="invalid-feedback d-block">
                        {{ itemFieldError(item, 'product') }}
                      </small>
                    </div>
                    <div class="form-group mb-0 receipt-code-field">
                      <label>کد کالا <span v-if="!item.productId" class="text-danger">*</span></label>
                      <div v-if="usesProductCodeLookup(item)" class="receipt-product-combobox">
                        <input
                          v-model="item.productCode"
                          class="form-control"
                          :class="{ 'is-invalid': itemFieldError(item, 'code') }"
                          type="text"
                          role="combobox"
                          aria-autocomplete="list"
                          :aria-expanded="productMenuIsOpen(item, 'code')"
                          autocomplete="off"
                          maxlength="100"
                          placeholder="انتخاب کد"
                          :disabled="saving"
                          @focus="openProductMenu(item, 'code')"
                          @input="handleProductLookupInput(item, 'code')"
                          @blur="handleProductLookupBlur(item, 'code')"
                          @keydown.esc="closeProductMenu"
                        />
                        <i class="las la-angle-down" aria-hidden="true"></i>
                        <div v-if="productMenuIsOpen(item, 'code')" class="receipt-product-options receipt-product-options--code" role="listbox">
                          <button
                            v-for="product in filteredProductOptions(item, 'code')"
                            :key="product.id"
                            class="receipt-product-option"
                            type="button"
                            role="option"
                            @mousedown.prevent="chooseProduct(item, product)"
                          >
                            <span class="receipt-product-option__title" dir="ltr">{{ product.code }}</span>
                            <span class="receipt-product-option__meta">
                              <span>{{ product.title }}</span>
                              <span>{{ product.unitOfMeasureTitle }}</span>
                            </span>
                          </button>
                          <div v-if="!filteredProductOptions(item, 'code').length" class="receipt-product-empty">
                            <i class="las la-plus-circle" aria-hidden="true"></i>
                            {{ canManageMasterData
                              ? 'کدی پیدا نشد؛ اطلاعات کالای جدید را تکمیل کنید.'
                              : 'کدی پیدا نشد؛ مجوز افزودن کالای جدید را ندارید.' }}
                          </div>
                        </div>
                      </div>
                      <input
                        v-else
                        v-model="item.productCode"
                        class="form-control"
                        :class="{ 'is-invalid': itemFieldError(item, 'code') }"
                        type="text"
                        maxlength="100"
                        autocomplete="off"
                        placeholder="کد کالای جدید"
                        :disabled="saving"
                        @input="handleNewProductCodeInput(item)"
                      />
                      <small v-if="itemFieldError(item, 'code')" class="invalid-feedback d-block">
                        {{ itemFieldError(item, 'code') }}
                      </small>
                    </div>
                    <div class="form-group mb-0 receipt-unit-field">
                      <label>واحد <span v-if="!item.productId" class="text-danger">*</span></label>
                      <div class="receipt-product-combobox">
                        <input
                          v-model="item.unitOfMeasureTitle"
                          class="form-control"
                          :class="{ 'is-invalid': itemFieldError(item, 'unit') }"
                          type="text"
                          role="combobox"
                          aria-autocomplete="list"
                          :aria-expanded="unitMenuIsOpen(item)"
                          autocomplete="off"
                          maxlength="200"
                          placeholder="انتخاب واحد"
                          :readonly="Boolean(item.productId)"
                          :disabled="saving"
                          @focus="openUnitMenu(item)"
                          @input="handleUnitInput(item)"
                          @blur="handleUnitBlur(item)"
                          @keydown.esc="closeUnitMenu(item)"
                        />
                        <i class="las la-angle-down" aria-hidden="true"></i>
                        <div v-if="unitMenuIsOpen(item)" class="receipt-product-options" role="listbox">
                          <button
                            v-for="unit in filteredUnitOptions(item)"
                            :key="unit.id"
                            class="receipt-product-option"
                            type="button"
                            role="option"
                            @mousedown.prevent="chooseUnit(item, unit)"
                          >
                            <span class="receipt-product-option__title">{{ unit.title }}</span>
                          </button>
                          <div v-if="!filteredUnitOptions(item).length" class="receipt-product-empty">
                            واحدی پیدا نشد؛ یکی از واحدهای موجود را انتخاب کنید.
                          </div>
                        </div>
                      </div>
                      <small v-if="itemFieldError(item, 'unit')" class="invalid-feedback d-block">
                        {{ itemFieldError(item, 'unit') }}
                      </small>
                      <small v-else-if="!item.productId && item.unitOfMeasureTitle.trim() && !item.unitOfMeasureId" class="receipt-new-master-data receipt-new-master-data--denied">
                        واحد جدید قابل افزودن نیست؛ از فهرست واحدهای موجود انتخاب کنید.
                      </small>
                    </div>
                    <div class="form-group mb-0 receipt-quantity-field">
                      <label>مقدار <span class="text-danger">*</span></label>
                      <input v-model.number="item.quantity" class="form-control" :class="{ 'is-invalid': itemFieldError(item, 'quantity') }" type="number" step="any" inputmode="decimal" :disabled="saving" @input="handleQuantityInput(item)" />
                      <small v-if="itemFieldError(item, 'quantity')" class="invalid-feedback d-block">
                        {{ itemFieldError(item, 'quantity') }}
                      </small>
                    </div>
                    <div class="form-group mb-0 receipt-contract-field">
                      <label>شماره درخواست</label>
                      <input v-model="item.requestNumber" class="form-control" :class="{ 'is-invalid': itemFieldError(item, 'requestNumber') }" type="text" maxlength="100" :disabled="saving" @input="clearItemFieldError(item, 'requestNumber')" />
                      <small v-if="itemFieldError(item, 'requestNumber')" class="invalid-feedback d-block">
                        {{ itemFieldError(item, 'requestNumber') }}
                      </small>
                    </div>
                  </div>

                  <small
                    v-if="isNewProduct(item) && !canManageMasterData"
                    class="receipt-new-master-data receipt-new-master-data--denied receipt-new-product-message"
                  >
                    <i class="las la-exclamation-circle" aria-hidden="true"></i>
                    مجوز افزودن کالای جدید را ندارید؛ از فهرست انتخاب کنید.
                  </small>

                  <div class="receipt-item-secondary-row">
                    <div class="form-group mb-0 receipt-item-description">
                      <label>توضیحات کالا</label>
                      <input v-model="item.description" class="form-control" :class="{ 'is-invalid': itemFieldError(item, 'description') }" type="text" maxlength="1000" :disabled="saving" @input="clearItemFieldError(item, 'description')" />
                      <small v-if="itemFieldError(item, 'description')" class="invalid-feedback d-block">
                        {{ itemFieldError(item, 'description') }}
                      </small>
                    </div>
                    <div class="receipt-item-actions">
                      <button class="master-data-row-action receipt-remove-item" type="button" title="حذف کالا" aria-label="حذف کالا" :disabled="saving || form.items.length === 1" @click="removeItem(index)"><i class="las la-trash"></i></button>
                    </div>
                  </div>
                </div>
                </template>
              </div>

              <div class="modal-footer">
                <button class="btn btn-light px-4" type="button" :disabled="saving" @click="closeModal">انصراف</button>
                <button class="btn btn-primary px-4" type="submit" :disabled="saving"><span v-if="saving" class="spinner-border spinner-border-sm ml-1"></span>ثبت ورود کالا</button>
              </div>
            </form>
          </div>
        </div>
      </div>
      <div class="modal-backdrop show"></div>
    </template>
  </Teleport>

  <Teleport to="body">
    <template v-if="receiptInvoiceModalOpen">
      <div class="modal show d-block" tabindex="-1" role="dialog" aria-modal="true" @click.self="closeReceiptInvoiceModal">
        <div class="modal-dialog modal-lg modal-dialog-centered" role="document">
          <div class="modal-content">
            <div class="modal-header">
              <h5 class="modal-title"><i class="las la-file-invoice ml-1" aria-hidden="true"></i> عملیات رسید و فاکتور</h5>
              <button type="button" class="close" aria-label="بستن" :disabled="saving" @click="closeReceiptInvoiceModal"><span>&times;</span></button>
            </div>

            <form novalidate @submit.prevent="saveReceiptInvoiceOperation">
              <div class="modal-body">
                <div v-if="formError" class="alert alert-danger py-2">{{ formError }}</div>
                <div class="row">
                  <div class="col-md-6 form-group">
                    <label>وضعیت فاکتور</label>
                    <select v-model="form.invoiceStatus" class="form-control" :class="{ 'is-invalid': fieldError('invoiceStatus') }" :disabled="saving" @change="clearFieldError('invoiceStatus'); clearFieldError('invoiceNumber')">
                      <option value="DoesNotHave">ندارد</option>
                      <option value="HasInvoice">دارد</option>
                    </select>
                    <small v-if="fieldError('invoiceStatus')" class="invalid-feedback d-block">{{ fieldError('invoiceStatus') }}</small>
                  </div>
                  <div class="col-md-6 form-group">
                    <label>شماره فاکتور <span v-if="invoiceHasNumber" class="text-danger">*</span></label>
                    <input v-model="form.invoiceNumber" class="form-control" :class="{ 'is-invalid': fieldError('invoiceNumber') }" type="text" maxlength="100" :disabled="saving || !invoiceHasNumber" @input="clearFieldError('invoiceNumber')" />
                    <small v-if="fieldError('invoiceNumber')" class="invalid-feedback d-block">{{ fieldError('invoiceNumber') }}</small>
                  </div>
                  <div class="col-md-6 form-group">
                    <label>طرف حساب</label>
                    <div class="receipt-product-combobox">
                      <input
                        v-model="form.supplierText"
                        class="form-control"
                        :class="{ 'is-invalid': fieldError('supplier') }"
                        type="text"
                        role="combobox"
                        aria-autocomplete="list"
                        :aria-expanded="supplierMenuOpen"
                        autocomplete="off"
                        maxlength="200"
                        placeholder="تایپ یا انتخاب تأمین‌کننده"
                        :disabled="saving"
                        @focus="supplierMenuOpen = true"
                        @input="handleSupplierInput"
                        @blur="handleSupplierBlur"
                        @keydown.esc="supplierMenuOpen = false"
                      />
                      <i class="las la-angle-down" aria-hidden="true"></i>
                      <div v-if="supplierMenuOpen" class="receipt-product-options" role="listbox">
                        <button
                          v-for="supplier in filteredSupplierOptions()"
                          :key="supplier.id"
                          class="receipt-product-option"
                          type="button"
                          role="option"
                          @mousedown.prevent="chooseSupplier(supplier)"
                        >
                          <span class="receipt-product-option__title">{{ supplier.title }}</span>
                        </button>
                        <div v-if="!filteredSupplierOptions().length" class="receipt-product-empty">تأمین‌کننده‌ای پیدا نشد.</div>
                      </div>
                    </div>
                    <small v-if="fieldError('supplier')" class="invalid-feedback d-block">{{ fieldError('supplier') }}</small>
                    <small
                      v-if="isNewSupplier()"
                      class="receipt-new-master-data"
                      :class="{ 'receipt-new-master-data--denied': !canManageMasterData }"
                    >
                      <i :class="canManageMasterData ? 'las la-plus-circle' : 'las la-exclamation-circle'" aria-hidden="true"></i>
                      {{ canManageMasterData
                        ? 'تأمین‌کننده جدید هنگام ثبت اضافه می‌شود.'
                        : 'مجوز افزودن تأمین‌کننده جدید را ندارید؛ از فهرست انتخاب کنید.' }}
                    </small>
                  </div>
                  <div class="col-md-6 form-group">
                    <label>شماره رسید</label>
                    <input v-model="form.receiptNumber" class="form-control" :class="{ 'is-invalid': fieldError('receiptNumber') }" type="text" maxlength="100" :disabled="saving" @input="clearFieldError('receiptNumber')" />
                    <small v-if="fieldError('receiptNumber')" class="invalid-feedback d-block">{{ fieldError('receiptNumber') }}</small>
                  </div>
                </div>
              </div>
              <div class="modal-footer">
                <button class="btn btn-light px-4" type="button" :disabled="saving" @click="closeReceiptInvoiceModal">انصراف</button>
                <button class="btn btn-primary px-4" type="submit" :disabled="saving">
                  <span v-if="saving" class="spinner-border spinner-border-sm ml-1"></span>
                  ثبت اطلاعات
                </button>
              </div>
            </form>
          </div>
        </div>
      </div>
      <div class="modal-backdrop show"></div>
    </template>
  </Teleport>
</template>

<style scoped>
.goods-receipts-toolbar { align-items: center; display: flex; gap: .65rem; }
.goods-receipts-view-mode { align-items: center; background: var(--body-bg); border: 1px solid var(--border-color); border-radius: .55rem; display: flex; gap: .5rem; padding: .25rem .35rem .25rem .6rem; }
.goods-receipts-view-mode > span { color: var(--text-secondary); font-size: .76rem; white-space: nowrap; }
.goods-receipts-view-mode .form-control { background-color: var(--surface); border: 0; box-shadow: none; min-width: 9rem; }
.goods-receipts-column-settings { position: relative; }
.goods-receipts-column-trigger { align-items: center; display: inline-flex; height: 2.1rem; justify-content: center; padding: 0; width: 2.1rem; }
.goods-receipts-column-trigger i { font-size: 1.15rem; }
.goods-receipts-column-menu { background: var(--surface); border: 1px solid var(--border-color); border-radius: .65rem; box-shadow: 0 .75rem 1.75rem rgba(15, 23, 42, .18); left: 0; max-height: 22rem; min-width: 14rem; overflow-y: auto; padding: .4rem; position: absolute; top: calc(100% + .4rem); z-index: 1080; }
.goods-receipts-column-menu__title { border-bottom: 1px solid var(--border-color); color: var(--text-secondary); font-size: .75rem; font-weight: 600; margin-bottom: .25rem; padding: .4rem .5rem .55rem; }
.goods-receipts-column-option { align-items: center; border-radius: .4rem; color: var(--text-primary); cursor: pointer; display: flex; font-size: .82rem; gap: .55rem; margin: 0; padding: .45rem .5rem; white-space: nowrap; }
.goods-receipts-column-option:hover { background: var(--primary-light); color: var(--primary-dark); }
.goods-receipts-column-option input { accent-color: var(--primary); flex: 0 0 auto; }
.receipt-items-header { align-items: center; display: flex; gap: 1rem; justify-content: space-between; }
.receipt-entry-actions { align-items: center; display: flex; flex-wrap: wrap; gap: .55rem; }
.receipt-entry-mode .btn { align-items: center; display: inline-flex; justify-content: center; min-width: 7rem; }
.receipt-list-entry { background: var(--surface); border: 1px solid var(--border-color); border-radius: .65rem; margin-bottom: .75rem; }
.receipt-list-table-scroll { overflow: visible; width: 100%; }
.receipt-list-table { border-collapse: separate; border-spacing: 0; font-size: .74rem; table-layout: fixed; width: 100%; }
.receipt-list-table th { background: var(--body-bg); border-bottom: 1px solid var(--border-color); border-left: 1px solid var(--border-color); color: var(--text-secondary); font-size: .69rem; font-weight: 700; padding: .4rem 0; position: sticky; text-align: center; top: 0; white-space: nowrap; z-index: 4; }
.receipt-list-table th:last-child { border-left: 0; }
.receipt-list-table td { border-bottom: 1px solid var(--border-color); border-left: 1px solid var(--border-color); padding: 0; position: relative; vertical-align: top; }
.receipt-list-table td:last-child { border-left: 0; }
.receipt-list-table tbody tr:last-child td { border-bottom: 0; }
.receipt-list-table .form-control { background: transparent; border-color: transparent; border-radius: 0; box-sizing: border-box; font-size: .73rem; margin: 0; min-height: 1.85rem; min-width: 0; padding: .25rem .35rem; width: 100%; }
.receipt-list-table .receipt-product-combobox .form-control { padding-left: 1.65rem; }
.receipt-list-table .form-control:focus { background: var(--surface); border-color: var(--primary); box-shadow: 0 0 0 .12rem rgba(37, 99, 235, .12); }
.receipt-list-table .form-control.is-invalid { border-color: #dc3545; box-shadow: inset 0 0 0 1px rgba(220, 53, 69, .18); }
.receipt-list-table .receipt-product-options { min-width: 17rem; z-index: 1085; }
.receipt-list-table .receipt-list-product-cell { width: 26%; }
.receipt-list-table .receipt-list-code-cell { width: 11%; }
.receipt-list-table .receipt-list-unit-cell { width: 9%; }
.receipt-list-table .receipt-list-quantity-cell { width: 8%; }
.receipt-list-table .receipt-list-request-cell { width: 9%; }
.receipt-list-table .receipt-list-description-cell { width: 28%; }
.receipt-list-row-number { color: var(--text-secondary); text-align: center !important; width: 4%; }
.receipt-list-action-column { background: var(--surface); text-align: center !important; width: 5%; }
.receipt-list-action-column .master-data-row-action { margin-inline: auto; }
.receipt-list-description-input { field-sizing: content; line-height: 1.35; overflow: hidden; resize: none; white-space: pre-wrap; }
.receipt-list-row--empty { opacity: .68; }
.receipt-list-row--empty:focus-within { opacity: 1; }
.receipt-list-row--new-product td { background-color: rgba(245, 158, 11, .12); }
:global(body[data-app-theme='dark'] .receipt-list-row--new-product td) { background-color: rgba(251, 191, 36, .11); }
.receipt-list-cell-note { color: var(--primary); display: block; font-size: .67rem; line-height: 1.2; margin: .2rem .35rem 0; }
.receipt-list-cell-error { font-size: .65rem; line-height: 1.2; margin: .15rem .3rem .25rem; }
.receipt-item-row { padding: .75rem; margin-bottom: .65rem; border: 1px solid var(--border, #e6e6e6); border-radius: .65rem; background-color: #fbfbfb; }
.receipt-item-row--new-product { background-color: rgba(245, 158, 11, .12); border-color: rgba(245, 158, 11, .35); }
:global(body[data-app-theme='dark'] .receipt-item-row) { background-color: #1e2536; border-color: #334155; color: #f1f5f9; }
:global(body[data-app-theme='dark'] .receipt-item-row--new-product) { background-color: rgba(251, 191, 36, .11); border-color: rgba(251, 191, 36, .3); }
.receipt-product-combobox { position: relative; }
.receipt-product-combobox > i { color: var(--text-secondary); left: .7rem; pointer-events: none; position: absolute; top: 50%; transform: translateY(-50%); }
.receipt-product-combobox .form-control { padding-left: 2rem; }
.receipt-product-options { background: var(--surface); border: 1px solid var(--border-color); border-radius: .65rem; box-shadow: 0 .75rem 1.75rem rgba(15, 23, 42, .18); max-height: 15rem; min-width: 18rem; overflow-y: auto; padding: .3rem; position: absolute; right: 0; top: calc(100% + .35rem); width: 100%; z-index: 1070; }
.receipt-product-option { align-items: center; background: transparent; border: 0; border-radius: .45rem; color: var(--text-primary); cursor: pointer; display: flex; gap: .5rem; justify-content: space-between; padding: .55rem .65rem; text-align: right; width: 100%; }
.receipt-product-option + .receipt-product-option { margin-top: .15rem; }
.receipt-product-option:hover, .receipt-product-option:focus { background: var(--primary-light); color: var(--primary-dark); outline: 0; }
.receipt-product-option__title { font-weight: 600; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.receipt-product-option__meta { align-items: center; color: var(--text-secondary); display: flex; flex: 0 0 auto; font-size: .74rem; gap: .35rem; }
.receipt-product-option__meta span { background: var(--body-bg); border: 1px solid var(--border-color); border-radius: 999px; padding: .12rem .4rem; }
.receipt-product-option:hover .receipt-product-option__meta { color: inherit; }
.receipt-product-empty { align-items: center; color: var(--text-secondary); display: flex; font-size: .8rem; gap: .4rem; padding: .7rem .55rem; }
.receipt-product-empty i { color: var(--primary); font-size: 1.05rem; }
.receipt-new-master-data { align-items: center; color: var(--primary); display: flex; font-size: .76rem; gap: .3rem; margin-top: .35rem; }
.receipt-new-master-data--denied { color: var(--error, #dc3545); }
.receipt-new-product-message { margin-bottom: -.25rem; margin-top: .55rem; }
.receipt-item-primary-row, .receipt-item-secondary-row { display: grid; grid-template-columns: minmax(13rem, 1fr) minmax(6.5rem, .25fr) minmax(6.5rem, .25fr) minmax(5.5rem, .2fr) minmax(9rem, .38fr); gap: .65rem; }
.receipt-item-primary-row { align-items: start; }
.receipt-item-secondary-row { margin-top: .65rem; align-items: end; }
.receipt-item-description { grid-column: 1; }
.receipt-item-actions { grid-column: 5; align-self: end; justify-self: end; padding-bottom: .15rem; }
.receipt-remove-item { color: #dc3545 !important; border-color: rgba(220, 53, 69, .45) !important; }
.receipt-remove-item:hover:not(:disabled) { color: #fff !important; background-color: #dc3545 !important; border-color: #dc3545 !important; }
.receipt-code-field input, .receipt-unit-field input, .receipt-quantity-field input { text-align: center; }
.receipt-code-field input[readonly], .receipt-unit-field input[readonly] { background-color: rgba(0, 0, 0, .035); }
@media (max-width: 991px) {
  .receipt-item-primary-row, .receipt-item-secondary-row { grid-template-columns: minmax(0, 1fr) minmax(7rem, .35fr); }
  .receipt-item-description { grid-column: 1; }
  .receipt-item-actions { grid-column: 2; }
}
@media (max-width: 575px) {
  .goods-receipts-toolbar { gap: .4rem; }
  .goods-receipts-view-mode { padding-left: .25rem; }
  .goods-receipts-view-mode > span { display: none; }
  .goods-receipts-view-mode .form-control { min-width: 8.25rem; }
  .receipt-items-header { align-items: stretch; flex-direction: column; }
  .receipt-entry-actions { justify-content: space-between; }
  .receipt-entry-mode { display: flex; flex: 1 1 auto; }
  .receipt-entry-mode .btn { flex: 1 1 50%; min-width: 0; }
  .receipt-product-options { min-width: 100%; }
  .receipt-item-primary-row { grid-template-columns: 1fr; }
  .receipt-item-secondary-row { grid-template-columns: minmax(0, 1fr) auto; }
  .receipt-item-description { grid-column: 1; }
  .receipt-item-actions { grid-column: 2; }
}
</style>
