<script setup lang="ts">
import { computed } from 'vue'

import { hasValidSession } from '@/services/auth'

defineOptions({ name: 'HomeView' })

const isAuthenticated = computed(() => hasValidSession())
</script>
<template>
  <main class="authincation h-100" dir="rtl">
    <div class="container h-100">
      <div class="row justify-content-center h-100 align-items-center">
        <div class="col-lg-7 col-md-9">
          <div class="authincation-content">
            <div class="row no-gutters">
              <div class="col-xl-12">
                <div class="auth-form text-center">
                  <div class="mb-4">
                    <img src="/mophy/images/logo-full.png" alt="لوگوی Mophy" />
                  </div>
                  <h2 class="text-white font-w600 mb-3">به پنل مدیریت خوش آمدید</h2>
                  <p class="text-white mb-4">
                    رابط کاربری Vue بر پایه اجزای قالب Mophy و متصل به سرویس احراز هویت سرور است.
                  </p>

                  <RouterLink
                    v-if="isAuthenticated"
                    class="btn bg-white text-primary btn-block"
                    to="/dashboard"
                  >
                    ورود به داشبورد
                  </RouterLink>
                  <div v-else class="row">
                    <div class="col-sm-6 mb-3 mb-sm-0">
                      <RouterLink class="btn bg-white text-primary btn-block" to="/login">
                        ورود
                      </RouterLink>
                    </div>
                    <div class="col-sm-6">
                      <RouterLink class="btn btn-outline-light btn-block" to="/register">
                        ثبت‌نام
                      </RouterLink>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  </main>
</template>
