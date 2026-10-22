<script setup lang="ts">
import type { AjaxCallback, AjaxData, Api, ColumnsConfig, Config } from 'datatables.net'
import DataTablesCore from 'datatables.net-bs4'
import DataTable from 'datatables.net-vue3'
import { computed, nextTick, onBeforeUnmount, onMounted, reactive, ref, shallowRef } from 'vue'
import DatePicker from 'vue3-persian-datetime-picker'

import DataTableFilters from '@/components/filters/DataTableFilters.vue'
import GoodsIssueDestinationSelect from '@/components/selectors/GoodsIssueDestinationSelect.vue'
import PersonSelect, { type PersonSelectOption } from '@/components/selectors/PersonSelect.vue'
import ProductSelect from '@/components/selectors/ProductSelect.vue'
import { getSession } from '@/services/auth'
import type { GoodsIssueDestinationOption } from '@/services/goodsIssueDestinations'
import {
  createGoodsIssue,
  deleteGoodsIssue,
  exportGoodsIssueItems,
  getGoodsIssue,
  getGoodsIssueOptions,
  getGoodsIssues,
  type GoodsIssue,
  type GoodsIssueItemRow,
  type GoodsIssueListFilters,
  type GoodsIssueOptions,
  type SaveGoodsIssueRequest,
  updateGoodsIssue,
} from '@/services/goodsIssues'
import type { Product } from '@/services/products'
import { toast } from '@/services/toast'
import type {
  DataTableFilterDefinition,
  DataTableFilterValue,
  DataTableFilterValues,
  DateFilterValue,
  NumericFilterValue,
  TextFilterValue,
} from '@/types/dataTableFilters'
import { createRowNumberColumn } from '@/utils/dataTable'

defineOptions({ name: 'GoodsIssuesView' })
DataTable.use(DataTablesCore)

type TableMode = 'issues' | 'items'
type GoodsEntryMode = 'cards' | 'list'
interface FormItem {
  key: number
  productId: string | null
  productCode: string
  productTitle: string
  productUnitOfMeasureTitle: string
  quantity: number | null
  assetNumber: string
  destinationId: string | null
  destinationTitle: string
  exitReason: string
  description: string
  deliveringUnit: string
  returnAtLocal: string
}

const sessionPermissions = new Set(getSession()?.permissions ?? [])
const canManage = sessionPermissions.has('GoodsIssuesManage')
const canExport = sessionPermissions.has('GoodsIssuesExport')
const canManageMasterData = sessionPermissions.has('MasterDataManage')
const mode = ref<TableMode>('items')
const goodsEntryMode = ref<GoodsEntryMode>('list')
const table = shallowRef<{ dt: Api } | null>(null)
const options = ref<GoodsIssueOptions>({ people: [] })
const loading = ref(false)
const saving = ref(false)
const exporting = ref(false)
const modalOpen = ref(false)
const editingId = ref<string | null>(null)
const search = ref('')
const appliedSearch = ref('')
const appliedFilters = ref<DataTableFilterValues>({})
const productFilterId = ref<string | null>(null)
const productFilterText = ref('')
const destinationFilterText = ref('')
const issuerFilterText = ref('')
const formError = ref('')
let requestController: AbortController | null = null
let itemKey = 0
const listModeMinimumRows = 4

const form = reactive({
  issueNumber: '',
  issuedAtLocal: '',
  issuerId: '' as string,
  issuerText: '',
  items: [] as FormItem[],
})

const people = computed<PersonSelectOption[]>(() => options.value.people)
const filterDefinitions = computed<DataTableFilterDefinition[]>(() => [
  { key: 'issuedAt', label: 'تاریخ خروج', type: 'date-comparison' },
  { key: 'issueNumberFilter', label: 'کد خروج', type: 'text-comparison', placeholder: 'کد یا شماره خروج', maxlength: 100 },
  { key: 'productTitle', label: 'عنوان کالا', type: 'text-comparison', placeholder: 'جستجو و انتخاب کالا', maxlength: 256 },
  { key: 'productCode', label: 'کد کالا', type: 'text-comparison', placeholder: 'بخشی از کد کالا', maxlength: 100 },
  { key: 'quantity', label: 'تعداد', type: 'numeric-comparison', numericType: 'decimal', min: 0 },
  { key: 'assetNumber', label: 'شماره اموال', type: 'text-comparison', placeholder: 'شماره اموال', maxlength: 100 },
  {
    key: 'destinationId', label: 'مقصد', type: 'select', placeholder: 'جستجو و انتخاب مقصد',
    options: filterId(appliedFilters.value.destinationId) && destinationFilterText.value
      ? [{ value: filterId(appliedFilters.value.destinationId)!, label: destinationFilterText.value }]
      : [],
  },
  { key: 'deliveringUnit', label: 'واحد تحویل‌دهنده', type: 'text-comparison', placeholder: 'واحد تحویل‌دهنده', maxlength: 200 },
  {
    key: 'issuerId', label: 'خارج‌کننده', type: 'select', placeholder: 'همه خارج‌کنندگان',
    options: filterId(appliedFilters.value.issuerId) && issuerFilterText.value
      ? [{ value: filterId(appliedFilters.value.issuerId)!, label: issuerFilterText.value }]
      : [],
  },
])

const issueColumns: ColumnsConfig[] = [
  createRowNumberColumn(),
  { data: 'issueNumber', name: 'issueNumber', title: 'کد خروج', className: 'all' },
  { data: 'issuedAtUtc', name: 'issuedAtUtc', title: 'تاریخ خروج', className: 'all text-bold' },
  { data: 'issuerFullName', name: 'issuerFullName', title: 'خارج‌کننده', className: 'min-tablet-l' },
  { data: 'items', name: 'itemCount', title: 'تعداد اقلام', className: 'min-tablet-l text-bold', orderable: false },
]

const itemColumns: ColumnsConfig[] = [
  createRowNumberColumn(),
  { data: 'issuedAtUtc', name: 'issuedAtUtc', title: 'تاریخ خروج', className: 'all text-bold' },
  { data: 'issueNumber', name: 'issueNumber', title: 'کد خروج', className: 'all' },
  { data: 'productCode', name: 'productCode', title: 'کد کالا', className: 'min-tablet-l' },
  { data: 'productTitle', name: 'productTitle', title: 'عنوان کالا', className: 'all' },
  { data: 'quantity', name: 'quantity', title: 'تعداد', className: 'min-tablet-l text-bold' },
  { data: 'assetNumber', name: 'assetNumber', title: 'شماره اموال', className: 'min-tablet-l' },
  { data: 'destinationTitle', name: 'destinationTitle', title: 'مقصد', className: 'min-tablet-l' },
  { data: 'exitReason', name: 'exitReason', title: 'علت خروج', className: 'min-tablet-p text-wrap-cell', orderable: false },
  { data: 'description', name: 'description', title: 'توضیحات', className: 'min-tablet-p text-wrap-cell', orderable: false },
  { data: 'deliveringUnit', name: 'deliveringUnit', title: 'واحد تحویل‌دهنده', className: 'min-desktop' },
  { data: 'returnAtUtc', name: 'returnAtUtc', title: 'تاریخ عودت', className: 'min-desktop' },
  { data: 'issuerFullName', name: 'issuerFullName', title: 'خارج‌کننده', className: 'min-desktop' },
]

if (canManage) {
  issueColumns.push({ data: 'id', name: 'actions', title: 'عملیات', className: 'min-tablet-p text-center actions-cell', orderable: false, searchable: false })
  itemColumns.push({ data: 'goodsIssueId', name: 'actions', title: 'عملیات', className: 'min-tablet-p text-center actions-cell', orderable: false, searchable: false })
}

function nowLocal(): string {
  const date = new Date()
  date.setMinutes(date.getMinutes() - date.getTimezoneOffset())
  return date.toISOString().slice(0, 19)
}

function newItem(): FormItem {
  return {
    key: ++itemKey,
    productId: null,
    productCode: '',
    productTitle: '',
    productUnitOfMeasureTitle: '',
    quantity: null,
    assetNumber: '',
    destinationId: null,
    destinationTitle: '',
    exitReason: '',
    description: '',
    deliveringUnit: '',
    returnAtLocal: '',
  }
}

function resetForm(): void {
  editingId.value = null
  form.issueNumber = ''
  form.issuedAtLocal = nowLocal()
  form.issuerId = ''
  form.issuerText = ''
  form.items = [newItem()]
  goodsEntryMode.value = 'list'
  ensureListRows()
  formError.value = ''
}

async function loadTableRows(request: AjaxData, callback: AjaxCallback): Promise<void> {
  requestController?.abort()
  const controller = new AbortController()
  requestController = controller
  loading.value = true
  const start = request.start ?? 0
  const pageSize = request.length && request.length > 0 ? request.length : 10

  try {
    const result = await getGoodsIssues(
      mode.value,
      Math.floor(start / pageSize) + 1,
      pageSize,
      appliedSearch.value,
      controller.signal,
      currentFilters(),
    )
    callback({
      draw: request.draw,
      recordsTotal: result.totalCount,
      recordsFiltered: result.filteredCount,
      data: result.items,
    })
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') return
    toast.error(error instanceof Error ? error.message : 'دریافت اطلاعات انجام نشد.')
    callback({ draw: request.draw, recordsTotal: 0, recordsFiltered: 0, data: [] })
  } finally {
    if (requestController === controller) loading.value = false
  }
}

function createTableOptions(columns: ColumnsConfig[], emptyTable: string): Config {
  return {
    ajax: loadTableRows,
    columns,
    serverSide: true,
    processing: true,
    searching: false,
    ordering: false,
    pageLength: 10,
    lengthMenu: [5, 10, 25, 50],
    autoWidth: false,
    pagingType: 'full_numbers',
    responsive: {
      details: {
        type: 'inline',
        renderer: DataTablesCore.Responsive.renderer.listHiddenNodes(),
      },
    },
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
      loadingRecords: 'در حال دریافت تاریخچه خروج کالا...',
      processing: 'در حال پردازش...',
      zeroRecords: 'خروج کالایی پیدا نشد.',
      paginate: { first: '<<', last: '>>', next: '>', previous: '<' },
    },
  }
}

const issueTableOptions = createTableOptions(issueColumns, 'هنوز خروج کالایی ثبت نشده است.')
const itemTableOptions = createTableOptions(itemColumns, 'هنوز کالایی از انبار خارج نشده است.')

function reloadTable(resetPaging = true): void {
  table.value?.dt.ajax.reload(undefined, resetPaging)
}

async function loadOptions(): Promise<void> {
  try { options.value = await getGoodsIssueOptions() }
  catch (error) { toast.error(error instanceof Error ? error.message : 'دریافت مفروضات انجام نشد.') }
}

function setMode(value: TableMode): void {
  if (mode.value === value) return
  mode.value = value
}

function handleFilterSearch(value: string): void {
  search.value = value
  appliedSearch.value = value.trim()
  reloadTable(true)
}

function handleFilterApply(filters: DataTableFilterValues): void {
  appliedFilters.value = filters
  reloadTable(true)
}

function handleFilterClear(): void {
  search.value = ''
  appliedSearch.value = ''
  appliedFilters.value = {}
  productFilterId.value = null
  productFilterText.value = ''
  destinationFilterText.value = ''
  issuerFilterText.value = ''
  reloadTable(true)
}

function handleFilterRemove(filters: DataTableFilterValues): void {
  appliedFilters.value = filters
  if (!filters.productTitle) { productFilterId.value = null; productFilterText.value = '' }
  if (!filters.destinationId) destinationFilterText.value = ''
  if (!filters.issuerId) issuerFilterText.value = ''
  reloadTable(true)
}

function filterId(value: DataTableFilterValue | undefined): string | null {
  return typeof value === 'string' && value ? value : null
}

function textFilter(key: string): TextFilterValue | undefined {
  const value = appliedFilters.value[key]
  return value && typeof value === 'object' && 'operator' in value && 'value' in value
    ? value as TextFilterValue : undefined
}

function dateFilter(key: string): DateFilterValue | undefined {
  return textFilter(key) as DateFilterValue | undefined
}

function numericFilter(key: string): NumericFilterValue | undefined {
  const value = appliedFilters.value[key]
  return value && typeof value === 'object' && 'operator' in value && 'value' in value
    ? value as NumericFilterValue : undefined
}

function currentFilters(): GoodsIssueListFilters {
  return {
    issuedAt: dateFilter('issuedAt'),
    issueNumberFilter: textFilter('issueNumberFilter'),
    productTitle: textFilter('productTitle'),
    productCode: textFilter('productCode'),
    quantity: numericFilter('quantity'),
    assetNumber: textFilter('assetNumber'),
    destinationId: filterId(appliedFilters.value.destinationId) ?? undefined,
    deliveringUnit: textFilter('deliveringUnit'),
    issuerId: filterId(appliedFilters.value.issuerId) ?? undefined,
  }
}

function selectProductFilter(product: Product, setValue: (value: DataTableFilterValue) => void): void {
  productFilterId.value = product.id
  productFilterText.value = product.title
  setValue({ operator: 'eq', value: product.title })
}

function clearProductFilter(setValue: (value: DataTableFilterValue) => void): void {
  productFilterId.value = null
  productFilterText.value = ''
  setValue(null)
}

function selectDestinationFilter(
  destination: GoodsIssueDestinationOption,
  setValue: (value: DataTableFilterValue) => void,
): void {
  destinationFilterText.value = destination.title
  setValue(destination.id)
}

function selectIssuerFilter(person: PersonSelectOption, setValue: (value: DataTableFilterValue) => void): void {
  issuerFilterText.value = person.fullName
  setValue(person.id)
}

function openCreate(): void {
  resetForm()
  modalOpen.value = true
}

async function openEdit(row: GoodsIssue | GoodsIssueItemRow): Promise<void> {
  if (!canManage || loading.value) return
  try {
    const id = 'goodsIssueId' in row ? row.goodsIssueId : row.id
    const issue = await getGoodsIssue(id)
    editingId.value = id
    form.issueNumber = issue.issueNumber
    form.issuedAtLocal = toLocalDateTime(issue.issuedAtUtc)
    form.issuerId = issue.issuerId
    form.issuerText = issue.issuerFullName
    form.items = issue.items.map(item => ({
      ...newItem(),
      productId: item.productId,
      productCode: item.productCode,
      productTitle: item.productTitle ?? '',
      productUnitOfMeasureTitle: item.productUnitOfMeasureTitle ?? '',
      quantity: item.quantity,
      assetNumber: item.assetNumber ?? '',
      destinationId: item.destinationId,
      destinationTitle: item.destinationTitle,
      exitReason: item.exitReason,
      description: item.description ?? '',
      deliveringUnit: item.deliveringUnit,
      returnAtLocal: item.returnAtUtc ? toLocalDate(item.returnAtUtc) : '',
    }))
    ensureListRows()
    formError.value = ''
    modalOpen.value = true
  } catch (error) {
    toast.error(error instanceof Error ? error.message : 'دریافت سند انجام نشد.')
  }
}

function closeModal(): void {
  if (!saving.value) modalOpen.value = false
}

function selectProduct(item: FormItem, product: Product): void {
  inheritDestination(item)
  item.productId = product.id
  item.productCode = product.code
  item.productTitle = product.title
  item.productUnitOfMeasureTitle = product.unitOfMeasureTitle
  ensureListRows()
  focusQuantity(item)
}

function updateProductTitle(item: FormItem, value: string): void {
  inheritDestination(item)
  item.productTitle = value
  ensureListRows()
}

function updateProductCode(item: FormItem, value: string): void {
  inheritDestination(item)
  item.productCode = value
  ensureListRows()
}

function clearProduct(item: FormItem, field: 'title' | 'code'): void {
  item.productId = null
  item.productUnitOfMeasureTitle = ''
  if (field === 'title') item.productCode = ''
  else item.productTitle = ''
  ensureListRows()
}

function updateDestinationTitle(item: FormItem, value: string): void {
  item.destinationTitle = value
  ensureListRows()
}

function selectDestination(item: FormItem, destination: GoodsIssueDestinationOption): void {
  item.destinationId = destination.id
  item.destinationTitle = destination.title
  ensureListRows()
}

function clearDestination(item: FormItem): void {
  item.destinationId = null
  ensureListRows()
}

function itemHasContent(item: FormItem): boolean {
  return Boolean(
    item.productId || item.productTitle.trim() || item.productCode.trim() || item.quantity !== null ||
    item.assetNumber.trim() || item.destinationId || item.destinationTitle.trim() ||
    item.exitReason.trim() || item.description.trim() || item.deliveringUnit.trim() ||
    item.returnAtLocal,
  )
}

function ensureListRows(): void {
  if (goodsEntryMode.value !== 'list') return
  while (form.items.length < listModeMinimumRows) form.items.push(newItem())
  if (itemHasContent(form.items.at(-1)!)) form.items.push(newItem())
}

function setGoodsEntryMode(value: GoodsEntryMode): void {
  if (goodsEntryMode.value === value) return
  const populated = form.items.filter(itemHasContent)
  goodsEntryMode.value = value
  form.items = populated.length ? populated : [newItem()]
  ensureListRows()
}

function inheritDestination(item: FormItem): void {
  if (item.destinationId || item.destinationTitle.trim()) return
  const index = form.items.findIndex(candidate => candidate.key === item.key)
  if (index <= 0) return
  const previous = form.items[index - 1]
  if (!previous || !itemHasContent(previous)) return
  item.destinationId = previous.destinationId
  item.destinationTitle = previous.destinationTitle
}

function focusQuantity(item: FormItem): void {
  void nextTick(() => {
    const input = document.querySelector<HTMLInputElement>(`input[data-goods-issue-quantity="${item.key}"]`)
    input?.focus()
    input?.select()
  })
}

function advanceFromProductTitle(item: FormItem): void {
  inheritDestination(item)
  ensureListRows()
  if (item.productId) {
    focusQuantity(item)
    return
  }
  if (!item.productTitle.trim()) return
  void nextTick(() => {
    const holder = document.querySelector<HTMLElement>(`[data-goods-issue-code="${item.key}"]`)
    const input = holder?.querySelector<HTMLInputElement>('input')
    input?.focus()
    input?.select()
  })
}

function advanceFromProductCode(item: FormItem): void {
  inheritDestination(item)
  ensureListRows()
  focusQuantity(item)
}

function addItem(): void {
  const item = newItem()
  form.items.push(item)
  ensureListRows()
}
function removeItem(index: number): void {
  if (goodsEntryMode.value === 'cards' && form.items.length === 1) form.items[0] = newItem()
  else form.items.splice(index, 1)
  ensureListRows()
}

function validateForm(): string | null {
  if (!form.issueNumber.trim()) return 'کد یا شماره خروج را وارد کنید.'
  if (!form.issuedAtLocal) return 'تاریخ خروج را وارد کنید.'
  if (!form.issuerId && !form.issuerText.trim()) return 'خارج‌کننده را انتخاب یا وارد کنید.'
  if (!form.issuerId && !canManageMasterData) return 'برای افزودن خارج‌کننده جدید مجوز مدیریت مفروضات لازم است.'
  const items = form.items.filter(itemHasContent)
  if (!items.length) return 'حداقل یک ردیف کالا اضافه کنید.'
  for (const [index, item] of items.entries()) {
    const row = index + 1
    if (!item.productCode.trim()) return `کد کالا در ردیف ${row} الزامی است.`
    if (!item.quantity || item.quantity <= 0) return `تعداد کالا در ردیف ${row} باید بزرگ‌تر از صفر باشد.`
    if (!item.destinationTitle.trim()) return `مقصد در ردیف ${row} الزامی است.`
    if (!item.destinationId && !canManageMasterData) return `مقصد ردیف ${row} باید از فهرست انتخاب شود.`
    if (!item.exitReason.trim()) return `علت خروج در ردیف ${row} الزامی است.`
    if (!item.deliveringUnit.trim()) return `واحد تحویل‌دهنده در ردیف ${row} الزامی است.`
  }
  return null
}

async function save(): Promise<void> {
  const error = validateForm()
  if (error) { formError.value = error; return }
  saving.value = true
  formError.value = ''
  const payload: SaveGoodsIssueRequest = {
    issueNumber: form.issueNumber.trim(),
    issuedAtUtc: new Date(form.issuedAtLocal).toISOString(),
    issuerId: form.issuerId || null,
    issuerFullName: form.issuerId ? null : form.issuerText.trim(),
    items: form.items.filter(itemHasContent).map(item => ({
      productId: item.productId,
      productCode: item.productCode.trim(),
      quantity: item.quantity!,
      assetNumber: item.assetNumber.trim() || null,
      destinationId: item.destinationId,
      destinationTitle: item.destinationId ? null : item.destinationTitle.trim(),
      exitReason: item.exitReason.trim(),
      description: item.description.trim() || null,
      deliveringUnit: item.deliveringUnit.trim(),
      returnAtUtc: item.returnAtLocal ? new Date(`${item.returnAtLocal}T00:00:00`).toISOString() : null,
    })),
  }
  try {
    if (editingId.value) await updateGoodsIssue(editingId.value, payload)
    else await createGoodsIssue(payload)
    toast.success(editingId.value ? 'سند خروج ویرایش شد.' : 'خروج کالا ثبت شد.')
    modalOpen.value = false
    reloadTable(true)
    await loadOptions()
  } catch (error) {
    formError.value = error instanceof Error ? error.message : 'ثبت اطلاعات انجام نشد.'
  } finally { saving.value = false }
}

async function remove(row: GoodsIssue | GoodsIssueItemRow): Promise<void> {
  const id = 'goodsIssueId' in row ? row.goodsIssueId : row.id
  if (!window.confirm('سند خروج و تمام اقلام آن حذف شود؟')) return
  try {
    await deleteGoodsIssue(id)
    toast.success('سند خروج حذف شد.')
    reloadTable(false)
  } catch (error) { toast.error(error instanceof Error ? error.message : 'حذف انجام نشد.') }
}

async function exportItems(): Promise<void> {
  exporting.value = true
  try { await exportGoodsIssueItems(appliedSearch.value, currentFilters()) }
  catch (error) { toast.error(error instanceof Error ? error.message : 'خروجی دریافت نشد.') }
  finally { exporting.value = false }
}

function formatDate(value: string | null): string {
  if (!value) return '—'
  return new Intl.DateTimeFormat('fa-IR', { year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date(value))
}
function toLocalDateTime(value: string): string {
  const date = new Date(value); date.setMinutes(date.getMinutes() - date.getTimezoneOffset())
  return date.toISOString().slice(0, 19)
}
function toLocalDate(value: string): string {
  const date = new Date(value); date.setMinutes(date.getMinutes() - date.getTimezoneOffset())
  return date.toISOString().slice(0, 10)
}

onMounted(() => { void loadOptions() })
onBeforeUnmount(() => requestController?.abort())
</script>

<template>
  <div class="container-fluid users-page goods-issues-page inventory-data-page">
    <div class="page-titles">
      <ol class="breadcrumb">
        <li class="breadcrumb-item">
          <RouterLink to="/dashboard">داشبورد</RouterLink>
        </li>
        <li class="breadcrumb-item active">خروج کالا</li>
      </ol>
    </div>
    <div class="card">
      <div class="card-header goods-issues-header">
        <div>
          <h4 class="card-title mb-0">خروج کالا</h4>
          <p class="text-muted mb-0 mt-1">ثبت و پیگیری اسناد و اقلام خروج کالا</p>
        </div>

        <div class="goods-receipts-toolbar">
          <!-- <label class="goods-receipts-view-mode mb-0">
            <span>نوع نمایش</span>
            <select v-model="tableMode" class="form-control form-control-sm" aria-label="نوع نمایش جدول">
              <option value="items">بر اساس کالا</option>
              <option value="receipts">بر اساس ورودی</option>
            </select>
          </label> -->

          <div class="btn-group ml-1" role="group">
            <button class="btn btn-sm" :class="mode === 'items' ? 'btn-primary' : 'btn-outline-primary'"
              @click="setMode('items')">
              اقلام خروج
            </button>
            <button class="btn btn-sm" :class="mode === 'issues' ? 'btn-primary' : 'btn-outline-primary'"
              @click="setMode('issues')">
              اسناد خروج
            </button>
          </div>

          <!-- <input v-if="canImportGoodsReceipts"
                 ref="importInput"
                 type="file"
                 accept=".xlsx,.xlsm"
                 hidden
                 @change="selectImportFile" /> -->
          <div class="btn-group goods-receipts-column-settings goods-receipts-action-group ml-1" role="group"
            aria-label="ابزارهای لیست" @click.stop>
            <!-- <button v-if="canImportGoodsReceipts"
                    class="btn btn-outline-primary btn-sm goods-receipts-toolbar-icon goods-receipts-import-button"
                    type="button"
                    title="ایمپورت ورودی کالا"
                    aria-label="ایمپورت ورودی کالا"
                    :disabled="importing"
                    @click="importInput?.click()">
              <span v-if="importing" class="spinner-border spinner-border-sm" aria-hidden="true"></span>
              <i v-else class="las la-file-import" aria-hidden="true"></i>
            </button> -->
            <button v-if="exportItems"
              class="btn btn-outline-success btn-sm goods-receipts-toolbar-icon goods-receipts-desktop-export"
              type="button" title="گزارش خروجی کالا" aria-label="گزارش ورودی کالا" :disabled="exporting"
              @click="exportItems">
              <span v-if="exporting" class="spinner-border spinner-border-sm" aria-hidden="true"></span>
              <i v-else class="las la-file-excel" aria-hidden="true"></i>
            </button>
            <!-- <button class="btn btn-outline-primary btn-sm goods-receipts-toolbar-icon goods-receipts-column-button"
                    type="button"
                    title="تنظیم ستون‌ها"
                    aria-label="تنظیم ستون‌ها"
                    :aria-expanded="columnSettingsOpen"
                    @click="toggleColumnSettings">
              <i class="las la-columns" aria-hidden="true"></i>
            </button> -->
            <!-- <div v-if="columnSettingsOpen" class="goods-receipts-column-menu">
              <div class="goods-receipts-column-menu__title">ستون‌های قابل نمایش</div>
              <label v-for="column in activeColumnSettings" :key="column.key" class="goods-receipts-column-option">
                <input type="checkbox"
                       :checked="columnIsVisible(column.key)"
                       @change="toggleColumnVisibility(column)" />
                <span>{{ column.label }}</span>
              </label>
            </div> -->
          </div>
          <button class="btn btn-primary btn-sm mobile-icon-button goods-receipts-desktop-add" type="button"
            title="ثبت خروج کالا" @click="openCreate">
            <i class="las la-plus ml-1"></i>
            <span class="mobile-button-label">ثبت خروج کالا</span>
          </button>
        </div>


        <!-- <div class="goods-issues-actions"> -->
          <!--<button v-if="canExport" class="btn btn-outline-primary"
          type="button" :disabled="exporting" @click="exportItems">
    <i class="las la-file-excel"></i> خروجی
  </button>
  <button v-if="canManage" class="btn btn-primary" type="button"
          @click="openCreate">
    <i class="las la-plus"></i> ثبت خروج
  </button>-->

          <!-- <button class="btn btn-primary btn-sm
                        mobile-icon-button
                        goods-receipts-desktop-add" type="button" title="ثبت خروجی" @click="openCreate">
            <i class="las la-plus ml-1"></i>
            <span class="mobile-button-label">ثبت خروج</span>
          </button> -->
        <!-- </div> -->
      </div>
      <div class="card-body">
        <DataTableFilters v-model="appliedFilters"
                          :definitions="filterDefinitions"
                          :search="search"
                          :loading="loading"
                          search-placeholder="جستجو در کد خروج، کالا، مقصد و خارج‌کننده..."
                          @update:search="handleFilterSearch"
                          @apply="handleFilterApply"
                          @clear="handleFilterClear"
                          @remove="handleFilterRemove">
          <template #filter-productTitle="{ setValue, error }">
            <ProductSelect :model-value="productFilterId"
                           :text="productFilterText"
                           label="عنوان کالا"
                           placeholder="جستجو و انتخاب کالا"
                           size="sm"
                           :error="error"
                           @update:model-value="productFilterId = $event"
                           @update:text="productFilterText = $event"
                           @select="selectProductFilter($event, setValue)"
                           @clear="clearProductFilter(setValue)" />
          </template>
          <template #filter-destinationId="{ value, setValue, error }">
            <GoodsIssueDestinationSelect :model-value="filterId(value)"
                                         :text="destinationFilterText"
                                         label="مقصد"
                                         placeholder="جستجو و انتخاب مقصد"
                                         size="sm"
                                         :error="error"
                                         @update:model-value="setValue($event)"
                                         @update:text="destinationFilterText = $event"
                                         @select="selectDestinationFilter($event, setValue)"
                                         @clear="setValue(null)" />
          </template>
          <template #filter-issuerId="{ value, setValue, error }">
            <PersonSelect :model-value="filterId(value)"
                          :text="issuerFilterText"
                          :options="people"
                          label="خارج‌کننده"
                          placeholder="جستجو و انتخاب خارج‌کننده"
                          size="sm"
                          :error="error"
                          @update:model-value="setValue($event)"
                          @update:text="issuerFilterText = $event"
                          @select="selectIssuerFilter($event, setValue)" />
          </template>
        </DataTableFilters>
        <div class="users-table-container goods-issues-table-wrap inventory-table-container data-table-scroll-container">
          <DataTable :key="mode"
                     ref="table"
                     :options="mode === 'items' ? itemTableOptions : issueTableOptions"
                     class="display responsive nowrap table-striped table table-sm users-table goods-issues-table inventory-data-table">
            <template #column-issuedAtUtc="{ cellData }">
              <strong class="text-black">{{ formatDate(cellData) }}</strong>
            </template>
            <template #column-issueNumber="{ cellData }">
              <span dir="ltr">{{ cellData }}</span>
            </template>
            <template #column-productCode="{ cellData }">
              <span dir="ltr">{{ cellData }}</span>
            </template>
            <template #column-productTitle="{ cellData }">
              <strong class="text-black">{{ cellData || '—' }}</strong>
            </template>
            <template #column-quantity="{ cellData }">
              <span class="text-bold">{{ cellData }}</span>
            </template>
            <template #column-assetNumber="{ cellData }">
              <span dir="ltr">{{ cellData || '—' }}</span>
            </template>
            <template #column-exitReason="{ cellData }">
              <span class="text-wrap-cell" :title="cellData || ''">{{ cellData || '—' }}</span>
            </template>
            <template #column-description="{ cellData }">
              <span class="text-wrap-cell" :title="cellData || ''">{{ cellData || '—' }}</span>
            </template>
            <template #column-returnAtUtc="{ cellData }">
              {{ formatDate(cellData) }}
            </template>
            <template #column-itemCount="{ cellData }">
              <span class="badge light badge-info">{{ (cellData as GoodsIssue['items']).length }} کالا</span>
            </template>
            <template v-if="canManage" #column-actions="{ rowData }">
              <div class="d-flex justify-content-center">
                <button class="btn users-table__role-action"
                        type="button"
                        title="ویرایش سند"
                        aria-label="ویرایش سند"
                        :disabled="loading"
                        @click="openEdit(rowData)">
                  <i class="las la-edit" aria-hidden="true"></i>
                </button>
                <button class="btn users-table__role-action text-danger"
                        type="button"
                        title="حذف سند"
                        aria-label="حذف سند"
                        :disabled="loading"
                        @click="remove(rowData)">
                  <i class="las la-trash" aria-hidden="true"></i>
                </button>
              </div>
            </template>
            <thead>
              <tr v-if="mode === 'items'">
                <th>ردیف</th>
                <th>تاریخ خروج</th>
                <th>کد خروج</th>
                <th>کد کالا</th>
                <th>عنوان کالا</th>
                <th>تعداد</th>
                <th>شماره اموال</th>
                <th>مقصد</th>
                <th>علت خروج</th>
                <th>توضیحات</th>
                <th>واحد تحویل‌دهنده</th>
                <th>تاریخ عودت</th>
                <th>خارج‌کننده</th>
                <th v-if="canManage">عملیات</th>
              </tr>
              <tr v-else>
                <th>ردیف</th>
                <th>کد خروج</th>
                <th>تاریخ خروج</th>
                <th>خارج‌کننده</th>
                <th>تعداد اقلام</th>
                <th v-if="canManage">عملیات</th>
              </tr>
            </thead>
          </DataTable>
        </div>
      </div>
    </div>
  </div>

  <Teleport to="body">
    <template v-if="modalOpen">
      <div class="modal goods-issue-modal show d-block" tabindex="-1" role="dialog" @click.self="closeModal">
        <div class="modal-dialog modal-xl modal-dialog-centered modal-dialog-scrollable">
          <form class="modal-content" @submit.prevent="save">
            <div class="modal-header">
              <h5 class="modal-title">{{ editingId ? 'ویرایش خروج کالا' : 'ثبت خروج کالا' }}</h5>
              <button type="button" class="close" :disabled="saving" @click="closeModal">
                <span>&times;</span>
              </button>
            </div>
            <div class="modal-body">
              <div v-if="formError" class="alert alert-danger py-2">{{ formError }}</div>
              <div class="row">
                <div class="col-md-4 form-group">
                  <label>
                    کد یا شماره خروج
                    <span class="text-danger">*</span>
                  </label>
                  <input v-model="form.issueNumber" class="form-control" maxlength="100" />
                </div>
                <div class="col-md-4 form-group">
                  <label>تاریخ خروج <span class="text-danger">*</span></label>
                  <DatePicker v-model="form.issuedAtLocal" type="date" format="YYYY-MM-DDTHH:mm:ss"
                    display-format="jYYYY/jMM/jDD HH:mm" :editable="true" :clearable="true" color="#2563eb" />
                </div>
                <div class="col-md-4 form-group">
                  <PersonSelect :model-value="form.issuerId || null" :text="form.issuerText" :options="people"
                    label="خارج‌کننده" placeholder="انتخاب یا ورود خارج‌کننده" :allow-create="canManageMasterData"
                    required @update:model-value="form.issuerId = $event ?? ''"
                    @update:text="form.issuerText = $event" />
                </div>
              </div>
              <div class="goods-issue-entry-header mb-2">
                <h6 class="mb-0">اقلام خروج</h6>
                <div class="goods-issue-entry-actions">
                  <div class="btn-group btn-group-sm goods-issue-entry-mode" role="group" aria-label="روش ثبت کالا">
                    <button class="btn" :class="goodsEntryMode === 'cards' ? 'btn-primary' : 'btn-outline-primary'"
                      type="button" :disabled="saving" title="ثبت کارتی" @click="setGoodsEntryMode('cards')"><i
                        class="las la-stream"></i></button>
                    <button class="btn" :class="goodsEntryMode === 'list' ? 'btn-primary' : 'btn-outline-primary'"
                      type="button" :disabled="saving" title="ثبت جدولی" @click="setGoodsEntryMode('list')"><i
                        class="las la-table"></i></button>
                  </div>
                  <button class="btn btn-sm btn-outline-primary" type="button" :disabled="saving" @click="addItem"><i
                      class="las la-plus"></i> افزودن ردیف</button>
                </div>
              </div>

              <div v-if="goodsEntryMode === 'list'" class="goods-issue-list-entry">
                <div class="goods-issue-list-table-scroll">
                  <table class="goods-issue-list-table table-striped" aria-label="ثبت جدولی اقلام خروج">
                    <thead>
                      <tr>
                        <th class="goods-issue-list-row-number">#</th>
                        <th class="goods-issue-list-product-cell">عنوان کالا</th>
                        <th class="goods-issue-list-code-cell">
                          کد کالا
                          <span class="text-danger">*</span>
                        </th>
                        <th class="goods-issue-list-product-unit-cell">واحد کالا</th>
                        <th class="goods-issue-list-quantity-cell">
                          تعداد
                          <span class="text-danger">*</span>
                        </th>
                        <th class="goods-issue-list-asset-cell">شماره اموال</th>
                        <th class="goods-issue-list-destination-cell">
                          مقصد
                          <span class="text-danger">*</span>
                        </th>
                        <th class="goods-issue-list-reason-cell">
                          علت خروج
                          <span class="text-danger">*</span>
                        </th>
                        <th class="goods-issue-list-description-cell">توضیحات</th>
                        <th class="goods-issue-list-delivering-unit-cell">
                          واحد تحویل‌دهنده
                          <span class="text-danger">*</span>
                        </th>
                        <th class="goods-issue-list-return-cell">تاریخ عودت</th>
                        <th class="goods-issue-list-action-column"></th>
                      </tr>
                    </thead>
                    <tbody>
                      <tr v-for="(item, index) in form.items" :key="item.key"
                        :class="{ 'goods-issue-list-row--empty': !itemHasContent(item) }">
                        <td class="goods-issue-list-row-number">{{ index + 1 }}</td>
                        <td class="goods-issue-list-product-cell">
                          <ProductSelect :model-value="item.productId" :text="item.productTitle" size="sm"
                            search-by="title" placeholder="" aria-label="عنوان کالا" :disabled="saving"
                            @update:model-value="item.productId = $event"
                            @update:text="updateProductTitle(item, $event)" @select="selectProduct(item, $event)"
                            @clear="clearProduct(item, 'title')" @enter="advanceFromProductTitle(item)" />
                        </td>
                        <td class="goods-issue-list-code-cell">
                          <ProductSelect :model-value="item.productId" :text="item.productCode" size="sm"
                            search-by="code" placeholder="" aria-label="کد کالا" :disabled="saving"
                            :data-goods-issue-code="item.key" @update:model-value="item.productId = $event"
                            @update:text="updateProductCode(item, $event)" @select="selectProduct(item, $event)"
                            @clear="clearProduct(item, 'code')" @enter="advanceFromProductCode(item)" />
                        </td>
                        <td class="goods-issue-list-product-unit-cell">
                          <input :value="item.productUnitOfMeasureTitle" class="form-control form-control-sm"
                            aria-label="واحد کالا" readonly />
                        </td>
                        <td class="goods-issue-list-quantity-cell">
                          <input v-model.number="item.quantity" class="form-control form-control-sm" type="number"
                            min="0" step="any" inputmode="decimal" aria-label="تعداد"
                            :data-goods-issue-quantity="item.key" :disabled="saving" @input="ensureListRows" />
                        </td>
                        <td class="goods-issue-list-asset-cell">
                          <input v-model="item.assetNumber" class="form-control form-control-sm" maxlength="100"
                            :disabled="saving" @input="ensureListRows" />
                        </td>
                        <td class="goods-issue-list-destination-cell">
                          <GoodsIssueDestinationSelect :model-value="item.destinationId" :text="item.destinationTitle"
                            size="sm" placeholder="" aria-label="مقصد" :allow-create="canManageMasterData"
                            :disabled="saving" @update:model-value="item.destinationId = $event"
                            @update:text="updateDestinationTitle(item, $event)"
                            @select="selectDestination(item, $event)" @clear="clearDestination(item)" />
                        </td>
                        <td class="goods-issue-list-reason-cell">
                          <textarea v-model="item.exitReason"
                            class="form-control form-control-sm goods-issue-list-textarea" rows="1" maxlength="1000"
                            :disabled="saving" @input="ensureListRows">
                          </textarea>
                        </td>
                        <td class="goods-issue-list-description-cell">
                          <textarea v-model="item.description"
                            class="form-control form-control-sm goods-issue-list-textarea" rows="1" maxlength="1000"
                            :disabled="saving" @input="ensureListRows"></textarea>
                        </td>
                        <td class="goods-issue-list-delivering-unit-cell">
                          <input v-model="item.deliveringUnit" class="form-control form-control-sm" maxlength="200"
                            :disabled="saving" @input="ensureListRows" />
                        </td>
                        <td class="goods-issue-list-return-cell">
                          <DatePicker v-if="item.productCode.trim()" v-model="item.returnAtLocal" type="date"
                            format="YYYY-MM-DD" display-format="jYYYY/jMM/jDD" locale="fa" placeholder="انتخاب تاریخ"
                            :editable="true" :clearable="true" :auto-submit="true" :disabled="saving" color="#2563eb" />
                        </td>
                        <td class="goods-issue-list-action-column"><button
                            class="master-data-row-action goods-issue-remove-item" type="button" title="پاک‌کردن ردیف"
                            :disabled="saving" @click="removeItem(index)"><i class="las la-trash"></i></button></td>
                      </tr>
                    </tbody>
                  </table>
                </div>
              </div>

              <div v-else class="goods-issue-items">
                <div v-for="(item, index) in form.items" :key="item.key" class="goods-issue-item">
                  <div class="goods-issue-item__head"><strong>ردیف {{ index + 1 }}</strong><button
                      class="btn btn-sm btn-link text-danger" type="button" @click="removeItem(index)"><i
                        class="las la-trash"></i></button></div>
                  <div class="goods-issue-item__grid">
                    <div class="form-group"><label>کد کالا <span class="text-danger">*</span></label>
                      <ProductSelect :model-value="item.productId" :text="item.productCode" search-by="code"
                        placeholder="ورود یا انتخاب کد" @update:model-value="item.productId = $event"
                        @update:text="updateProductCode(item, $event)" @select="selectProduct(item, $event)"
                        @clear="clearProduct(item, 'code')" @enter="advanceFromProductCode(item)" />
                    </div>
                    <div class="form-group"><label>تعداد <span class="text-danger">*</span></label><input
                        v-model.number="item.quantity" class="form-control" type="number" min="0" step="any"
                        inputmode="decimal" :data-goods-issue-quantity="item.key" /></div>
                    <div class="form-group"><label>شماره اموال</label><input v-model="item.assetNumber"
                        class="form-control" maxlength="100" /></div>
                    <div class="form-group">
                      <GoodsIssueDestinationSelect :model-value="item.destinationId" :text="item.destinationTitle"
                        label="مقصد" placeholder="تایپ یا انتخاب مقصد" required :allow-create="canManageMasterData"
                        :disabled="saving" @update:model-value="item.destinationId = $event"
                        @update:text="updateDestinationTitle(item, $event)" @select="selectDestination(item, $event)"
                        @clear="clearDestination(item)" /><small v-if="item.destinationTitle && !item.destinationId"
                        :class="canManageMasterData ? 'text-primary' : 'text-danger'">
                        {{ canManageMasterData ? 'مقصد جدید هنگام ثبت اضافه می‌شود.' : 'مقصد را از فهرست انتخاب کنید.' }}</small>
                    </div>
                    <div class="form-group"><label>واحد تحویل‌دهنده <span class="text-danger">*</span></label><input
                        v-model="item.deliveringUnit" class="form-control" maxlength="200" /></div>
                    <div class="form-group"><label>تاریخ عودت</label>
                      <DatePicker v-model="item.returnAtLocal" type="date" format="YYYY-MM-DD"
                        display-format="jYYYY/jMM/jDD" :editable="true" :clearable="true" color="#2563eb" />
                    </div>
                    <div class="form-group goods-issue-item__wide"><label>علت خروج <span
                          class="text-danger">*</span></label><textarea v-model="item.exitReason" class="form-control"
                        rows="2" maxlength="1000"></textarea></div>
                    <div class="form-group goods-issue-item__wide"><label>توضیحات</label><textarea
                        v-model="item.description" class="form-control" rows="2" maxlength="1000"></textarea></div>
                  </div>
                </div>
              </div>
            </div>
            <div class="modal-footer"><button class="btn btn-light" type="button" :disabled="saving"
                @click="closeModal">انصراف</button><button class="btn btn-primary" type="submit"
                :disabled="saving"><span v-if="saving" class="spinner-border spinner-border-sm ml-1"></span>{{ saving ?
                  'در حال ثبت...' : 'ثبت اطلاعات' }}</button></div>
          </form>
        </div>
      </div>
      <div class="modal-backdrop show"></div>
    </template>
  </Teleport>
</template>

<style scoped>
.goods-issues-header,
.goods-issues-actions,
.goods-issues-pagination,
.goods-issue-item__head,
.goods-issue-entry-header,
.goods-issue-entry-actions {
  align-items: center;
  display: flex;
  gap: .65rem;
  justify-content: space-between
}

.goods-issue-modal .modal-dialog {
  margin: 1.5rem auto;
  max-height: 85vh;
  max-width: 1450px;
  width: calc(100vw - 3rem);
}

.goods-issues-actions,
.goods-issue-entry-actions {
  justify-content: flex-end
}

.goods-issues-table {
  min-width: 82rem
}

.goods-issue-items {
  display: grid;
  gap: .75rem
}

.goods-issue-item {
  background: var(--body-bg);
  border: 1px solid var(--border-color);
  border-radius: .65rem;
  padding: .75rem
}

.goods-issue-item__grid {
  display: grid;
  gap: .65rem;
  grid-template-columns: repeat(5, minmax(0, 1fr))
}

.goods-issue-item__wide {
  grid-column: span 2
}

.goods-issue-item :deep(.product-select__menu) {
  min-width: 18rem
}

.goods-issue-item .form-group {
  margin-bottom: 0
}

.goods-issue-entry-header {
  gap: 1rem
}

.goods-issue-entry-actions {
  flex-wrap: wrap;
  gap: .55rem
}

.goods-issue-entry-mode .btn {
  align-items: center;
  display: inline-flex;
  justify-content: center
}

.goods-issue-list-entry {
  background: var(--surface);
  border: 1px solid var(--border-color);
  border-radius: .65rem;
  margin-bottom: .75rem
}

.goods-issue-list-table-scroll {
  overflow-x: auto;
  overflow-y: visible;
  width: 100%
}

.goods-issue-list-table {
  --goods-issue-row-column-width: 2rem;
  --goods-issue-product-column-width: 14rem;
  --goods-issue-code-column-width: 100px;
  --goods-issue-product-unit-column-width: 70px;
  --goods-issue-quantity-column-width: 70px;
  --goods-issue-asset-column-width: 100px;
  --goods-issue-destination-column-width: 80px;
  --goods-issue-reason-column-width: 11rem;
  --goods-issue-description-column-width: 140px;
  --goods-issue-delivering-unit-column-width: 100px;
  --goods-issue-return-column-width: 120px;
  --goods-issue-action-column-width: var(--goods-issue-row-column-width);
  border-collapse: separate;
  border-spacing: 0;
  font-size: .74rem;
  table-layout: fixed;
  min-width: calc(var(--goods-issue-row-column-width) + var(--goods-issue-product-column-width) + var(--goods-issue-code-column-width) + var(--goods-issue-product-unit-column-width) + var(--goods-issue-quantity-column-width) + var(--goods-issue-asset-column-width) + var(--goods-issue-destination-column-width) + var(--goods-issue-reason-column-width) + var(--goods-issue-description-column-width) + var(--goods-issue-delivering-unit-column-width) + var(--goods-issue-return-column-width) + var(--goods-issue-action-column-width));
  width: 100%
}

.goods-issue-list-table th {
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
  z-index: 4
}

.goods-issue-list-table th:last-child {
  border-left: 0
}

.goods-issue-list-table td {
  border-bottom: 1px solid var(--border-color);
  border-left: 1px solid var(--border-color);
  padding: 0;
  position: relative;
  vertical-align: top
}

.goods-issue-list-table td:last-child {
  border-left: 0
}

.goods-issue-list-table tbody tr:last-child td {
  border-bottom: 0
}

.goods-issue-list-table tbody .goods-issue-list-product-cell,
.goods-issue-list-table tbody .goods-issue-list-product-unit-cell,
.goods-issue-list-table tbody .goods-issue-list-quantity-cell,
.goods-issue-list-table tbody .goods-issue-list-asset-cell,
.goods-issue-list-table tbody .goods-issue-list-destination-cell,
.goods-issue-list-table tbody .goods-issue-list-delivering-unit-cell,
.goods-issue-list-table tbody .goods-issue-list-return-cell {
  height: 1px
}

.goods-issue-list-table td>.form-control,
.goods-issue-list-table td>.form-control:disabled,
.goods-issue-list-table td>.form-control[readonly] {
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
  width: 100%
}

.goods-issue-list-table td>.form-control:focus {
  background-color: transparent !important;
  border-color: var(--primary);
  box-shadow: 0 0 0 .12rem rgba(37, 99, 235, .12)
}

.goods-issue-list-table .form-control.is-invalid {
  border-color: #dc3545;
  box-shadow: inset 0 0 0 1px rgba(220, 53, 69, .18)
}

.goods-issue-list-table tbody tr:has(.product-select--open),
.goods-issue-list-table tbody tr:has(.destination-select--open) {
  position: relative;
  z-index: 100068
}

.goods-issue-list-table td:has(.product-select--open),
.goods-issue-list-table td:has(.destination-select--open) {
  z-index: 100069
}

.goods-issue-list-table tbody tr:nth-last-child(-n + 3) :deep(.product-select__menu),
.goods-issue-list-table tbody tr:nth-last-child(-n + 3) :deep(.destination-select__menu) {
  bottom: calc(100% + .35rem);
  top: auto
}

.goods-issue-list-table .goods-issue-list-product-cell {
  width: var(--goods-issue-product-column-width)
}

.goods-issue-list-table .goods-issue-list-code-cell {
  width: var(--goods-issue-code-column-width)
}

.goods-issue-list-table .goods-issue-list-product-unit-cell {
  width: var(--goods-issue-product-unit-column-width)
}

.goods-issue-list-table .goods-issue-list-quantity-cell {
  width: var(--goods-issue-quantity-column-width)
}

.goods-issue-list-table .goods-issue-list-asset-cell {
  width: var(--goods-issue-asset-column-width)
}

.goods-issue-list-table .goods-issue-list-destination-cell {
  width: var(--goods-issue-destination-column-width)
}

.goods-issue-list-table .goods-issue-list-reason-cell {
  width: var(--goods-issue-reason-column-width)
}

.goods-issue-list-table .goods-issue-list-description-cell {
  width: var(--goods-issue-description-column-width)
}

.goods-issue-list-table .goods-issue-list-delivering-unit-cell {
  width: var(--goods-issue-delivering-unit-column-width)
}

.goods-issue-list-table .goods-issue-list-return-cell {
  width: var(--goods-issue-return-column-width)
}

.goods-issue-list-table .goods-issue-list-product-cell :deep(.product-select),
.goods-issue-list-table .goods-issue-list-code-cell :deep(.product-select) {
  --product-select-input-background: transparent;
  --product-select-input-border-color: transparent;
  --product-select-input-border-width: 0;
  --product-select-input-radius: 0;
  --product-select-menu-width: var(--goods-issue-product-column-width)
}

.goods-issue-list-table .goods-issue-list-product-cell :deep(.product-select__control .form-control),
.goods-issue-list-table .goods-issue-list-code-cell :deep(.product-select__control .form-control) {
  font-size: .68rem
}

.goods-issue-list-table .goods-issue-list-product-cell :deep(.product-select .form-control:focus),
.goods-issue-list-table .goods-issue-list-code-cell :deep(.product-select .form-control:focus) {
  background-color: transparent
}

.goods-issue-list-table .goods-issue-list-destination-cell :deep(.destination-select) {
  --destination-select-input-background: transparent;
  --destination-select-input-border-color: transparent;
  --destination-select-input-border-width: 0;
  --destination-select-input-radius: 0;
  --destination-select-menu-min-width: var(--goods-issue-product-column-width);
  --destination-select-menu-width: var(--goods-issue-product-column-width)
}

.goods-issue-list-table .goods-issue-list-destination-cell :deep(.destination-select__control .form-control) {
  font-size: .68rem
}

.goods-issue-list-table .goods-issue-list-destination-cell :deep(.destination-select .form-control:focus) {
  background-color: transparent
}

.goods-issue-list-table .goods-issue-list-return-cell :deep(.vpd-main) {
  display: block;
  height: 100%;
  width: 100%
}

.goods-issue-list-table .goods-issue-list-return-cell :deep(.vpd-input-group) {
  height: 100%;
  min-height: 1.85rem;
  width: 100%
}

.goods-issue-list-table .goods-issue-list-return-cell :deep(.vpd-input-group input) {
  background-color: transparent !important;
  border-color: transparent;
  border-radius: 0;
  box-sizing: border-box;
  font-size: .68rem;
  height: 100%;
  line-height: 1.35;
  margin: 0;
  min-height: 1.85rem;
  min-width: 0;
  padding: .25rem .35rem;
  width: 100%
}

.goods-issue-list-table .goods-issue-list-return-cell :deep(.vpd-input-group input:focus) {
  border-color: var(--primary);
  box-shadow: 0 0 0 .12rem rgba(37, 99, 235, .12);
  outline: 0
}

.goods-issue-list-table .goods-issue-list-return-cell :deep(.vpd-input-group label) {
  align-items: center;
  display: flex;
  flex: 0 0 1.85rem;
  justify-content: center;
  margin: 0;
  min-height: 1.85rem;
  padding: 0
}

.goods-issue-list-row-number {
  color: var(--text-secondary);
  text-align: center !important;
  width: var(--goods-issue-row-column-width)
}

.goods-issue-list-action-column {
  background: var(--surface);
  text-align: center !important;
  width: var(--goods-issue-action-column-width)
}

.goods-issue-list-action-column .master-data-row-action {
  margin-inline: auto
}

.goods-issue-list-table td>.goods-issue-list-textarea {
  box-sizing: border-box;
  height: auto;
  line-height: 1.35;
  min-height: 1.85rem !important;
  overflow: hidden;
  resize: none;
  white-space: pre-wrap
}

.goods-issue-list-row--empty {
  opacity: .68
}

.goods-issue-list-row--empty:focus-within {
  opacity: 1
}

.goods-issue-remove-item {
  border-color: rgba(220, 53, 69, .45) !important;
  color: #dc3545 !important
}

.goods-issue-remove-item:hover:not(:disabled) {
  background-color: #dc3545 !important;
  border-color: #dc3545 !important;
  color: #fff !important
}

.goods-issue-list-code-cell input,
.goods-issue-list-quantity-cell input {
  text-align: center
}

@media(max-width:991px) {
  .goods-issue-item__grid {
    grid-template-columns: repeat(2, minmax(0, 1fr))
  }
}

@media(max-width:575px) {

  .goods-issues-header,
  .goods-issue-entry-header {
    align-items: stretch;
    flex-direction: column
  }

  .goods-issue-item__grid {
    grid-template-columns: 1fr
  }

  .goods-issue-item__wide {
    grid-column: auto
  }

  .goods-issues-pagination {
    align-items: stretch;
    flex-direction: column;
    text-align: center
  }
}
</style>
