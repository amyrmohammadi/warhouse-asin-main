import { reactive, ref } from 'vue'

import { useEntryRows } from '@/composables/useEntryRows'
import { useFieldErrors } from '@/composables/useFieldErrors'
import { usePermissions } from '@/composables/usePermissions'
import { isAbortError, useRequestChannel } from '@/composables/useRequestChannel'
import { useUnauthorizedRedirect } from '@/composables/useUnauthorizedRedirect'
import type { GoodsIssueDestinationOption } from '@/services/goodsIssueDestinations'
import {
  createGoodsIssue,
  deleteGoodsIssue,
  getGoodsIssue,
  GoodsIssuesApiError,
  updateGoodsIssue,
  type GoodsIssue,
  type GoodsIssueItemRow,
  type SaveGoodsIssueRequest,
} from '@/services/goodsIssues'
import type { Product } from '@/services/products'
import { toast } from '@/services/toast'
import {
  localDateTimeToIso,
  localDateToIso,
  nowLocalDateTime,
  toLocalDate,
  toLocalDateTime,
} from '@/utils/dateTime'
import { focusEntryField } from '@/utils/entryFocus'

export interface FormItem {
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

type ItemField = 'product' | 'code' | 'quantity' | 'assetNumber' | 'destination'
  | 'exitReason' | 'description' | 'deliveringUnit' | 'returnAtLocal'

const ITEM_FIELD_MAP: [string, ItemField][] = [
  ['productid', 'product'], ['productcode', 'code'], ['quantity', 'quantity'],
  ['assetnumber', 'assetNumber'], ['destination', 'destination'], ['exitreason', 'exitReason'],
  ['deliveringunit', 'deliveringUnit'], ['returnatutc', 'returnAtLocal'], ['description', 'description'],
]
const FORM_FIELD_MAP: [string, string][] = [
  ['issuenumber', 'issueNumber'], ['issuedatutc', 'issuedAtLocal'],
  ['issuer', 'issuer'], ['description', 'description'],
]

function newItem(key: number): FormItem {
  return {
    key,
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

function itemHasContent(item: FormItem): boolean {
  return Boolean(
    item.productId || item.productTitle.trim() || item.productCode.trim() || item.quantity !== null
    || item.assetNumber.trim() || item.destinationId || item.destinationTitle.trim()
    || item.exitReason.trim() || item.description.trim() || item.deliveringUnit.trim()
    || item.returnAtLocal,
  )
}

export function useGoodsIssueForm(deps: Dependencies) {
  const { has } = usePermissions()
  const permissions = {
    canCreate: has('GoodsIssuesCreate'),
    canUpdate: has('GoodsIssuesUpdate'),
    canDelete: has('GoodsIssuesDelete'),
    canManageMasterData: has('MasterDataManage'),
  }
  const { handleUnauthorized } = useUnauthorizedRedirect('/dashboard/inventory/goods-issues')
  const channel = useRequestChannel()
  const errors = useFieldErrors()

  const open = ref(false)
  const editingId = ref<string | null>(null)
  const concurrencyStamp = ref<string | null>(null)
  const saving = ref(false)
  const formError = ref('')
  const form = reactive({ issueNumber: '', issuedAtLocal: '', description: '', issuerId: '', issuerText: '' })
  const entry = useEntryRows<FormItem>({ create: newItem, hasContent: itemHasContent, minimumRows: 8 })

  const isNewIssuer = (): boolean => !form.issuerId && Boolean(form.issuerText.trim())
  const isNewDestination = (item: FormItem): boolean =>
    !item.destinationId && Boolean(item.destinationTitle.trim())
  const rowError = (item: FormItem, field: ItemField): string => errors.getRow(item.key, field)
  const clearRowError = (item: FormItem, field: ItemField): void => errors.clearRow(item.key, field)

  function reset(): void {
    editingId.value = null
    concurrencyStamp.value = null
    Object.assign(form, {
      issueNumber: '',
      issuedAtLocal: nowLocalDateTime(),
      description: '',
      issuerId: '',
      issuerText: '',
    })
    entry.reset()
    errors.clearAll()
    formError.value = ''
  }

  function openCreate(): void {
    reset()
    open.value = true
  }

  const issueId = (row: GoodsIssue | GoodsIssueItemRow): string =>
    'goodsIssueId' in row ? row.goodsIssueId : row.id

  async function openEdit(row: GoodsIssue | GoodsIssueItemRow): Promise<void> {
    const id = issueId(row)
    const controller = channel.begin()
    try {
      const issue = await getGoodsIssue(id, controller.signal)
      reset()
      editingId.value = id
      concurrencyStamp.value = issue.concurrencyStamp
      Object.assign(form, {
        issueNumber: issue.issueNumber,
        issuedAtLocal: toLocalDateTime(issue.issuedAtUtc),
        description: issue.description ?? '',
        issuerId: issue.issuerId,
        issuerText: issue.issuerFullName,
      })
      entry.reset(issue.items.map(item => Object.assign(entry.newRow(), {
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
      })))
      open.value = true
    } catch (error) {
      if (isAbortError(error)) return
      if (await handleUnauthorized(error)) return
      toast.error(error instanceof Error ? error.message : 'دریافت سند خروج انجام نشد.')
    } finally {
      if (channel.isCurrent(controller)) channel.end(controller)
    }
  }

  function close(): void {
    if (saving.value) return
    open.value = false
    formError.value = ''
  }

  function selectProduct(item: FormItem, product: Product): void {
    entry.inheritFromPrevious(item, ['destinationId', 'destinationTitle'])
    item.productId = product.id
    item.productCode = product.code
    item.productTitle = product.title
    item.productUnitOfMeasureTitle = product.unitOfMeasureTitle
    clearRowError(item, 'product')
    clearRowError(item, 'code')
    focusEntryField(item.key, 'quantity')
  }

  function onProductText(item: FormItem, field: 'title' | 'code', value: string): void {
    if (field === 'title') item.productTitle = value
    else item.productCode = value
    entry.inheritFromPrevious(item, ['destinationId', 'destinationTitle'])
    clearRowError(item, field === 'title' ? 'product' : 'code')
  }

  function clearProduct(item: FormItem, field: 'title' | 'code'): void {
    item.productId = null
    item.productUnitOfMeasureTitle = ''
    if (field === 'title') item.productCode = ''
    else item.productTitle = ''
  }

  function selectDestination(item: FormItem, destination: GoodsIssueDestinationOption): void {
    item.destinationId = destination.id
    item.destinationTitle = destination.title
    clearRowError(item, 'destination')
  }

  function onDestinationText(item: FormItem, value: string): void {
    item.destinationTitle = value
    clearRowError(item, 'destination')
  }

  function clearDestination(item: FormItem): void {
    item.destinationId = null
    clearRowError(item, 'destination')
  }

  const advanceFromProductTitle = (item: FormItem): void =>
    focusEntryField(item.key, item.productId ? 'quantity' : 'code')
  const advanceFromProductCode = (item: FormItem): void => focusEntryField(item.key, 'quantity')

  function validate(): boolean {
    errors.clearAll()
    if (!form.issueNumber.trim()) errors.set('issueNumber', 'کد یا شماره خروج را وارد کنید.')
    if (!form.issuedAtLocal) errors.set('issuedAtLocal', 'تاریخ خروج را وارد کنید.')
    if (!form.issuerText.trim()) errors.set('issuer', 'خارج‌کننده را انتخاب یا وارد کنید.')
    else if (isNewIssuer() && !permissions.canManageMasterData)
      errors.set('issuer', 'مجوز افزودن خارج‌کننده جدید را ندارید؛ از فهرست انتخاب کنید.')

    const items = entry.rowsForSubmission.value
    if (!items.length) errors.set('rows', 'حداقل یک ردیف کالا اضافه کنید.')
    if (items.length > 100) errors.set('rows', 'در هر سند حداکثر ۱۰۰ ردیف قابل ثبت است.')

    const issuedAt = form.issuedAtLocal ? new Date(form.issuedAtLocal) : null
    for (const item of items) {
      const set = (field: ItemField, message: string) => errors.setRow(item.key, field, message)
      if (!item.productCode.trim()) set('code', 'کد کالا الزامی است.')
      if (!item.quantity || item.quantity <= 0) set('quantity', 'تعداد باید بزرگ‌تر از صفر باشد.')
      if (!item.destinationTitle.trim()) set('destination', 'مقصد الزامی است.')
      else if (isNewDestination(item) && !permissions.canManageMasterData)
        set('destination', 'مقصد را از فهرست انتخاب کنید.')
      if (!item.exitReason.trim()) set('exitReason', 'علت خروج الزامی است.')
      if (!item.deliveringUnit.trim()) set('deliveringUnit', 'واحد تحویل‌دهنده الزامی است.')
      if (issuedAt && item.returnAtLocal && new Date(`${item.returnAtLocal}T00:00:00`) < issuedAt)
        set('returnAtLocal', 'تاریخ عودت نمی‌تواند قبل از تاریخ خروج باشد.')
    }
    return !errors.hasAny()
  }

  function applyApiFieldErrors(error: GoodsIssuesApiError): boolean {
    let applied = false
    const items = entry.rowsForSubmission.value
    for (const [path, messages] of Object.entries(error.fieldErrors)) {
      const message = messages.find(Boolean)
      if (!message) continue
      const lower = path.toLowerCase()
      const match = lower.match(/items\[(\d+)]\.(.+)$/)
      let key = ''
      if (match) {
        const item = items[Number(match[1])]
        const field = ITEM_FIELD_MAP.find(([fragment]) => match[2].includes(fragment))?.[1]
        if (item && field) key = errors.rowKey(item.key, field)
      } else {
        key = FORM_FIELD_MAP.find(([fragment]) => lower.includes(fragment))?.[1]
          ?? (lower.endsWith('items') ? 'rows' : '')
      }
      if (key) {
        errors.set(key, message)
        applied = true
      }
    }
    return applied
  }

  function buildPayload(): SaveGoodsIssueRequest {
    return {
      issueNumber: form.issueNumber.trim(),
      issuedAtUtc: localDateTimeToIso(form.issuedAtLocal),
      description: form.description.trim() || null,
      issuerId: form.issuerId || null,
      issuerFullName: form.issuerId ? null : form.issuerText.trim(),
      concurrencyStamp: editingId.value ? concurrencyStamp.value : null,
      items: entry.rowsForSubmission.value.map(item => ({
        productId: item.productId,
        productCode: item.productCode.trim(),
        quantity: Number(item.quantity),
        assetNumber: item.assetNumber.trim() || null,
        destinationId: item.destinationId,
        destinationTitle: item.destinationId ? null : item.destinationTitle.trim(),
        exitReason: item.exitReason.trim(),
        description: item.description.trim() || null,
        deliveringUnit: item.deliveringUnit.trim(),
        returnAtUtc: item.returnAtLocal ? localDateToIso(item.returnAtLocal) : null,
      })),
    }
  }

  async function save(): Promise<void> {
    if ((editingId.value && !permissions.canUpdate) || (!editingId.value && !permissions.canCreate)) return
    if (!validate()) {
      formError.value = ''
      return
    }
    const controller = channel.begin()
    saving.value = true
    formError.value = ''
    const wasEditing = Boolean(editingId.value)
    try {
      const payload = buildPayload()
      if (editingId.value) await updateGoodsIssue(editingId.value, payload, controller.signal)
      else await createGoodsIssue(payload, controller.signal)
      open.value = false
      toast.success(wasEditing ? 'سند خروج کالا با موفقیت ویرایش شد.' : 'خروج کالا با موفقیت ثبت شد.')
      deps.onSaved('saved')
    } catch (error) {
      if (isAbortError(error)) return
      if (await handleUnauthorized(error)) return
      if (error instanceof GoodsIssuesApiError && applyApiFieldErrors(error)) formError.value = ''
      else formError.value = error instanceof Error ? error.message : 'ثبت خروج کالا انجام نشد.'
    } finally {
      if (channel.isCurrent(controller)) {
        channel.end(controller)
        saving.value = false
      }
    }
  }

  async function removeById(id: string): Promise<void> {
    if (!permissions.canDelete || saving.value) return
    const confirmed = await deps.confirm({
      title: 'حذف سند خروج کالا',
      text: 'با حذف این سند، تمام اقلام ثبت‌شده در آن نیز حذف می‌شوند. آیا از حذف مطمئن هستید؟',
      icon: 'warning',
      confirmButtonText: 'حذف',
      cancelButtonText: 'انصراف',
      confirmButtonColor: '#dc3545',
    })
    if (!confirmed) return

    const controller = channel.begin()
    saving.value = true
    try {
      await deleteGoodsIssue(id, controller.signal)
      if (editingId.value === id) {
        open.value = false
        editingId.value = null
      }
      toast.success('سند خروج کالا حذف شد.')
      deps.onSaved('deleted')
    } catch (error) {
      if (isAbortError(error)) return
      if (await handleUnauthorized(error)) return
      toast.error(error instanceof Error ? error.message : 'حذف سند خروج انجام نشد.')
    } finally {
      if (channel.isCurrent(controller)) {
        channel.end(controller)
        saving.value = false
      }
    }
  }

  const remove = (): Promise<void> => editingId.value ? removeById(editingId.value) : Promise.resolve()
  const removeDocument = (row: GoodsIssue | GoodsIssueItemRow): Promise<void> => removeById(issueId(row))

  return {
    permissions,
    open,
    editingId,
    saving,
    formError,
    form,
    errors,
    rows: entry.rows,
    mode: entry.mode,
    setMode: entry.setMode,
    addRow: entry.addRow,
    removeRow: entry.removeRow,
    itemHasContent,
    isNewIssuer,
    isNewDestination,
    rowError,
    clearRowError,
    openCreate,
    openEdit,
    close,
    save,
    remove,
    removeDocument,
    selectProduct,
    onProductText,
    clearProduct,
    selectDestination,
    onDestinationText,
    clearDestination,
    advanceFromProductTitle,
    advanceFromProductCode,
  }
}
