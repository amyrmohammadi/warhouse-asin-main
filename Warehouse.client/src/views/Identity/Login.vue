<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'

import AuthThemeSwitch from '@/components/AuthThemeSwitch.vue'
import { login } from '@/services/auth'
import { toast } from '@/services/toast'

defineOptions({ name: 'LoginView' })

const route = useRoute()
const router = useRouter()

const email = ref('')
const password = ref('')
const remember = ref(false)
const loading = ref(false)
const errorMessages = ref<string[]>([])

const redirectPath = computed(() => {
  const requestedPath = route.query.redirect
  return typeof requestedPath === 'string' && requestedPath.startsWith('/')
    ? requestedPath
    : '/dashboard'
})

async function submit(): Promise<void> {
  errorMessages.value = []

  if (!email.value || !password.value) {
    errorMessages.value = ['ایمیل و رمز عبور الزامی است.']
    return
  }

  loading.value = true
  try {
    await login({ email: email.value, password: password.value }, remember.value)
    await router.replace(redirectPath.value)
  } catch (error) {
    const messages = error instanceof Error ? error.message.split('\n') : ['خطای ناشناخته‌ای رخ داد.']
    const translatedMessages = messages.map((message) =>
      message === 'The account is awaiting administrator approval.'
        ? 'حساب شما هنوز توسط مدیر تأیید نشده است.'
        : message,
    )
    toast.error(translatedMessages.join(' '), 'ورود ناموفق')
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <main class="authincation login-page h-100" dir="rtl">
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
                  <h4 class="text-center mb-4 text-white">وارد حساب خود شوید</h4>

                  <div v-if="errorMessages.length" class="auth-message auth-message--error" role="alert">
                    <ul>
                      <li v-for="message in errorMessages" :key="message">{{ message }}</li>
                    </ul>
                  </div>

                  <form @submit.prevent="submit">
                    <div class="form-group">
                      <label class="mb-1 text-white" for="login-email"><strong>ایمیل</strong></label>
                      <input
                        id="login-email"
                        v-model.trim="email"
                        type="email"
                        class="form-control"
                        dir="ltr"
                        autocomplete="username"
                        placeholder="hello@example.com"
                        required
                      />
                    </div>
                    <div class="form-group">
                      <label class="mb-1 text-white" for="login-password"><strong>رمز عبور</strong></label>
                      <input
                        id="login-password"
                        v-model="password"
                        type="password"
                        class="form-control"
                        dir="ltr"
                        autocomplete="current-password"
                        placeholder="رمز عبور"
                        required
                      />
                    </div>
                    <div class="form-row d-flex justify-content-between mt-4 mb-2">
                      <div class="form-group">
                        <div class="custom-control custom-checkbox ml-1 text-white">
                          <input id="remember-me" v-model="remember" type="checkbox" class="custom-control-input" />
                          <label class="custom-control-label" for="remember-me">مرا به خاطر بسپار</label>
                        </div>
                      </div>
                    </div>
                    <div class="text-center">
                      <button type="submit" class="btn bg-white text-primary btn-block" :disabled="loading">
                        {{ loading ? 'در حال ورود...' : 'وارد شوید' }}
                      </button>
                    </div>
                  </form>
                  <div class="new-account mt-3">
                    <p class="text-white">
                      حساب ندارید؟
                      <RouterLink class="text-white" to="/register">ثبت‌نام</RouterLink>
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
