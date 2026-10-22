import { createRouter, createWebHistory } from 'vue-router'

import {
  getSession,
  hasValidSession,
  restoreSessionFromOpenTab,
  synchronizeSessionPermissions,
} from '@/services/auth'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/',
      name: 'home',
      component: () => import('@/views/Home.vue'),
    },
    {
      path: '/login',
      name: 'login',
      component: () => import('@/views/Identity/Login.vue'),
      meta: { guestOnly: true },
    },
    {
      path: '/register',
      name: 'register',
      component: () => import('@/views/Identity/Register.vue'),
      meta: { guestOnly: true },
    },
    {
      path: '/dashboard',
      component: () => import('@/layouts/DashboardLayout.vue'),
      meta: { requiresAuth: true },
      children: [
        {
          path: '',
          name: 'dashboard',
          component: () => import('@/views/Dashboard.vue'),
          meta: { title: 'داشبورد' },
        },
        {
          path: 'account',
          name: 'account-profile',
          component: () => import('@/views/AccountProfile.vue'),
          meta: { title: 'ویرایش حساب کاربری' },
        },
        {
          path: 'users',
          name: 'users',
          component: () => import('@/views/Users.vue'),
          meta: { title: 'کاربران', requiredPermission: 'UsersRead' },
        },
        {
          path: 'roles',
          name: 'roles',
          component: () => import('@/views/Roles.vue'),
          meta: { title: 'نقش‌ها', requiredPermission: 'RolesRead' },
        },
        {
          path: 'roles/:roleName/permissions',
          name: 'role-permissions',
          component: () => import('@/views/RolePermissions.vue'),
          meta: { title: 'مدیریت دسترسی‌های نقش', requiredPermission: 'RolePoliciesManage' },
        },
        {
          path: 'audit-logs',
          name: 'audit-logs',
          component: () => import('@/views/AuditLogs.vue'),
          meta: { title: 'گزارش فعالیت کاربران', requiredPermission: 'AuditLogsRead' },
        },
        {
          path: 'inventory/goods-receipts',
          name: 'goods-receipts',
          component: () => import('@/views/Inventory/GoodsReceipts.vue'),
          meta: {
            title: 'تاریخچه ورود کالا',
            requiredPermission: 'GoodsReceiptsRead',
          },
        },
        {
          path: 'inventory/goods-issues',
          name: 'goods-issues',
          component: () => import('@/views/Inventory/GoodsIssues.vue'),
          meta: {
            title: 'تاریخچه خروج کالا',
            requiredPermission: 'GoodsIssuesRead',
          },
        },
        {
          path: 'master-data/products',
          name: 'products',
          component: () => import('@/views/MasterData/Products.vue'),
          meta: {
            title: 'تعریف کالا',
            requiredPermission: 'MasterDataRead',
          },
        },
        {
          path: 'master-data/units-of-measure',
          name: 'units-of-measure',
          component: () => import('@/views/MasterData/UnitsOfMeasure.vue'),
          meta: {
            title: 'واحدهای اندازه‌گیری',
            requiredPermission: 'MasterDataRead',
          },
        },
        {
          path: 'master-data/suppliers',
          name: 'suppliers',
          component: () => import('@/views/MasterData/Suppliers.vue'),
          meta: {
            title: 'تأمین‌کنندگان',
            requiredPermission: 'MasterDataRead',
          },
        },
        {
          path: 'master-data/warehouses',
          name: 'warehouses',
          component: () => import('@/views/MasterData/Warehouses.vue'),
          meta: {
            title: 'انبارها',
            requiredPermission: 'MasterDataRead',
          },
        },
        {
          path: 'master-data/people',
          name: 'people',
          component: () => import('@/views/MasterData/People.vue'),
          meta: {
            title: 'افراد',
            requiredPermission: 'MasterDataRead',
          },
        },
        {
          path: 'master-data/goods-issue-destinations',
          name: 'goods-issue-destinations',
          component: () => import('@/views/MasterData/GoodsIssueDestinations.vue'),
          meta: {
            title: 'مقصدهای خروج',
            requiredPermission: 'MasterDataRead',
          },
        },
      ],
    },
    {
      path: '/:pathMatch(.*)*',
      redirect: '/',
    },
  ],
})

router.beforeEach(async (to) => {
  let isAuthenticated = hasValidSession()

  if (!isAuthenticated) {
    await restoreSessionFromOpenTab()
    isAuthenticated = hasValidSession()
  }

  if (to.meta.requiresAuth && !isAuthenticated) {
    return { name: 'login', query: { redirect: to.fullPath } }
  }

  if (to.meta.guestOnly && isAuthenticated) {
    return { name: 'dashboard' }
  }

  let session = getSession()
  if (to.meta.requiresAuth && isAuthenticated) {
    try {
      session = await synchronizeSessionPermissions()
    } catch {
      // The API still enforces permissions; keep the cached menu state on transient failures.
    }

    if (!session) {
      return { name: 'login', query: { redirect: to.fullPath } }
    }
  }

  const requiredPermission = to.meta.requiredPermission
  if (
    typeof requiredPermission === 'string' &&
    !session?.permissions.includes(requiredPermission)
  ) {
    return { name: 'dashboard' }
  }

  return true
})

export default router
