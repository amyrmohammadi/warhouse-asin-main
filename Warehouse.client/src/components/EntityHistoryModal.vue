<script setup lang="ts">
import { onBeforeUnmount, watch } from 'vue'

import type { EntityHistoryEntry } from '@/services/goodsReceipts'

defineOptions({ name: 'EntityHistoryModal' })

const props = defineProps<{
  open: boolean
  title: string
  loading: boolean
  error: string
  entries: EntityHistoryEntry[]
}>()

const emit = defineEmits<{ close: [] }>()

watch(
  () => props.open,
  open => document.body.classList.toggle('modal-open', open),
)

onBeforeUnmount(() => document.body.classList.remove('modal-open'))

function close(): void {
  if (!props.loading) emit('close')
}

function formatDate(value: string): string {
  return new Intl.DateTimeFormat('fa-IR', {
    year: 'numeric', month: '2-digit', day: '2-digit',
    hour: '2-digit', minute: '2-digit', second: '2-digit',
  }).format(new Date(value))
}

function actionClass(action: EntityHistoryEntry['action']): string {
  return action === 'Create' ? 'badge-success' : action === 'Delete' ? 'badge-danger' : 'badge-info'
}
</script>

<template>
  <Teleport to="body">
    <template v-if="open">
      <div class="modal show d-block entity-history-modal"
           tabindex="-1" role="dialog" aria-modal="true"
           aria-labelledby="entity-history-title"
           @click.self="close">
        <div class="modal-dialog modal-dialog-centered modal-lg" role="document">
          <div class="modal-content">
            <div class="modal-header">
              <div>
                <h5 id="entity-history-title" class="modal-title">تاریخچه تغییرات</h5>
                <small class="text-muted">{{ title }}</small>
              </div>
              <button type="button" class="close" aria-label="بستن" :disabled="loading" @click="close">
                <span aria-hidden="true">&times;</span>
              </button>
            </div>

            <div class="modal-body entity-history-modal__body">
              <div v-if="loading" class="entity-history-modal__state">
                <span class="spinner-border spinner-border-sm ml-2"></span>
                در حال دریافت تاریخچه...
              </div>
              <div v-else-if="error" class="alert alert-danger mb-0">{{ error }}</div>
              <div v-else-if="!entries.length" class="entity-history-modal__state">
                تغییری برای این کالا ثبت نشده است.
              </div>
              <div v-else class="entity-history-timeline">
                <article v-for="entry in entries" :key="entry.id" class="entity-history-entry">
                  <header class="entity-history-entry__header">
                    <div>
                      <span class="badge light" :class="actionClass(entry.action)">{{ entry.actionLabel }}</span>
                      <strong>{{ entry.sectionLabel }}</strong>
                    </div>
                    <div class="entity-history-entry__meta">
                      <span><i class="las la-user ml-1"></i>{{ entry.userFullName }}</span>
                      <time><i class="las la-clock ml-1"></i>{{ formatDate(entry.occurredAtUtc) }}</time>
                    </div>
                  </header>

                  <div class="table-responsive">
                    <table class="table table-sm entity-history-entry__table mb-0">
                      <thead>
                        <tr><th>فیلد</th><th>مقدار قبلی</th><th>مقدار جدید</th></tr>
                      </thead>
                      <tbody>
                        <tr v-for="change in entry.changes" :key="change.fieldName">
                          <th>{{ change.fieldLabel }}</th>
                          <td>{{ change.oldValue }}</td>
                          <td>{{ change.newValue }}</td>
                        </tr>
                      </tbody>
                    </table>
                  </div>
                </article>
              </div>
            </div>

            <div class="modal-footer">
              <button class="btn btn-light btn-sm" type="button" :disabled="loading" @click="close">بستن</button>
            </div>
          </div>
        </div>
      </div>
      <div class="modal-backdrop show"></div>
    </template>
  </Teleport>
</template>

<style scoped>
.entity-history-modal__body { max-height: min(72vh, 46rem); overflow-y: auto; }
.entity-history-modal__state { color: var(--text-secondary); padding: 2.5rem 1rem; text-align: center; }
.entity-history-timeline { display: grid; gap: .85rem; }
.entity-history-entry {
  background: color-mix(in srgb, var(--surface) 70%, var(--background));
  border: 1px solid var(--border-color); border-radius: .75rem; overflow: hidden;
}
.entity-history-entry__header {
  align-items: center; display: flex; gap: .75rem; justify-content: space-between; padding: .75rem;
}
.entity-history-entry__header > div:first-child { align-items: center; display: flex; gap: .5rem; }
.entity-history-entry__meta { color: var(--text-secondary); display: flex; flex-wrap: wrap; font-size: .75rem; gap: .75rem; }
.entity-history-entry__table th, .entity-history-entry__table td { border-color: var(--border-color); font-size: .78rem; padding: .55rem .7rem; }
.entity-history-entry__table thead th { background: var(--background); color: var(--text-primary); }
.entity-history-entry__table tbody th { color: var(--text-primary); min-width: 8rem; }
.entity-history-entry__table td { color: var(--text-secondary); min-width: 9rem; white-space: pre-wrap; }
@media (max-width: 575px) {
  .entity-history-modal .modal-dialog { margin: .25rem; }
  .entity-history-entry__header { align-items: flex-start; flex-direction: column; }
  .entity-history-entry__meta { flex-direction: column; gap: .25rem; }
}
</style>
