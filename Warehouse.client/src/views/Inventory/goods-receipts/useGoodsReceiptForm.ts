import { reactive, ref } from 'vue'

import { useEntryRows } from '@/composables/useEntryRows'
import { useFieldErrors } from '@/composables/useFieldErrors'
import { usePermissions } from '@/composables/usePermissions'
import { isAbortError, useRequestChannel } from '@/composables/useRequestChannel'
import { useUnauthorizedRedirect } from '@/composables/useUnauthorizedRedirect'
import {
  createGoodsReceipt,
  deleteGoodsReceipt,
  getGoodsReceipt,
  GoodsReceiptsApiError,
  updateGoodsReceipt,
  type CreateGoodsReceiptRequest,
  type GoodsReceipt,
  type GoodsReceiptItemRow,
  type InvoiceStatus,
} from '@/services/goodsReceipts'
import type { Product } from '@/services/products'
import { toast } from '@/services/toast'
import { localDateTimeToIso, nowLocalDateTime, toLocalDateTime } from '@/utils/dateTime'
import { focusEntryField } from '@/utils/entryFocus'

export interface FormItem {
  key: number
  productText: string
  productId: string | null
  productCode: string
  unitOfMeasureId: string | null
  unitOfMeasureTitle: string
  warehouseId: string
  invoiceStatus: InvoiceStatus | null
  invoiceNumber: string
  supplierId: string
  supplierText: string
  receiptNumber: string
  issueNumber: string
  quantity: number | null
  requestNumber: string
  description: string
}

export interface ConfirmOptions {
  title: string
  text?: string
  icon?: 'warning'
  confirmButtonText?: string
  cancelButtonText?: string
  confirmButtonColor?: string
}

interface Dependencies {
  confirm: (options: ConfirmOptions) => Promise<boolean>
  onSaved: (reason: 'saved' | 'deleted') => void
}

type ItemField = 'product' | 'code' | 'unit' | 'warehouse' | 'invoiceStatus' | 'invoiceNumber'
  | 'supplier' | 'receiptNumber' | 'issueNumber' | 'quantity' | 'requestNumber' | 'description'

/** فیلدهایی که ردیف جدید از ردیف قبلی ارث می‌برد (سرعت ورود اطلاعات). */
const INHERITED_FIELDS = ['warehouseId', 'invoiceStatus', 'invoiceNumber', 'supplierId', 'supplierText', 'receiptNumber'] as const

// نگاشت مسیر خطای API به فیلد فرم (ترتیب مهم است)
const ITEM_FIELD_MAP: [string, ItemField][] = [
  ['producttitle', 'product'], ['productid', 'product'], ['productcode', 'code'], ['unitofmeasure', 'unit'],
  ['warehouse', 'warehouse'], ['invoicenumber', 'invoiceNumber'], ['invoicestatus', 'invoiceStatus'],
  ['supplier', 'supplier'], ['receiptnumber', 'receiptNumber'], ['issuenumber', 'issueNumber'], ['quantity', 'quantity'],
  ['requestnumber', 'requestNumber'], ['description', 'description'],
]
const FORM_FIELD_MAP: [string, string][] = [
  ['enteredatutc', 'enteredAtLocal'], ['deliverer', 'deliverer'], ['pagenumber', 'pageNumber'], ['description', 'description'],
]

const productKey = (value: string): string =>
  value.replace(/[\s\u200C\u2212\p{Dash_Punctuation}]+/gu, '').toLocaleUpperCase('en-US')
const textKey = (value: string): string =>
  value.replace(/\u200C/gu, ' ').trim().replace(/\s+/gu, ' ').toLocaleUpperCase('fa-IR')

function newItem(key: number): FormItem {
  return {
    key, productText: '', productId: null, productCode: '', unitOfMeasureId: null, unitOfMeasureTitle: '',
    warehouseId: '', invoiceStatus: null, invoiceNumber: '', supplierId: '', supplierText: '',
    receiptNumber: '', issueNumber: '', quantity: null, requestNumber: '', description: '',
  }
}

function itemHasContent(item: FormItem): boolean {
  return Boolean(
    item.productId || item.productText.trim() || item.productCode.trim() || item.unitOfMeasureId ||
    item.unitOfMeasureTitle.trim() || item.quantity !== null || item.issueNumber.trim() || item.requestNumber.trim() || item.description.trim(),
  )
}

export function useGoodsReceiptForm(deps: Dependencies) {
  const { has } = usePermissions()
  const permissions = {
    canInitialEntry: has('GoodsReceiptsInitialEntry'),
    canManageReceiptInvoice: has('GoodsReceiptsReceiptInvoiceOperations'),
    canManageMasterData: has('MasterDataManage'),
    canCreateProducts: has('ProductsCreate'),
  }
  const { handleUnauthorized } = useUnauthorizedRedirect('/dashboard/inventory/goods-receipts')
  const channel = useRequestChannel()
  const errors = useFieldErrors()

  const open = ref(false)
  const editingId = ref<string | null>(null)
  const concurrencyStamp = ref<string | null>(null)   // جدید
  const saving = ref(false)
  const formError = ref('')

  const form = reactive({ enteredAtLocal: '', description: '', delivererId: '', delivererText: '', pageNumber: '' })
  const entry = useEntryRows<FormItem>({ create: newItem, hasContent: itemHasContent, minimumRows: 8 })

  // ── وضعیت‌های مشتق‌شده ───────────────────────────────────────────
  const isNewDeliverer = (): boolean => !form.delivererId && Boolean(form.delivererText.trim())
  const isNewProduct = (item: FormItem): boolean =>
    !item.productId && Boolean(item.productText.trim() || item.productCode.trim())
  const isNewSupplier = (item: FormItem): boolean => !item.supplierId && Boolean(item.supplierText.trim())
  const hasInvoiceNumber = (item: FormItem): boolean => item.invoiceStatus === 'HasInvoice'

  const rowError = (item: FormItem, field: ItemField): string => errors.getRow(item.key, field)
  const clearRowError = (item: FormItem, field: ItemField): void => errors.clearRow(item.key, field)

  // ── باز و بسته‌کردن ─────────────────────────────────────────────
  function reset(): void {
    editingId.value = null
    concurrencyStamp.value = null
    Object.assign(form, { enteredAtLocal: nowLocalDateTime(), description: '', delivererId: '', delivererText: '', pageNumber: '' })
    entry.reset()
    errors.clearAll()
    formError.value = ''
  }

  function openCreate(): void {
    reset()
    open.value = true
  }

  async function openEdit(row: GoodsReceipt | GoodsReceiptItemRow): Promise<void> {
    const id = 'goodsReceiptId' in row ? row.goodsReceiptId : row.id
    let receipt: GoodsReceipt
    try {
      receipt = await getGoodsReceipt(id)
    } catch (error) {
      if (await handleUnauthorized(error)) return
      toast.error(error instanceof Error ? error.message : 'دریافت سند انجام نشد.')
      return
    }
    reset()
    editingId.value = id,
      concurrencyStamp.value = receipt.concurrencyStamp
    Object.assign(form, {
      enteredAtLocal: toLocalDateTime(receipt.enteredAtUtc),
      description: receipt.description ?? '',
      delivererId: receipt.delivererId,
      delivererText: receipt.delivererFullName,
      pageNumber: receipt.pageNumber,
    })
    entry.reset(receipt.items.map(item => Object.assign(entry.newRow(), {
      productId: item.productId, productText: item.productTitle, productCode: item.productCode,
      unitOfMeasureId: item.unitOfMeasureId, unitOfMeasureTitle: item.unitOfMeasureTitle,
      warehouseId: item.warehouseId, invoiceStatus: item.invoiceStatus, invoiceNumber: item.invoiceNumber ?? '',
      supplierId: item.supplierId ?? '', supplierText: item.counterparty ?? '', receiptNumber: item.receiptNumber ?? '',
      issueNumber: item.issueNumber ?? '',
      quantity: item.quantity, requestNumber: item.requestNumber ?? '', description: item.description ?? '',
    })))
    open.value = true
  }

  function close(): void {
    if (saving.value) return
    open.value = false
    formError.value = ''
  }

  // ── رویدادهای کالا / واحد ───────────────────────────────────────
  function selectProduct(item: FormItem, product: Product): void {
    entry.inheritFromPrevious(item, [...INHERITED_FIELDS])
    item.productId = product.id
    item.productText = product.title
    item.productCode = product.code
    item.unitOfMeasureId = product.unitOfMeasureId
    item.unitOfMeasureTitle = product.unitOfMeasureTitle
    // توضیحات تایپ‌شدهٔ کاربر بازنویسی نمی‌شود
    if (!item.description.trim()) item.description = product.description ?? ''
    clearRowError(item, 'product'); clearRowError(item, 'code'); clearRowError(item, 'unit')
    focusEntryField(item.key, 'quantity')
  }

  function onProductText(item: FormItem, field: 'title' | 'code', value: string): void {
    if (field === 'title') item.productText = value
    else item.productCode = value
    entry.inheritFromPrevious(item, [...INHERITED_FIELDS])
    clearRowError(item, field === 'title' ? 'product' : 'code')
  }

  /** متن کالا دستی تغییر کرد؛ انتخاب قبلی بی‌اعتبار می‌شود. */
  function clearProduct(item: FormItem, field: 'title' | 'code'): void {
    item.productId = null
    item.unitOfMeasureId = null
    item.unitOfMeasureTitle = ''
    if (field === 'title') item.productCode = ''
    else item.productText = ''
  }

  function onUnitText(item: FormItem, value: string): void {
    item.unitOfMeasureTitle = value
    clearRowError(item, 'unit')
  }

  const advanceFromProductTitle = (item: FormItem): void => {
    if (item.productId) focusEntryField(item.key, 'quantity')
    else if (item.productText.trim()) focusEntryField(item.key, 'code')
  }
  const advanceFromProductCode = (item: FormItem): void =>
    focusEntryField(item.key, item.productId ? 'quantity' : 'unit')
  const advanceFromUnit = (item: FormItem): void => focusEntryField(item.key, 'quantity')

  function onInvoiceStatusChange(item: FormItem): void {
    if (!hasInvoiceNumber(item)) item.invoiceNumber = ''
    clearRowError(item, 'invoiceStatus')
  }

  // ── اعتبارسنجی ──────────────────────────────────────────────────
  function validate(): boolean {
    errors.clearAll()
    const { canManageReceiptInvoice, canManageMasterData, canCreateProducts } = permissions

    if (!form.enteredAtLocal) errors.set('enteredAtLocal', 'تاریخ و ساعت ورود کالا را وارد کنید.')
    if (!form.delivererText.trim()) errors.set('deliverer', 'تحویل‌دهنده را وارد یا انتخاب کنید.')
    else if (isNewDeliverer() && !canManageMasterData)
      errors.set('deliverer', 'مجوز افزودن تحویل‌دهنده جدید را ندارید؛ از فهرست انتخاب کنید.')
    if (!form.pageNumber.trim()) errors.set('pageNumber', 'شماره صفحه را وارد کنید.')

    const items = entry.rowsForSubmission.value
    if (!items.length) errors.set('rows', 'حداقل یک کالا وارد کنید.')

    for (const item of items) {
      const set = (field: ItemField, message: string) => errors.setRow(item.key, field, message)
      if (!item.warehouseId) set('warehouse', 'انبار را انتخاب کنید.')
      if (canManageReceiptInvoice && hasInvoiceNumber(item) && !item.invoiceNumber.trim())
        set('invoiceNumber', 'شماره فاکتور را وارد کنید.')
      if (canManageReceiptInvoice && isNewSupplier(item) && !canManageMasterData)
        set('supplier', 'مجوز افزودن تأمین‌کننده جدید را ندارید؛ از فهرست انتخاب کنید.')
      if (!item.productText.trim()) set('product', 'عنوان کالا را وارد کنید.')
      else if (!item.productId && !canCreateProducts)
        set('product', 'کالا را از فهرست انتخاب کنید؛ مجوز افزودن کالای جدید را ندارید.')
      if (!item.quantity || item.quantity <= 0) set('quantity', 'مقدار باید بزرگ‌تر از صفر باشد.')
      if (!item.productId && !item.productCode.trim()) set('code', 'کد کالای جدید را وارد کنید.')
      if (!item.productId && !item.unitOfMeasureId) set('unit', 'واحد را از فهرست موجود انتخاب کنید.')
    }

    validateDuplicates(items)
    return !errors.hasAny()
  }

  function duplicateKey(item: FormItem): string {
    const product = item.productId
      ?? `new:${productKey(item.productText)}:${productKey(item.productCode)}:${item.unitOfMeasureId ?? ''}`
    return JSON.stringify([
      product, item.warehouseId, item.invoiceStatus, textKey(item.invoiceNumber),
      item.supplierId || `new:${textKey(item.supplierText)}`, textKey(item.receiptNumber),
      Number(item.quantity), textKey(item.requestNumber), textKey(item.description),
    ])
  }

  const sameNewProduct = (a: FormItem, b: FormItem): boolean =>
    productKey(a.productText) === productKey(b.productText)
    && productKey(a.productCode) === productKey(b.productCode)
    && a.unitOfMeasureId === b.unitOfMeasureId

  function validateDuplicates(items: FormItem[]): void {
    const seen = new Map<string, FormItem>()
    for (const item of items) {
      const key = duplicateKey(item)
      const previous = seen.get(key)
      if (!previous) { seen.set(key, item); continue }
      const message = 'این ردیف کاملاً تکراری است؛ حداقل یکی از مشخصات آن را تغییر دهید.'
      errors.setRow(previous.key, 'product', message)
      errors.setRow(item.key, 'product', message)
    }

    const titles = new Map<string, FormItem>()
    const codes = new Map<string, FormItem>()
    for (const item of items.filter(i => !i.productId)) {
      const check = (map: Map<string, FormItem>, key: string, field: ItemField, message: string) => {
        if (!key) return
        const previous = map.get(key)
        if (previous && !sameNewProduct(previous, item)) {
          errors.setRow(previous.key, field, message)
          errors.setRow(item.key, field, message)
        } else map.set(key, item)
      }
      check(titles, productKey(item.productText), 'product', 'کالاهای جدید با این عنوان باید کد و واحد یکسانی داشته باشند.')
      check(codes, productKey(item.productCode), 'code', 'کالاهای جدید با این کد باید عنوان و واحد یکسانی داشته باشند.')
    }
  }

  function applyApiFieldErrors(error: GoodsReceiptsApiError): boolean {
    const items = entry.rowsForSubmission.value
    let applied = false
    for (const [path, messages] of Object.entries(error.fieldErrors)) {
      const message = messages.find(Boolean)
      if (!message) continue
      const lower = path.toLowerCase()
      let key = ''
      const match = lower.match(/items\[(\d+)]\.(.+)$/)
      if (match) {
        const item = items[Number(match[1])]
        const field = ITEM_FIELD_MAP.find(([fragment]) => match[2].includes(fragment))?.[1]
        if (item && field) key = errors.rowKey(item.key, field)
      } else {
        key = FORM_FIELD_MAP.find(([fragment]) => lower.includes(fragment))?.[1] ?? (lower.endsWith('items') ? 'rows' : '')
      }
      if (key) { errors.set(key, message); applied = true }
    }
    return applied
  }

  // ── ذخیره و حذف ─────────────────────────────────────────────────
  function buildPayload(): CreateGoodsReceiptRequest {
    const { canManageReceiptInvoice } = permissions
    return {
      enteredAtUtc: localDateTimeToIso(form.enteredAtLocal),
      description: form.description.trim() || null,
      delivererId: form.delivererId || null,
      concurrencyStamp: editingId.value ? concurrencyStamp.value : null,
      delivererFullName: form.delivererId ? null : form.delivererText.trim(),
      pageNumber: form.pageNumber.trim(),
      items: entry.rowsForSubmission.value.map(item => ({
        productId: item.productId,
        productTitle: item.productId ? null : item.productText.trim(),
        productCode: item.productId ? null : item.productCode.trim(),
        unitOfMeasureId: item.productId ? null : item.unitOfMeasureId,
        warehouseId: item.warehouseId,
        invoiceStatus: canManageReceiptInvoice ? item.invoiceStatus : null,
        invoiceNumber: canManageReceiptInvoice && hasInvoiceNumber(item) ? item.invoiceNumber.trim() : null,
        supplierId: canManageReceiptInvoice ? (item.supplierId || null) : null,
        supplierTitle: canManageReceiptInvoice && !item.supplierId ? (item.supplierText.trim() || null) : null,
        receiptNumber: canManageReceiptInvoice ? (item.receiptNumber.trim() || null) : null,
        issueNumber: item.issueNumber.trim() || null,
        quantity: Number(item.quantity),
        requestNumber: item.requestNumber.trim() || null,
        description: item.description.trim() || null,
      })),
    }
  }

  async function save(): Promise<void> {
    if (!validate()) { formError.value = ''; return }
    const controller = channel.begin()
    saving.value = true
    formError.value = ''
    const wasEditing = Boolean(editingId.value)
    try {
      const payload = buildPayload()
      if (editingId.value) await updateGoodsReceipt(editingId.value, payload, controller.signal)
      else await createGoodsReceipt(payload, controller.signal)
      open.value = false
      toast.success(wasEditing ? 'سند ورود کالا با موفقیت ویرایش شد.' : 'ورود کالا با موفقیت ثبت شد.')
      deps.onSaved('saved')
    } catch (error) {
      if (isAbortError(error)) return
      if (await handleUnauthorized(error)) return
      if (error instanceof GoodsReceiptsApiError && applyApiFieldErrors(error)) formError.value = ''
      // if (error instanceof GoodsReceiptsApiError && (error.status === 409 || error.status === 412)) {
      //   formError.value = 'این سند توسط کاربر دیگری تغییر کرده است. مودال را ببندید و سند را دوباره باز کنید.'
      //   return
      // }
      else formError.value = error instanceof Error ? error.message : 'ثبت ورود کالا انجام نشد.'
    } finally {
      if (channel.isCurrent(controller)) { channel.end(controller); saving.value = false }
    }
  }

  async function remove(): Promise<void> {
    const id = editingId.value
    if (!id || !permissions.canInitialEntry || saving.value) return
    const confirmed = await deps.confirm({
      title: 'حذف سند ورود کالا',
      text: 'با حذف این ورودی، سند ورود و اطلاعات تمام کالاهای ثبت‌شده در این ورودی حذف می‌شوند. آیا از حذف مطمئن هستید؟',
      icon: 'warning', confirmButtonText: 'حذف', cancelButtonText: 'انصراف', confirmButtonColor: '#dc3545',
    })
    if (!confirmed) return
    saving.value = true // فقط در طول درخواست حذف، نه هنگام باز بودن دیالوگ تأیید
    try {
      await deleteGoodsReceipt(id)
      open.value = false
      editingId.value = null
      toast.success('سند ورود کالا حذف شد.')
      deps.onSaved('deleted')
    } catch (error) {
      if (await handleUnauthorized(error)) return
      toast.error(error instanceof Error ? error.message : 'حذف سند انجام نشد.')
    } finally {
      saving.value = false
    }
  }

  return {
    permissions, open, editingId, saving, formError, form, errors,
    rows: entry.rows, mode: entry.mode, setMode: entry.setMode, addRow: entry.addRow, removeRow: entry.removeRow,
    itemHasContent, isNewDeliverer, isNewProduct, isNewSupplier, hasInvoiceNumber, rowError, clearRowError,
    openCreate, openEdit, close, save, remove,
    selectProduct, onProductText, clearProduct, onUnitText, onInvoiceStatusChange,
    advanceFromProductTitle, advanceFromProductCode, advanceFromUnit,
  }
}
