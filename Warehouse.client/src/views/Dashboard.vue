<script setup lang="ts">
import { useRouter } from 'vue-router'

import { clearSession, getSession } from '@/services/auth'

defineOptions({ name: 'DashboardView' })

const router = useRouter()
const session = getSession()

async function logout(): Promise<void> {
  clearSession()
  await router.replace('/login')
}
</script>

<template>
  <div class="container-fluid">
    <div class="form-head mb-4">
      <h2 class="text-black font-w600 mb-0">داشبورد</h2>
      <p class="page-description mb-0 mt-2">خوش آمدید؛ اتصال حساب کاربری شما با سرور برقرار است.</p>
    </div>

    <div class="row">
      <div class="col-xl-4 col-sm-6">
        <div class="card">
          <div class="card-body d-flex align-items-center justify-content-between">
            <div>
              <span class="fs-14">وضعیت حساب</span>
              <h3 class="fs-24 text-black font-w600 mb-0 mt-2">فعال</h3>
            </div>
            <span class="overview-icon bgl-success text-success"><i class="las la-check-circle"></i></span>
          </div>
        </div>
      </div>
      <div class="col-xl-4 col-sm-6">
        <div class="card">
          <div class="card-body d-flex align-items-center justify-content-between">
            <div>
              <span class="fs-14">سطح دسترسی</span>
              <h3 class="fs-24 text-black font-w600 mb-0 mt-2">{{ session?.permissions.length ?? 0 }}</h3>
            </div>
            <span class="overview-icon bgl-primary text-primary"><i class="las la-user-shield"></i></span>
          </div>
        </div>
      </div>
      <div class="col-xl-4 col-sm-12">
        <div class="card">
          <div class="card-body d-flex align-items-center justify-content-between">
            <div>
              <span class="fs-14">شناسه کاربر</span>
              <h3 class="fs-16 text-black font-w600 mb-0 mt-2">{{ session?.userId.slice(0, 8) }}…</h3>
            </div>
            <span class="overview-icon bgl-warning text-warning"><i class="las la-id-card"></i></span>
          </div>
        </div>
      </div>
    </div>

    <div class="row">
      <div class="col-xl-8 col-lg-7">
        <div class="card">
          <div class="card-header border-0 pb-0">
            <div>
              <h4 class="fs-20 text-black mb-1">نمای کلی سامانه</h4>
           
            </div>
          </div>
          <div class="card-body">
            <div class="row">
              <div class="col-sm-4 mb-3">
                <div class="p-3 rounded bgl-primary text-center">
                  <span class="d-block fs-14">درخواست‌های امروز</span>
                  <strong class="fs-24 text-primary">۰</strong>
                </div>
              </div>
              <div class="col-sm-4 mb-3">
                <div class="p-3 rounded bgl-success text-center">
                  <span class="d-block fs-14">عملیات موفق</span>
                  <strong class="fs-24 text-success">۰</strong>
                </div>
              </div>
              <div class="col-sm-4 mb-3">
                <div class="p-3 rounded bgl-warning text-center">
                  <span class="d-block fs-14">پیام جدید</span>
                  <strong class="fs-24 text-warning">۰</strong>
                </div>
              </div>
            </div>
           
          </div>
        </div>
      </div>

      <div class="col-xl-4 col-lg-5">
        <div class="card">
          <div class="card-header border-0 pb-0">
            <h4 class="fs-20 text-black mb-0">اطلاعات نشست</h4>
          </div>
          <div class="card-body">
            <div class="activity-row d-flex align-items-center justify-content-between">
              <span>ایمیل</span>
              <strong class="text-black">{{ session?.email }}</strong>
            </div>
            <div class="activity-row d-flex align-items-center justify-content-between">
              <span>ورود</span>
              <span class="badge light badge-success">موفق</span>
            </div>
            <div class="activity-row d-flex align-items-center justify-content-between">
              <span>اعتبار توکن تا</span>
              <strong class="text-black">{{ session ? new Date(session.accessTokenExpiresAtUtc).toLocaleString('fa-IR') : '—' }}</strong>
            </div>
            <button class="btn btn-primary btn-block mt-3" type="button" @click="logout">خروج از حساب</button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
