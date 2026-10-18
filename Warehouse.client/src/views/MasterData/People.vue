<script setup lang="ts">
import MasterDataCrudPage from '@/components/masterData/MasterDataCrudPage.vue'

defineOptions({ name: 'PeopleView' })

const config = {
  basePath: '/endpoint/master-data/people',
  routePath: '/dashboard/master-data/people',
  singularLabel: 'فرد',
  pluralLabel: 'افراد',
  pageDescription: 'تعریف و مدیریت اطلاعات افراد مرتبط با عملیات انبار',
  icon: 'las la-address-book',
  quickSearchPlaceholder: 'جستجو در نام کامل و شماره تلفن...',
  fields: [
    {
      key: 'fullName',
      label: 'نام کامل',
      placeholder: 'نام کامل را وارد کنید',
    },
    {
      key: 'phoneNumber',
      label: 'شماره تلفن',
      placeholder: 'شماره تلفن را وارد کنید',
      type: 'tel' as const,
      inputDirection: 'ltr' as const,
    },
  ],
}
</script>

<template>
  <MasterDataCrudPage :config="config" />
</template>
