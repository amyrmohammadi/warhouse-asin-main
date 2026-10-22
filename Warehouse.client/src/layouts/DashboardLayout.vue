
<script setup lang="ts">
  import {
    computed,
    onBeforeUnmount,
    onMounted,
    ref,
    watch,
  } from 'vue'

  import { useRoute, useRouter } from 'vue-router'

  import { clearSession, getSession } from '@/services/auth'

  import {
    getCurrentUserProfile,
    getProfileImageDataUrl,
    ProfileApiError,
    profileUpdatedEvent,
    type CurrentUserProfile,
  } from '@/services/profile'

  import {
    getStoredTheme,
    storeTheme,
    type ColorTheme,
  } from '@/services/theme'

  defineOptions({ name: 'DashboardLayout' })

  // ---------------------------------------------
  // Router
  // ---------------------------------------------

  const route = useRoute()
  const router = useRouter()

  // ---------------------------------------------
  // Sidebar
  // ---------------------------------------------

  const menuCollapsed = ref(false)

  type SidebarGroup =
    | 'masterData'
    | 'users'
    | null

  function getSidebarGroup(path: string): SidebarGroup {
    if (path.startsWith('/dashboard/master-data/')) {
      return 'masterData'
    }

    if (
      path === '/dashboard/users' ||
      path.startsWith('/dashboard/users/') ||
      path === '/dashboard/roles' ||
      path.startsWith('/dashboard/roles/')
    ) {
      return 'users'
    }

    return null
  }

  const openSidebarGroup = ref<SidebarGroup>(
    getSidebarGroup(route.path)
  )

  function toggleSidebarGroup(
    event: MouseEvent,
    group: Exclude<SidebarGroup, null>
  ): void {
    event.preventDefault()
    event.stopPropagation()

    // In collapsed desktop mode the submenu is
    // displayed using CSS hover / focus.
    if (isDesktopMini.value) {
      return
    }

    openSidebarGroup.value =
      openSidebarGroup.value === group
        ? null
        : group
  }
  function isSubmenuActive(path: string): boolean {
    return (
      route.path === path ||
      route.path.startsWith(`${path}/`)
    )
  }
  // ---------------------------------------------
  // Authentication
  // ---------------------------------------------

  const session = ref(getSession())

  const currentProfile = ref<CurrentUserProfile | null>(null)

  const profileMenu = ref<HTMLElement | null>(null)

  const activeProfileMenu = ref<'header' | null>(null)

  const canReadUsers = computed(
    () => session.value?.permissions.includes('UsersRead') ?? false
  )

  const canReadRoles = computed(
    () => session.value?.permissions.includes('RolesRead') ?? false
  )

  const canReadAuditLogs = computed(
    () => session.value?.permissions.includes('AuditLogsRead') ?? false
  )

  const canReadMasterData = computed(
    () => session.value?.permissions.includes('MasterDataRead') ?? false
  )

  const canReadGoodsReceipts = computed(
    () => session.value?.permissions.includes('GoodsReceiptsRead') ?? false
  )

  const canReadUserManagement = computed(
    () => canReadUsers.value || canReadRoles.value
  )

  const displayName = computed(
    () =>
      currentProfile.value?.fullName ||
      session.value?.email?.split('@')[0] ||
      'کاربر'
  )

  const profileImageUrl = computed(
    () =>
      getProfileImageDataUrl(currentProfile.value) ??
      '/mophy/images/user-icon.png'
  )

  // ---------------------------------------------
  // Theme
  // ---------------------------------------------

  const colorTheme = ref<ColorTheme>(getStoredTheme())

  const isDarkTheme = computed(
    () => colorTheme.value === 'dark'
  )

  function toggleTheme(): void {
    colorTheme.value = isDarkTheme.value
      ? 'light'
      : 'dark'

    storeTheme(colorTheme.value)
  }

  // ---------------------------------------------
  // Responsive
  // ---------------------------------------------

  const mobileMediaQuery = window.matchMedia(
    '(max-width: 767px)'
  )

  const isMobile = ref(mobileMediaQuery.matches)

  const sidebarStyle = computed(
    () => isMobile.value ? 'overlay' : 'full'
  )

  const isDesktopMini = computed(
    () => menuCollapsed.value && !isMobile.value
  )

  const sidebarToggleLabel = computed(() => {
    if (isMobile.value) {
      return menuCollapsed.value
        ? 'بستن منو'
        : 'نمایش منو'
    }

    return menuCollapsed.value
      ? 'باز کردن منو'
      : 'کوچک کردن منو'
  })

  // ---------------------------------------------
  // Body attributes
  // ---------------------------------------------

  const bodyLayoutAttributes = {
    'data-theme-version': 'light',
    'data-layout': 'vertical',
    'data-sidebar-position': 'fixed',
    'data-header-position': 'fixed',
    'data-container': 'wide',
    direction: 'rtl',
  } as const

  // ---------------------------------------------
  // Lifecycle
  // ---------------------------------------------

  onMounted(() => {
    for (const [name, value] of Object.entries(
      bodyLayoutAttributes
    )) {
      document.body.setAttribute(name, value)
    }

    document.body.setAttribute(
      'data-sidebar-style',
      sidebarStyle.value
    )

    mobileMediaQuery.addEventListener(
      'change',
      handleMobileModeChange
    )

    document.addEventListener(
      'click',
      handleDocumentClick
    )

    document.addEventListener(
      'keydown',
      handleDocumentKeydown
    )

    window.addEventListener(
      profileUpdatedEvent,
      handleProfileUpdated
    )

    storeTheme(colorTheme.value)

    void loadCurrentProfile()
  })

  onBeforeUnmount(() => {
    mobileMediaQuery.removeEventListener(
      'change',
      handleMobileModeChange
    )

    document.removeEventListener(
      'click',
      handleDocumentClick
    )

    document.removeEventListener(
      'keydown',
      handleDocumentKeydown
    )

    window.removeEventListener(
      profileUpdatedEvent,
      handleProfileUpdated
    )

    for (const [name, value] of Object.entries(
      bodyLayoutAttributes
    )) {
      if (document.body.getAttribute(name) === value) {
        document.body.removeAttribute(name)
      }
    }

    if (
      document.body.getAttribute('data-sidebar-style') ===
      sidebarStyle.value
    ) {
      document.body.removeAttribute(
        'data-sidebar-style'
      )
    }
  })

  // ---------------------------------------------
  // Watchers
  // ---------------------------------------------

  watch(
    () => route.fullPath,
    () => {
      session.value = getSession()
      activeProfileMenu.value = null

      openSidebarGroup.value = getSidebarGroup(
        route.path
      )

      if (isMobile.value) {
        menuCollapsed.value = false
      }
    }
  )

  watch(sidebarStyle, (value) => {
    document.body.setAttribute(
      'data-sidebar-style',
      value
    )
  })

  // ---------------------------------------------
  // Actions
  // ---------------------------------------------

  async function logout(): Promise<void> {
    activeProfileMenu.value = null

    clearSession()

    await router.replace('/login')
  }

  async function loadCurrentProfile(): Promise<void> {
    if (!session.value?.accessToken) {
      return
    }

    try {
      currentProfile.value =
        await getCurrentUserProfile()
    } catch (error) {
      if (
        error instanceof ProfileApiError &&
        error.status === 401
      ) {
        await logout()
      }
    }
  }

  function handleProfileUpdated(): void {
    void loadCurrentProfile()
  }

  function toggleProfileMenu(): void {
    activeProfileMenu.value =
      activeProfileMenu.value === 'header'
        ? null
        : 'header'
  }

  function handleDocumentClick(
    event: MouseEvent
  ): void {
    if (!activeProfileMenu.value) {
      return
    }

    const target = event.target

    if (
      target instanceof Node &&
      !profileMenu.value?.contains(target)
    ) {
      activeProfileMenu.value = null
    }
  }

  function handleDocumentKeydown(
    event: KeyboardEvent
  ): void {
    if (event.key === 'Escape') {
      activeProfileMenu.value = null
    }
  }

  function handleMobileModeChange(
    event: MediaQueryListEvent
  ): void {
    isMobile.value = event.matches
    menuCollapsed.value = false
    activeProfileMenu.value = null
  }
</script>

<template>
  <main id="main-wrapper"
        class="dashboard-shell show"
        :class="{
      'menu-toggle': menuCollapsed,
      'sidebar-mini-flyout': isDesktopMini
    }"
        data-theme-version="light"
        data-layout="vertical"
        :data-sidebar-style="sidebarStyle"
        data-sidebar-position="fixed"
        data-header-position="fixed"
        data-container="wide"
        :data-app-theme="colorTheme"
        direction="rtl">

    <!-- =====================================
         Header Logo
    ====================================== -->

    <div class="nav-header">

      <RouterLink to="/dashboard"
                  class="brand-logo">
        <img class="logo-abbr"
             src="/mophy/images/logo.png"
             alt="لوگو" />

        <img class="logo-compact"
             src="/mophy/images/logo-text.png"
             alt="نام سامانه" />

        <img class="brand-title"
             src="/mophy/images/logo-text.png"
             alt="نام سامانه" />
      </RouterLink>

      <div class="nav-control">

        <button class="hamburger menu-action"
                :class="{
            'is-active': menuCollapsed
          }"
                type="button"
                :aria-label="sidebarToggleLabel"
                :aria-expanded="
            isMobile ? menuCollapsed : undefined
          "
                @click="
            menuCollapsed = !menuCollapsed
          ">
          <span class="line"></span>
          <span class="line"></span>
          <span class="line"></span>
        </button>

      </div>
    </div>

    <!-- =====================================
         Top Header
    ====================================== -->

    <div class="header">
      <div class="header-content">

        <nav class="navbar navbar-expand">

          <div class="collapse navbar-collapse justify-content-between">

            <div class="header-left"></div>

            <ul class="navbar-nav header-right">

              <!-- Theme -->

              <li class="nav-item dropdown notification_dropdown">
                <button class="nav-link bell bell-link theme-switch"
                        type="button"
                        :aria-label="
                    isDarkTheme
                      ? 'فعال‌سازی تم روشن'
                      : 'فعال‌سازی تم تاریک'
                  "
                        :aria-pressed="isDarkTheme"
                        :title="
                    isDarkTheme
                      ? 'تم روشن'
                      : 'تم تاریک'
                  "
                        @click="toggleTheme">
                  <i :class="
                      isDarkTheme
                        ? 'las la-sun'
                        : 'las la-moon'
                    "></i>
                </button>
              </li>

              <!-- Profile -->

              <li ref="profileMenu"
                  class="nav-item dropdown header-profile"
                  :class="{
                  show: activeProfileMenu === 'header'
                }">

                <button class="nav-link d-flex profile-menu-trigger"
                        type="button"
                        aria-haspopup="true"
                        :aria-expanded="
                    activeProfileMenu === 'header'
                  "
                        @click.stop="toggleProfileMenu">

                  <div class="header-info d-flex align-items-center">
                    <span class="text-black">
                      <strong>
                        {{ displayName }}
                      </strong>
                    </span>
                  </div>

                  <img :src="profileImageUrl"
                       width="20"
                       alt="تصویر کاربر" />

                </button>

                <div class="dropdown-menu dropdown-menu-right"
                     :class="{
                    show: activeProfileMenu === 'header'
                  }"
                     role="menu">

                  <RouterLink class="dropdown-item ai-icon"
                              to="/dashboard/account"
                              role="menuitem"
                              @click="
                      activeProfileMenu = null
                    ">
                    <i class="las la-user-edit text-primary"></i>

                    <span class="mr-2">
                      ویرایش حساب کاربری
                    </span>
                  </RouterLink>

                  <button class="dropdown-item ai-icon"
                          type="button"
                          role="menuitem"
                          @click="logout">
                    <i class="las la-sign-out-alt text-danger"></i>

                    <span class="mr-2">
                      خروج از حساب
                    </span>
                  </button>

                </div>
              </li>

            </ul>
          </div>
        </nav>
      </div>
    </div>

    <!-- =====================================
         Sidebar
    ====================================== -->

    <aside class="deznav">

      <div class="deznav-scroll">

        <ul id="vue-sidebar-menu"
            class="metismenu sidebar-menu-vue">

          <!-- Dashboard -->

          <li>
            <RouterLink class="ai-icon"
                        to="/dashboard"
                        aria-expanded="false">
              <i class="flaticon-381-networking"></i>

              <span class="nav-text">
                داشبورد
              </span>
            </RouterLink>
          </li>

          <!-- =================================
               Master Data
          ================================== -->

          <li v-if="canReadGoodsReceipts">
            <RouterLink class="ai-icon"
                        to="/dashboard/inventory/goods-receipts"
                        aria-expanded="false">
              <i class="las la-dolly-flatbed"></i>
              <span class="nav-text">ورود کالا</span>
            </RouterLink>
          </li>

          <li v-if="canReadMasterData"
              class="sidebar-group-item"
              :class="{
              'mm-active':
                openSidebarGroup === 'masterData'
            }">

            <a class="has-arrow ai-icon sidebar-group-trigger"
               href="#"
               role="button"
               :aria-expanded="
                openSidebarGroup === 'masterData'
              "
               @click.capture="
                toggleSidebarGroup(
                  $event,
                  'masterData'
                )
              ">

              <!-- Main Icon -->

              <i class="las la-layer-group sidebar-main-icon"></i>

              <!-- Title -->

              <span class="nav-text">
                تعریف مفروضات
              </span>

              <!-- Chevron SVG -->

              <svg class="sidebar-chevron"
                   viewBox="0 0 24 24"
                   width="16"
                   height="16"
                   fill="none"
                   aria-hidden="true"
                   focusable="false">
                <path d="M6 9L12 15L18 9"
                      stroke="currentColor"
                      stroke-width="2.2"
                      stroke-linecap="round"
                      stroke-linejoin="round" />
              </svg>

            </a>

            <!-- Submenu -->

            <ul class="mm-collapse sidebar-submenu"
                :class="{
                'mm-show':
                  openSidebarGroup === 'masterData'
              }"
                :aria-hidden="
                !isDesktopMini &&
                openSidebarGroup !== 'masterData'
              ">

              <li class="sidebar-flyout-heading"
                  aria-hidden="true">
                تعریف مفروضات
              </li>

              <li>
                <RouterLink to="/dashboard/master-data/products"
                            :class="{
                            'submenu-active': isSubmenuActive(
                            '/dashboard/master-data/products'
                              )
                }">
                  تعریف کالا
                </RouterLink>
              </li>

              <li>
                <RouterLink to="/dashboard/master-data/units-of-measure"
                            :class="{
                            'submenu-active': isSubmenuActive(
                            '/dashboard/master-data/units-of-measure'
                              )
                }">
                  واحد اندازه‌گیری
                </RouterLink>
              </li>

              <li>
                <RouterLink to="/dashboard/master-data/suppliers"
                            :class="{
                            'submenu-active': isSubmenuActive(
                            '/dashboard/master-data/suppliers'
                              )
                }">
                  تأمین‌کنندگان
                </RouterLink>
              </li>

              <li>
                <RouterLink to="/dashboard/master-data/warehouses"
                            :class="{
                            'submenu-active': isSubmenuActive(
                            '/dashboard/master-data/warehouses'
                              )
                }">
                  انبارها
                </RouterLink>
              </li>

              <li>
                <RouterLink to="/dashboard/master-data/people"
                            :class="{
                            'submenu-active': isSubmenuActive(
                            '/dashboard/master-data/people'
                              )
                }">
                  افراد
                </RouterLink>
              </li>

            </ul>
          </li>

          <!-- =================================
               User Management
          ================================== -->

          <li v-if="canReadUserManagement"
              class="sidebar-group-item"
              :class="{
              'mm-active':
                openSidebarGroup === 'users'
            }">

            <a class="has-arrow ai-icon sidebar-group-trigger"
               href="#"
               role="button"
               :aria-expanded="
                openSidebarGroup === 'users'
              "
               @click.capture="
                toggleSidebarGroup(
                  $event,
                  'users'
                )
              ">

              <!-- Main Icon -->

              <i class="las la-users-cog sidebar-main-icon"></i>

              <!-- Title -->

              <span class="nav-text">
                مدیریت کاربران
              </span>

              <!-- Chevron SVG -->

              <svg class="sidebar-chevron"
                   viewBox="0 0 24 24"
                   width="16"
                   height="16"
                   fill="none"
                   aria-hidden="true"
                   focusable="false">
                <path d="M6 9L12 15L18 9"
                      stroke="currentColor"
                      stroke-width="2.2"
                      stroke-linecap="round"
                      stroke-linejoin="round" />
              </svg>

            </a>

            <!-- Submenu -->

            <ul class="mm-collapse sidebar-submenu"
                :class="{
                'mm-show':
                  openSidebarGroup === 'users'
              }"
                :aria-hidden="
                !isDesktopMini &&
                openSidebarGroup !== 'users'
              ">

              <li class="sidebar-flyout-heading"
                  aria-hidden="true">
                مدیریت کاربران
              </li>

              <li v-if="canReadUsers">
                <RouterLink to="/dashboard/users"
                            :class="{
                    'submenu-active': isSubmenuActive('/dashboard/users')
                    }">
                  کاربران
                </RouterLink>
              </li>

              <li v-if="canReadRoles">
                <RouterLink to="/dashboard/roles"
                            :class="{
                    'submenu-active': isSubmenuActive('/dashboard/roles')
                }">
                  نقش‌ها
                </RouterLink>
              </li>

            </ul>
          </li>

          <!-- =================================
               Audit Logs
          ================================== -->

          <li v-if="canReadAuditLogs">

            <RouterLink class="ai-icon"
                        to="/dashboard/audit-logs"
                        aria-expanded="false">

              <i class="las la-history"></i>

              <span class="nav-text">
                گزارش فعالیت‌ها
              </span>

            </RouterLink>

          </li>

        </ul>

      </div>
    </aside>

    <!-- =====================================
         Mobile Backdrop
    ====================================== -->

    <button v-if="isMobile && menuCollapsed"
            class="sidebar-backdrop"
            type="button"
            aria-label="بستن منو"
            @click="
        menuCollapsed = false
      "></button>

    <!-- =====================================
         Page Content
    ====================================== -->

    <section class="content-body">
      <RouterView />
    </section>

    <!-- =====================================
         Footer
    ====================================== -->

    <footer class="footer">
      <div class="copyright">
        تمامی حقوق محفوظ است
      </div>
    </footer>

  </main>
</template>

<style scoped>

    /* ===========================================
     Base Sidebar
  =========================================== */

    .sidebar-menu-vue > li {
      position: relative;
    }

      /* ===========================================
     Group Trigger RTL
  =========================================== */

      .sidebar-menu-vue > li > .sidebar-group-trigger {
        display: flex !important;
        flex-direction: row !important;
        align-items: center !important;
        direction: rtl;
        text-align: right;
        gap: 12px;
        position: relative;
      }

    /* ===========================================
     Main Icon
  =========================================== */

    .sidebar-group-trigger .sidebar-main-icon {
      flex: 0 0 auto;
      display: inline-flex !important;
      align-items: center;
      justify-content: center;
      margin: 0 !important;
    }

    /* ===========================================
     Group Title
  =========================================== */

    .sidebar-group-trigger .nav-text {
      flex: 1 1 auto;
      min-width: 0;
      margin: 0 !important;
      text-align: right;
    }

    /* ===========================================
     Remove Default Mophy Arrow
  =========================================== */

    .sidebar-menu-vue > li > .sidebar-group-trigger.has-arrow::after {
      content: none !important;
      display: none !important;
    }

    /* ===========================================
     Custom Chevron
  =========================================== */

    .sidebar-menu-vue > li > .sidebar-group-trigger > .sidebar-chevron {
      display: block !important;
      flex: 0 0 16px;
      width: 16px !important;
      height: 16px !important;
      min-width: 16px !important;
      min-height: 16px !important;
      margin: 0 !important;
      padding: 0 !important;
      visibility: visible !important;
      opacity: 1 !important;
      background: transparent !important;
      background-image: none !important;
      border: none !important;
      border-radius: 0 !important;
      box-shadow: none !important;
      color: inherit;
      overflow: visible;
      transform: rotate(0deg);
      transition: transform 250ms ease, color 200ms ease;
    }

    /* Chevron Open State */

    .sidebar-menu-vue > li.mm-active > .sidebar-group-trigger > .sidebar-chevron {
      transform: rotate(180deg);
    }

    /* ===========================================
     Normal Submenu Animation
  =========================================== */

    /*
    Keep display:block for both states.
    Animate max-height instead of display.
  */

    .sidebar-menu-vue > li > .sidebar-submenu {
      display: block !important;
      height: auto !important;
      max-height: 0;
      overflow: hidden !important;
      opacity: 0;
      visibility: hidden;
      pointer-events: none;
      transform: translateY(-6px);
      transition: max-height 320ms cubic-bezier(0.22, 1, 0.36, 1), opacity 220ms ease, transform 280ms ease, visibility 0s linear 320ms;
    }

      /* Expanded submenu */

      .sidebar-menu-vue > li > .sidebar-submenu.mm-show {
        max-height: 500px;
        opacity: 1;
        visibility: visible;
        pointer-events: auto;
        transform: translateY(0);
        transition: max-height 350ms cubic-bezier(0.22, 1, 0.36, 1), opacity 220ms ease, transform 280ms ease, visibility 0s;
      }

    /* ===========================================
     Flyout Heading
  =========================================== */

    .sidebar-flyout-heading {
      display: none;
    }

    /* ===========================================
     Collapsed Sidebar - Desktop
  =========================================== */

    /* Allow flyout outside sidebar */

    .sidebar-mini-flyout :deep(.deznav),
    .sidebar-mini-flyout :deep(.deznav-scroll),
    .sidebar-mini-flyout :deep(.sidebar-menu-vue) {
      overflow: visible !important;
    }

    /* Each group is flyout anchor */

    .sidebar-mini-flyout
    :deep(.sidebar-menu-vue > li) {
      position: relative;
    }

    /* Center main icon */

    .sidebar-mini-flyout
    :deep( .sidebar-menu-vue > li > .sidebar-group-trigger ) {
      justify-content: center !important;
    }

    /* Hide labels and arrows in mini sidebar */

    .sidebar-mini-flyout
    :deep( .sidebar-group-trigger .nav-text ),
    .sidebar-mini-flyout
    :deep( .sidebar-group-trigger .sidebar-chevron ) {
      display: none !important;
    }

    .sidebar-mini-flyout
    :deep( .sidebar-group-trigger.has-arrow::after ) {
      content: none !important;
      display: none !important;
    }

    /* Main icon in mini sidebar */

    .sidebar-mini-flyout
    :deep( .sidebar-group-trigger > .sidebar-main-icon ) {
      margin: 0 !important;
    }

    /* ===========================================
     Mini Sidebar Main Icon Hover
  =========================================== */

    .sidebar-mini-flyout
    :deep( .sidebar-menu-vue > li:hover > .sidebar-group-trigger > .sidebar-main-icon ),
    .sidebar-mini-flyout
    :deep( .sidebar-menu-vue > li:focus-within > .sidebar-group-trigger > .sidebar-main-icon ) {
      background: var(--primary, #2563eb) !important;
      background-color: var(--primary, #2563eb) !important;
      color: #fff !important;
      border-color: var(--primary, #2563eb) !important;
    }

    /* Icon font color */

    .sidebar-mini-flyout
    :deep( .sidebar-menu-vue > li:hover > .sidebar-group-trigger > .sidebar-main-icon::before ),
    .sidebar-mini-flyout
    :deep( .sidebar-menu-vue > li:focus-within > .sidebar-group-trigger > .sidebar-main-icon::before ) {
      color: #fff !important;
    }

    /* ===========================================
     Mini Sidebar Flyout
  =========================================== */

    .sidebar-mini-flyout
    :deep( .sidebar-menu-vue > li > .sidebar-submenu ),
    .sidebar-mini-flyout
    :deep( .sidebar-menu-vue > li > .sidebar-submenu.mm-show ) {
      display: block !important;
      position: absolute !important;
      top: 0 !important;
      /* RTL: Open to the left */
      right: 100% !important;
      left: auto !important;
      width: 220px;
      min-width: 220px;
      height: auto !important;
      max-height: min(70vh, 520px) !important;
      overflow-x: hidden !important;
      overflow-y: auto !important;
      z-index: 1200;
      margin: 0 !important;
      padding: 8px 0 !important;
      border-radius: 10px 0 0 10px;
      background: var(--sidebar-bg, var(--card, #fff));
      box-shadow: 0 10px 30px rgba(0, 0, 0, 0.18);
      opacity: 0 !important;
      visibility: hidden !important;
      pointer-events: none !important;
      transform: translateX(10px) !important;
      transition: opacity 220ms ease, transform 250ms ease, visibility 0s linear 250ms;
    }

    /* ===========================================
     Flyout Hover & Focus
  =========================================== */

    .sidebar-mini-flyout
    :deep( .sidebar-menu-vue > li:hover > .sidebar-submenu ),
    .sidebar-mini-flyout
    :deep( .sidebar-menu-vue > li:focus-within > .sidebar-submenu ) {
      opacity: 1 !important;
      visibility: visible !important;
      pointer-events: auto !important;
      transform: translateX(0) !important;
      transition: opacity 220ms ease, transform 250ms ease, visibility 0s;
    }

    /* ===========================================
     Flyout Heading
  =========================================== */

    .sidebar-mini-flyout
    :deep(.sidebar-flyout-heading) {
      display: block !important;
      padding: 10px 18px !important;
      font-weight: 700;
      font-size: 13px;
      text-align: right;
      direction: rtl;
      opacity: 0.85;
    }

    /* ===========================================
     Flyout Links
  =========================================== */

    .sidebar-mini-flyout
    :deep(.sidebar-submenu a) {
      display: block !important;
      direction: rtl;
      text-align: right;
      white-space: normal;
      padding: 10px 18px;
    }

    /* ===========================================
     Dark Mode
  =========================================== */

    .sidebar-mini-flyout[data-app-theme="dark"]
    :deep(.sidebar-submenu) {
      background: var(--sidebar-bg, #202937) !important;
      color: #e8edf6;
    }

    /* ===========================================
     Reduced Motion
  =========================================== */

    @media (prefers-reduced-motion: reduce) {

      .sidebar-menu-vue > li > .sidebar-submenu,
      .sidebar-menu-vue > li > .sidebar-group-trigger > .sidebar-chevron {
        transition: none !important;
      }

      .sidebar-mini-flyout
      :deep(.sidebar-submenu) {
        transition: none !important;
      }
    }
</style>
