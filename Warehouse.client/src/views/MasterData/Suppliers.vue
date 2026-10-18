<script setup lang="ts">
import MasterDataCrudPage from '@/components/masterData/MasterDataCrudPage.vue'

defineOptions({ name: 'SuppliersView' })

const config = {
  basePath: '/endpoint/master-data/suppliers',
  routePath: '/dashboard/master-data/suppliers',
  singularLabel: 'تأمین‌کننده',
  pluralLabel: 'تأمین‌کنندگان',
  pageDescription: 'تعریف و مدیریت تأمین‌کنندگان قابل استفاده در عملیات انبار',
  icon: 'las la-truck-loading',
  quickSearchPlaceholder: 'جستجو در عنوان و توضیحات...',
  import: {
    label: 'ایمپورت از Excel',
  },
  fields: [
    {
      key: 'title',
      label: 'عنوان',
      placeholder: 'عنوان تأمین‌کننده را وارد کنید',
    },
    {
      key: 'description',
      label: 'توضیحات',
      placeholder: 'توضیحات تأمین‌کننده را وارد کنید',
      type: 'textarea' as const,
      sortable: false,
    },
  ],
}
</script>

<template>
  <MasterDataCrudPage :config="config" />
</template>
