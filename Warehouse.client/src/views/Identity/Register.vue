<script setup lang="ts">
import { ref } from 'vue'

import AuthThemeSwitch from '@/components/AuthThemeSwitch.vue'
import { register } from '@/services/auth'
import { toast } from '@/services/toast'

defineOptions({ name: 'RegisterView' })

const fullName = ref('')
const phoneNumber = ref('')
const userName = ref('')
const email = ref('')
const password = ref('')
const confirmPassword = ref('')
const loading = ref(false)
const registered = ref(false)
const errorMessages = ref<string[]>([])

async function submit(): Promise<void> {
  errorMessages.value = []

  if (!fullName.value || !phoneNumber.value || !email.value || !password.value) {
    errorMessages.value = ['نام کامل، شماره تلفن، ایمیل و رمز عبور الزامی است.']
    return
  }

  if (password.value.length < 8) {
    errorMessages.value = ['رمز عبور باید حداقل ۸ نویسه باشد.']
    return
  }

  if (password.value !== confirmPassword.value) {
    errorMessages.value = ['تکرار رمز عبور با رمز عبور یکسان نیست.']
    return
  }

  loading.value = true
  try {
    await register({
      email: email.value,
      password: password.value,
      fullName: fullName.value,
      phoneNumber: phoneNumber.value,
      userName: userName.value || undefined,
    })
    registered.value = true
    toast.success('ثبت‌نام شما انجام شد. پس از تأیید مدیر می‌توانید وارد حساب شوید.')
  } catch (error) {
    toast.error(error instanceof Error ? error.message : 'ثبت‌نام انجام نشد.')
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <main class="authincation h-100" dir="rtl">
    <AuthThemeSwitch />
    <div class="container h-100">
      <div class="row justify-content-center h-100 align-items-center">
        <div class="col-md-6">
          <div class="authincation-content">
            <div class="row no-gutters">
              <div class="col-xl-12">
                <div class="auth-form">
                  <div class="text-center mb-3">
                    <RouterLink to="/" aria-label="صفحه اصلی">
                      <img src="/mophy/images/logo-full.png" alt="لوگو" />
                    </RouterLink>
                  </div>
                  <h4 class="text-center mb-4 text-white">حساب خود را ثبت کنید</h4>

                  <div v-if="errorMessages.length" class="auth-message auth-message--error" role="alert">
                    <ul>
                      <li v-for="message in errorMessages" :key="message">{{ message }}</li>
                    </ul>
                  </div>

                  <form v-if="!registered" @submit.prevent="submit">
                    <div class="form-group">
                      <label class="mb-1 text-white" for="register-full-name"><strong>نام و نام خانوادگی</strong></label>
                      <input
                        id="register-full-name"
                        v-model.trim="fullName"
                        type="text"
                        class="form-control"
                        autocomplete="name"
                        placeholder="نام و نام خانوادگی"
                        maxlength="200"
                        required
                      />
                    </div>
                    <div class="form-group">
                      <label class="mb-1 text-white" for="register-phone-number"><strong>شماره تلفن</strong></label>
                      <input
                        id="register-phone-number"
                        v-model.trim="phoneNumber"
                        type="tel"
                        class="form-control"
                        dir="ltr"
                        autocomplete="tel"
                        placeholder="09123456789"
                        maxlength="32"
                        required
                      />
                    </div>
                    <div class="form-group">
                      <label class="mb-1 text-white" for="register-user-name"><strong>نام کاربری</strong></label>
                      <input
                        id="register-user-name"
                        v-model.trim="userName"
                        type="text"
                        class="form-control"
                        autocomplete="username"
                        placeholder="نام کاربری (اختیاری)"
                        maxlength="256"
                      />
                    </div>
                    <div class="form-group">
                      <label class="mb-1 text-white" for="register-email"><strong>ایمیل</strong></label>
                      <input
                        id="register-email"
                        v-model.trim="email"
                        type="email"
                        class="form-control"
                        dir="ltr"
                        autocomplete="email"
                        placeholder="hello@example.com"
                        required
                      />
                    </div>
                    <div class="form-group">
                      <label class="mb-1 text-white" for="register-password"><strong>رمز عبور</strong></label>
                      <input
                        id="register-password"
                        v-model="password"
                        type="password"
                        class="form-control"
                        dir="ltr"
                        autocomplete="new-password"
                        placeholder="حداقل ۸ نویسه"
                        minlength="8"
                        maxlength="128"
                        required
                      />
                    </div>
                    <div class="form-group">
                      <label class="mb-1 text-white" for="confirm-password"><strong>تکرار رمز عبور</strong></label>
                      <input
                        id="confirm-password"
                        v-model="confirmPassword"
                        type="password"
                        class="form-control"
                        dir="ltr"
                        autocomplete="new-password"
                        placeholder="تکرار رمز عبور"
                        required
                      />
                    </div>
                    <div class="text-center mt-4">
                      <button type="submit" class="btn bg-white text-primary btn-block" :disabled="loading">
                        {{ loading ? 'در حال ثبت‌نام...' : 'ثبت‌نام کنید' }}
                      </button>
                    </div>
                  </form>
                  <div class="new-account mt-3">
                    <p class="text-white">
                      {{ registered ? 'حساب شما در انتظار تأیید مدیر است.' : 'قبلاً حساب دارید؟' }}
                      <RouterLink class="text-white" to="/login">ورود</RouterLink>
                    </p>
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
