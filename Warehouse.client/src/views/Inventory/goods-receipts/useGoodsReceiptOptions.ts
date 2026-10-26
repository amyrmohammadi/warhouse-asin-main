import { ref } from 'vue'

import { isAbortError, useRequestChannel } from '@/composables/useRequestChannel'
import type { PersonSelectOption } from '@/components/selectors/PersonSelect.vue'
import { getGoodsReceiptOptions } from '@/services/goodsReceipts'
import { toast } from '@/services/toast'
import type { UnitOfMeasure } from '@/services/unitsOfMeasure'

export interface WarehouseOption { id: string; title: string }

/** داده‌های مرجع (افراد، انبارها، واحدها) که هم فیلترها و هم فرم لازم دارند. */
export function useGoodsReceiptOptions(handleUnauthorized: (error: unknown) => Promise<boolean>) {
  const people = ref<PersonSelectOption[]>([])
  const warehouses = ref<WarehouseOption[]>([])
  const units = ref<UnitOfMeasure[]>([])
  const loaded = ref(false)
  const loading = ref(false)
  const channel = useRequestChannel()

  async function load(force = false): Promise<boolean> {
    if (loaded.value && !force) return true
    const controller = channel.begin()
    loading.value = true
    try {
      const result = await getGoodsReceiptOptions(controller.signal)
      people.value = result.people
      warehouses.value = result.warehouses
      units.value = result.unitsOfMeasure
      loaded.value = true
      return true
    } catch (error) {
      if (isAbortError(error)) return false
      if (await handleUnauthorized(error)) return false
      toast.error(error instanceof Error ? error.message : 'دریافت اطلاعات فرم انجام نشد.')
      return false
    } finally {
      if (channel.isCurrent(controller)) { channel.end(controller); loading.value = false }
    }
  }

  /** بعد از ثبت، افراد/واحدهای جدید هم در فیلترها دیده شوند (بدون خالی‌کردن لیست‌ها). */
  const reload = (): Promise<boolean> => load(true)

  return { people, warehouses, units, loading, load, reload }
}
