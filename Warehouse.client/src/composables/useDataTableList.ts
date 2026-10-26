import type { AjaxCallback, AjaxData, Api } from 'datatables.net'
import { ref, shallowRef } from 'vue'

import { toast } from '@/services/toast'
import { isAbortError, useRequestChannel } from './useRequestChannel'

export interface ListQuery {
  page: number
  pageSize: number
  sortBy?: string
  sortDescending?: boolean
  signal: AbortSignal
}
export interface ListPage<T> {
  items: T[]
  totalCount: number
  filteredCount: number
}

interface Options<T> {
  fetchPage: (query: ListQuery) => Promise<ListPage<T>>
  errorMessage: string
  onUnauthorized?: (error: unknown) => Promise<boolean>
}

/**
 * منطق مشترک DataTables سمت سرور: صفحه‌بندی، مرتب‌سازی، لغو درخواست قبلی،
 * خطا و وضعیت loading. صفحه فقط `fetchPage` را می‌دهد.
 * وقتی `<DataTable :key="mode">` دوباره ساخته می‌شود، خودش ajax را صدا می‌زند؛
 * پس نیازی به reload دستی هنگام تغییر mode نیست.
 */
export function useDataTableList<T>(options: Options<T>) {
  const table = shallowRef<{ dt: Api } | null>(null)
  const loading = ref(false)
  const channel = useRequestChannel()

  async function ajax(request: AjaxData, callback: AjaxCallback): Promise<void> {
    const controller = channel.begin()
    loading.value = true

    const pageSize = request.length && request.length > 0 ? request.length : 10
    const order = request.order?.[0]
    const sortName = order ? request.columns?.[order.column]?.name : undefined

    try {
      const result = await options.fetchPage({
        page: Math.floor((request.start ?? 0) / pageSize) + 1,
        pageSize,
        sortBy: typeof sortName === 'string' && sortName ? sortName : undefined,
        sortDescending: order ? order.dir === 'desc' : undefined,
        signal: controller.signal,
      })
      callback({
        draw: request.draw,
        recordsTotal: result.totalCount,
        recordsFiltered: result.filteredCount,
        data: result.items,
      })
    } catch (error) {
      if (isAbortError(error)) return // درخواست جدیدتری جایگزین شده و callback خودش را صدا می‌زند
      if (await options.onUnauthorized?.(error)) return
      toast.error(error instanceof Error ? error.message : options.errorMessage)
      callback({ draw: request.draw, recordsTotal: 0, recordsFiltered: 0, data: [] })
    } finally {
      if (channel.isCurrent(controller)) {
        channel.end(controller)
        loading.value = false
      }
    }
  }

  function reload(resetPaging = true): void {
    table.value?.dt.ajax.reload(undefined, resetPaging)
  }

  return { table, loading, ajax, reload }
}
