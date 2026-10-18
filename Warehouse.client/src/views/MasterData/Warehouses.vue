<script setup lang="ts">
import MasterDataCrudPage from '@/components/masterData/MasterDataCrudPage.vue'

defineOptions({ name: 'WarehousesView' })

const config = {
  basePath: '/endpoint/master-data/warehouses',
  routePath: '/dashboard/master-data/warehouses',
  singularLabel: 'انبار',
  pluralLabel: 'انبارها',
  pageDescription: 'تعریف و مدیریت انبارهای قابل استفاده در سامانه',
  icon: 'las la-warehouse',
  quickSearchPlaceholder: 'جستجو در عنوان و توضیحات...',
  fields: [
    {
      key: 'title',
      label: 'عنوان',
      placeholder: 'عنوان انبار را وارد کنید',
    },
    {
      key: 'description',
      label: 'توضیحات',
      placeholder: 'توضیحات انبار را وارد کنید',
      type: 'textarea' as const,
      sortable: false,
    },
  ],
}
</script>

<template>
  <MasterDataCrudPage :config="config" />
</template>
