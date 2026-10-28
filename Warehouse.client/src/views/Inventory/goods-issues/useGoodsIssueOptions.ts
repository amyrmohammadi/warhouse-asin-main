import { ref } from 'vue'

import type { PersonSelectOption } from '@/components/selectors/PersonSelect.vue'
import { isAbortError, useRequestChannel } from '@/composables/useRequestChannel'
import { getGoodsIssueOptions } from '@/services/goodsIssues'
import { toast } from '@/services/toast'
import type { UnitOfMeasure } from '@/services/unitsOfMeasure'

/** داده‌های مرجع مشترک میان فیلترها و فرم خروج کالا. */
export function useGoodsIssueOptions(handleUnauthorized: (error: unknown) => Promise<boolean>) {
  const people = ref<PersonSelectOption[]>([])
  const units = ref<UnitOfMeasure[]>([])
  const loaded = ref(false)
  const loading = ref(false)
  const channel = useRequestChannel()

  async function load(force = false): Promise<boolean> {
    if (loaded.value && !force) return true
    const controller = channel.begin()
    loading.value = true
    try {
      const result = await getGoodsIssueOptions(controller.signal)
      people.value = result.people
      units.value = result.unitsOfMeasure
      loaded.value = true
      return true
    } catch (error) {
      if (isAbortError(error)) return false
      if (await handleUnauthorized(error)) return false
      toast.error(error instanceof Error ? error.message : 'دریافت اطلاعات فرم انجام نشد.')
      return false
    } finally {
      if (channel.isCurrent(controller)) {
        channel.end(controller)
        loading.value = false
      }
    }
  }

  const reload = (): Promise<boolean> => load(true)

  return { people, units, loading, load, reload }
}
