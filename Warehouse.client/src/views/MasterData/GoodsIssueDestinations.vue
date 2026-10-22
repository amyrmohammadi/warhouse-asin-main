<script setup lang="ts">
import MasterDataCrudPage from '@/components/masterData/MasterDataCrudPage.vue'

defineOptions({ name: 'GoodsIssueDestinationsView' })

const config = {
  basePath: '/endpoint/master-data/goods-issue-destinations',
  routePath: '/dashboard/master-data/goods-issue-destinations',
  singularLabel: 'مقصد خروج',
  pluralLabel: 'مقصدهای خروج',
  pageDescription: 'تعریف و مدیریت شهرها و مقصدهای قابل استفاده در خروج کالا',
  icon: 'las la-map-marker-alt',
  quickSearchPlaceholder: 'جستجو در عنوان و توضیحات...',
  fields: [
    { key: 'title', label: 'عنوان مقصد', placeholder: 'نام شهر یا مقصد را وارد کنید' },
    { key: 'description', label: 'توضیحات', placeholder: 'توضیحات مقصد را وارد کنید', type: 'textarea' as const, sortable: false },
  ],
}
</script>

<template><MasterDataCrudPage :config="config" /></template>
