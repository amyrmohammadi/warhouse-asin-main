<script setup lang="ts">
import type { InvoiceProcessingStatus } from '@/services/invoices'

defineProps<{ status: InvoiceProcessingStatus }>()

const statuses: Record<InvoiceProcessingStatus, { label: string; tone: string; icon: string }> = {
  UnderReview: { label: 'در حال رسیدگی', tone: 'review', icon: 'la-clock' },
  SentToFinance: { label: 'ارسال به واحد مالی', tone: 'sent', icon: 'la-paper-plane' },
  FinanceApproved: { label: 'تأیید واحد مالی', tone: 'approved', icon: 'la-check-circle' },
}
</script>

<template>
  <div v-if="statuses[status]" class="invoice-status-chip" :class="`invoice-status-chip--${statuses[status].tone}`">
    <i class="las" :class="statuses[status].icon" aria-hidden="true"></i>
    {{ statuses[status].label }}
  </div>
</template>

<style scoped>
.invoice-status-chip {
  display: inline-flex;
  align-items: center;
  align-self: flex-start;
  gap: .25rem;
  padding: .2rem .5rem;
  border: 1px solid var(--chip-border);
  border-radius: 999px;
  background: var(--chip-bg);
  color: var(--chip-ink);
  font-size: .7rem;
  font-weight: 600;
  line-height: 1.3;
  white-space: nowrap;
}
.invoice-status-chip i { font-size: .85rem; }
.invoice-status-chip--review { --chip-bg: #fff3cd; --chip-ink: #805700; --chip-border: #efd285; }
.invoice-status-chip--sent { --chip-bg: #e3efff; --chip-ink: #1e55a5; --chip-border: #b6d1f5; }
.invoice-status-chip--approved { --chip-bg: #def5e6; --chip-ink: #176b3a; --chip-border: #a2d9b5; }
:global(body[data-app-theme='dark']) .invoice-status-chip--review { --chip-bg: #3e3218; --chip-ink: #f4d16f; --chip-border: #79612c; }
:global(body[data-app-theme='dark']) .invoice-status-chip--sent { --chip-bg: #1b304b; --chip-ink: #a4cbff; --chip-border: #3f6596; }
:global(body[data-app-theme='dark']) .invoice-status-chip--approved { --chip-bg: #193c2a; --chip-ink: #95dfb0; --chip-border: #397553; }
</style>
