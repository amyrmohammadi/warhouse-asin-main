<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'

import { clearSession } from '@/services/auth'
import {
  changeCurrentUserPassword,
  getCurrentUserProfile,
  getProfileImageDataUrl,
  ProfileApiError,
  profileUpdatedEvent,
  updateCurrentUserProfile,
  type CurrentUserProfile,
} from '@/services/profile'
import { toast } from '@/services/toast'

defineOptions({ name: 'AccountProfileView' })

const maximumImageSize = 2 * 1024 * 1024
const supportedImageTypes = ['image/jpeg', 'image/png', 'image/webp']
const defaultAvatar = '/mophy/images/user-icon.png'

const route = useRoute()
const router = useRouter()
const profile = ref<CurrentUserProfile | null>(null)
const fullName = ref('')
const phoneNumber = ref('')
const selectedImage = ref<File | null>(null)
const removeProfileImage = ref(false)
const selectedImageUrl = ref<string | null>(null)
const loading = ref(true)
const savingProfile = ref(false)
const changingPassword = ref(false)
const profileError = ref('')
const passwordError = ref('')
const currentPassword = ref('')
const newPassword = ref('')
const confirmPassword = ref('')
let activeRequest: AbortController | null = null

const profileImageUrl = computed(() => {
  if (removeProfileImage.value) return defaultAvatar
  return selectedImageUrl.value ?? getProfileImageDataUrl(profile.value) ?? defaultAvatar
})

function translateError(error: unknown, fallback: string): string {
  if (!(error instanceof Error)) return fallback
  const translations: Record<string, string> = {
    'Incorrect password.': 'رمز عبور فعلی صحیح نیست.',
    'The new password must be different from the current password.':
      'رمز عبور جدید باید با رمز فعلی متفاوت باشد.',
    'Passwords must have at least one non alphanumeric character.':
      'رمز عبور باید حداقل یک نویسه ویژه داشته باشد.',
    'Passwords must have at least one digit (\'0\'-\'9\').':
      'رمز عبور باید حداقل یک عدد داشته باشد.',
    'Passwords must have at least one uppercase (\'A\'-\'Z\').':
      'رمز عبور باید حداقل یک حرف بزرگ انگلیسی داشته باشد.',
    'Passwords must have at least one lowercase (\'a\'-\'z\').':
      'رمز عبور باید حداقل یک حرف کوچک انگلیسی داشته باشد.',
  }
  return translations[error.message] ?? error.message ?? fallback
}

async function loadProfile(): Promise<void> {
  activeRequest?.abort()
  activeRequest = new AbortController()
  loading.value = true
  profileError.value = ''

  try {
    const result = await getCurrentUserProfile(activeRequest.signal)
    profile.value = result
    fullName.value = result.fullName
    phoneNumber.value = result.phoneNumber
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') return
    if (error instanceof ProfileApiError && error.status === 401) {
      await router.replace({ name: 'login', query: { redirect: route.fullPath } })
      return
    }
    profileError.value = translateError(error, 'دریافت اطلاعات حساب کاربری انجام نشد.')
  } finally {
    loading.value = false
  }
}

function selectImage(event: Event): void {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0] ?? null
  profileError.value = ''

  if (!file) return
  if (!supportedImageTypes.includes(file.type)) {
    profileError.value = 'فرمت تصویر باید JPEG، PNG یا WebP باشد.'
    input.value = ''
    return
  }
  if (file.size > maximumImageSize) {
    profileError.value = 'حجم تصویر نباید بیشتر از ۲ مگابایت باشد.'
    input.value = ''
    return
  }

  if (selectedImageUrl.value) URL.revokeObjectURL(selectedImageUrl.value)
  selectedImage.value = file
  selectedImageUrl.value = URL.createObjectURL(file)
  removeProfileImage.value = false
}

function removeImage(): void {
  if (selectedImageUrl.value) URL.revokeObjectURL(selectedImageUrl.value)
  selectedImage.value = null
  selectedImageUrl.value = null
  removeProfileImage.value = true
}

async function saveProfile(): Promise<void> {
  if (savingProfile.value) return
  profileError.value = ''

  if (!fullName.value.trim() || !phoneNumber.value.trim()) {
    profileError.value = 'نام کامل و شماره تلفن الزامی است.'
    return
  }

  activeRequest?.abort()
  activeRequest = new AbortController()
  savingProfile.value = true
  try {
    await updateCurrentUserProfile(
      {
        fullName: fullName.value.trim(),
        phoneNumber: phoneNumber.value.trim(),
        profileImage: selectedImage.value,
        removeProfileImage: removeProfileImage.value,
      },
      activeRequest.signal,
    )
    await loadProfile()
    selectedImage.value = null
    if (selectedImageUrl.value) URL.revokeObjectURL(selectedImageUrl.value)
    selectedImageUrl.value = null
    removeProfileImage.value = false
    toast.success('اطلاعات حساب کاربری با موفقیت ذخیره شد.')
    window.dispatchEvent(new CustomEvent(profileUpdatedEvent))
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') return
    if (error instanceof ProfileApiError && error.status === 401) {
      await router.replace({ name: 'login', query: { redirect: route.fullPath } })
      return
    }
    toast.error(translateError(error, 'ویرایش حساب کاربری انجام نشد.'))
  } finally {
    savingProfile.value = false
  }
}

async function savePassword(): Promise<void> {
  if (changingPassword.value) return
  passwordError.value = ''

  if (!currentPassword.value || !newPassword.value || !confirmPassword.value) {
    passwordError.value = 'تکمیل هر سه فیلد رمز عبور الزامی است.'
    return
  }
  if (newPassword.value !== confirmPassword.value) {
    passwordError.value = 'تکرار رمز عبور جدید مطابقت ندارد.'
    return
  }

  activeRequest?.abort()
  activeRequest = new AbortController()
  changingPassword.value = true
  try {
    await changeCurrentUserPassword(
      currentPassword.value,
      newPassword.value,
      activeRequest.signal,
    )
    toast.success('رمز عبور با موفقیت تغییر کرد. لطفاً با رمز جدید وارد شوید.')
    clearSession()
    await router.replace({ name: 'login' })
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') return
    if (error instanceof ProfileApiError && error.status === 401) {
      await router.replace({ name: 'login', query: { redirect: route.fullPath } })
      return
    }
    toast.error(translateError(error, 'تغییر رمز عبور انجام نشد.'))
  } finally {
    changingPassword.value = false
  }
}

onMounted(loadProfile)
onBeforeUnmount(() => {
  activeRequest?.abort()
  if (selectedImageUrl.value) URL.revokeObjectURL(selectedImageUrl.value)
})
</script>

<template>
  <div class="container-fluid account-profile-page">
    <div class="page-titles">
      <ol class="breadcrumb">
        <li class="breadcrumb-item"><RouterLink to="/dashboard">داشبورد</RouterLink></li>
        <li class="breadcrumb-item active"><span>ویرایش حساب کاربری</span></li>
      </ol>
    </div>

    <div v-if="loading" class="card">
      <div class="card-body text-center py-5 text-muted">
        <span class="spinner-border spinner-border-sm ml-2" role="status"></span>
        در حال دریافت اطلاعات حساب کاربری...
      </div>
    </div>

    <div v-else class="row">
      <div class="col-xl-7 mb-4">
        <form class="card h-100" @submit.prevent="saveProfile">
          <div class="card-header">
            <div>
              <h4 class="card-title mb-1">
                <i class="las la-user-edit ml-1"></i>
                ویرایش حساب کاربری
              </h4>            
            </div>
          </div>
          <div class="card-body">
            <div v-if="profileError" class="alert alert-danger" role="alert">{{ profileError }}</div>

            <div class="account-profile-page__image-row">
              <img :src="profileImageUrl" class="account-profile-page__avatar" alt="تصویر کاربر" />
              <div>
                <label class="btn btn-outline-primary btn-sm mb-2" for="profile-image">
                  <i class="las la-camera ml-1"></i>انتخاب تصویر
                </label>
                <input
                  id="profile-image"
                  class="sr-only"
                  type="file"
                  accept="image/jpeg,image/png,image/webp"
                  @change="selectImage"
                />
                <button class="btn btn-link btn-sm text-danger mb-2" type="button" @click="removeImage">
                  حذف تصویر
                </button>
                <small class="d-block text-muted">JPEG، PNG یا WebP؛ حداکثر ۲ مگابایت</small>
              </div>
            </div>

            <div class="row">
              <div class="col-md-6 form-group">
                <label for="profile-username">نام کاربری</label>
                <input id="profile-username" :value="profile?.userName" class="form-control" disabled />
              </div>
              <div class="col-md-6 form-group">
                <label for="profile-email">ایمیل</label>
                <input id="profile-email" :value="profile?.email" class="form-control" dir="ltr" disabled />
              </div>
              <div class="col-md-6 form-group">
                <label for="profile-full-name">نام و نام خانوادگی</label>
                <input
                  id="profile-full-name"
                  v-model.trim="fullName"
                  class="form-control"
                  maxlength="200"
                  autocomplete="name"
                  required
                />
              </div>
              <div class="col-md-6 form-group">
                <label for="profile-phone">شماره تلفن</label>
                <input
                  id="profile-phone"
                  v-model.trim="phoneNumber"
                  class="form-control"
                  dir="ltr"
                  maxlength="32"
                  autocomplete="tel"
                  required
                />
              </div>
            </div>
          </div>
          <div class="card-footer d-flex justify-content-end">
            <button class="btn btn-primary" type="submit" :disabled="savingProfile">
              <span v-if="savingProfile" class="spinner-border spinner-border-sm ml-2"></span>
              <i v-else class="las la-save ml-1"></i>
              ذخیره اطلاعات
            </button>
          </div>
        </form>
      </div>

      <div class="col-xl-5 mb-4">
        <form class="card h-100" @submit.prevent="savePassword">
          <div class="card-header">
            <div>
              <h4 class="card-title mb-1">
                <i class="las la-key ml-1"></i>
                تغییر رمز عبور
              </h4>
             
            </div>
          </div>
          <div class="card-body">
            <div v-if="passwordError" class="alert alert-danger" role="alert">{{ passwordError }}</div>
            <div class="form-group">
              <label for="current-password">رمز عبور فعلی</label>
              <input
                id="current-password"
                v-model="currentPassword"
                class="form-control"
                type="password"
                dir="ltr"
                maxlength="128"
                autocomplete="current-password"
                required
              />
            </div>
            <div class="form-group">
              <label for="new-password">رمز عبور جدید</label>
              <input
                id="new-password"
                v-model="newPassword"
                class="form-control"
                type="password"
                dir="ltr"
                minlength="8"
                maxlength="128"
                autocomplete="new-password"
                required
              />
              <small class="form-text text-muted">حداقل ۸ کاراکتر شامل حروف بزرگ و کوچک، عدد و نویسه ویژه</small>
            </div>
            <div class="form-group mb-0">
              <label for="confirm-password">تکرار رمز عبور جدید</label>
              <input
                id="confirm-password"
                v-model="confirmPassword"
                class="form-control"
                type="password"
                dir="ltr"
                minlength="8"
                maxlength="128"
                autocomplete="new-password"
                required
              />
            </div>
          </div>
          <div class="card-footer d-flex justify-content-end">
            <button class="btn btn-primary" type="submit" :disabled="changingPassword">
              <span v-if="changingPassword" class="spinner-border spinner-border-sm ml-2"></span>
              <i v-else class="las la-key ml-1"></i>
              تغییر رمز عبور
            </button>
          </div>
        </form>
      </div>
    </div>
  </div>
</template>

<style scoped>
.account-profile-page__image-row {
  align-items: center;
  display: flex;
  gap: 1rem;
  margin-bottom: 2rem;
}

.account-profile-page__avatar {
  border: 3px solid var(--border-color);
  border-radius: 50%;
  height: 6rem;
  object-fit: cover;
  width: 6rem;
}

@media (max-width: 575.98px) {
  .account-profile-page__image-row {
    align-items: flex-start;
  }

  .account-profile-page__avatar {
    height: 5rem;
    width: 5rem;
  }
}
</style>
