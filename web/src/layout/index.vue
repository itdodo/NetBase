<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import type { FormInstance, FormRules } from 'element-plus'
import { Fold, Expand, ArrowDown } from '@element-plus/icons-vue'
import Sidebar from './components/Sidebar.vue'
import TagsView from './components/TagsView.vue'
import { changePassword } from '@/api/log'
import { useUserStore } from '@/stores/user'
import { usePermissionStore } from '@/stores/permission'
import { useTabsStore } from '@/stores/tabs'

const route = useRoute()
const router = useRouter()
const userStore = useUserStore()
const permissionStore = usePermissionStore()
const tabsStore = useTabsStore()

const isCollapse = ref(false)
const userName = computed(() => userStore.userName)

/** keep-alive 缓存名单：当前打开页签对应的组件名，关闭页签即释放缓存 */
const cachedNames = computed(() =>
  tabsStore.visitedViews.map((v) => v.cachedName).filter((n): n is string => !!n),
)

const breadcrumbs = computed(() =>
  route.matched
    .filter((r) => r.meta?.title)
    .map((r) => ({ title: String(r.meta.title), path: r.path }))
)

async function handleLogout() {
  userStore.logout()
  permissionStore.reset()
  tabsStore.closeAll()
  ElMessage.success('已退出登录')
  await router.push('/login')
}

// ---------- 修改自己密码 ----------
const pwdDialogVisible = ref(false)
const pwdFormRef = ref<FormInstance>()
const pwdForm = reactive({ oldPassword: '', newPassword: '', confirmPassword: '' })
const pwdSaving = ref(false)

const pwdRules: FormRules = {
  oldPassword: [{ required: true, message: '请输入旧密码', trigger: 'blur' }],
  newPassword: [
    { required: true, message: '请输入新密码', trigger: 'blur' },
    { min: 6, message: '密码至少 6 位', trigger: 'blur' }
  ],
  confirmPassword: [
    { required: true, message: '请再次输入新密码', trigger: 'blur' },
    {
      validator: (_rule, value: string, callback) =>
        value === pwdForm.newPassword ? callback() : callback(new Error('两次输入的密码不一致')),
      trigger: 'blur'
    }
  ]
}

function openChangePassword(): void {
  Object.assign(pwdForm, { oldPassword: '', newPassword: '', confirmPassword: '' })
  pwdDialogVisible.value = true
}

async function handleChangePassword(): Promise<void> {
  const valid = await pwdFormRef.value?.validate().catch(() => false)
  if (!valid) return

  pwdSaving.value = true
  try {
    await changePassword({ oldPassword: pwdForm.oldPassword, newPassword: pwdForm.newPassword })
    pwdDialogVisible.value = false
    // 改密后服务端已清除全部会话，本地登出并重新登录
    userStore.logout()
    permissionStore.reset()
    tabsStore.closeAll()
    ElMessage.success('密码修改成功，请重新登录')
    await router.push('/login')
  } catch {
    // 错误提示由 request 拦截器统一处理
  } finally {
    pwdSaving.value = false
  }
}
</script>

<template>
  <el-container class="layout">
    <el-aside :width="isCollapse ? '64px' : '220px'" class="aside">
      <div class="logo">
        <span v-if="!isCollapse">NetBase 管理系统</span>
        <span v-else>NB</span>
      </div>
      <Sidebar :collapse="isCollapse" />
    </el-aside>

    <el-container>
      <el-header class="header">
        <el-icon class="collapse-btn" @click="isCollapse = !isCollapse">
          <Fold v-if="!isCollapse" />
          <Expand v-else />
        </el-icon>

        <el-breadcrumb separator="/">
          <el-breadcrumb-item v-for="item in breadcrumbs" :key="item.path">
            {{ item.title }}
          </el-breadcrumb-item>
        </el-breadcrumb>

        <div class="header-right">
          <el-dropdown>
            <span class="user-info">
              {{ userName }}
              <el-icon><ArrowDown /></el-icon>
            </span>
            <template #dropdown>
              <el-dropdown-menu>
                <el-dropdown-item @click="openChangePassword">修改密码</el-dropdown-item>
                <el-dropdown-item divided @click="handleLogout">退出登录</el-dropdown-item>
              </el-dropdown-menu>
            </template>
          </el-dropdown>
        </div>
      </el-header>

      <TagsView />

      <el-main class="main">
        <router-view v-slot="{ Component }">
          <keep-alive :include="cachedNames">
            <component :is="Component" />
          </keep-alive>
        </router-view>
      </el-main>
    </el-container>

    <!-- 修改自己密码 -->
    <el-dialog v-model="pwdDialogVisible" title="修改密码" width="440px">
      <el-form ref="pwdFormRef" :model="pwdForm" :rules="pwdRules" label-width="90px">
        <el-form-item label="旧密码" prop="oldPassword">
          <el-input v-model="pwdForm.oldPassword" type="password" show-password />
        </el-form-item>
        <el-form-item label="新密码" prop="newPassword">
          <el-input v-model="pwdForm.newPassword" type="password" show-password placeholder="至少 6 位，含字母和数字" />
        </el-form-item>
        <el-form-item label="确认新密码" prop="confirmPassword">
          <el-input v-model="pwdForm.confirmPassword" type="password" show-password />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="pwdDialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="pwdSaving" @click="handleChangePassword">确定</el-button>
      </template>
    </el-dialog>
  </el-container>
</template>

<style scoped>
.layout {
  height: 100vh;
  /* 高内容页面下防止 flex 子项把布局撑破（body 级滚动、header 错位） */
  overflow: hidden;
}

/* 右侧嵌套容器允许收缩，使 el-main 的 overflow:auto 内部滚动生效 */
.layout > :deep(.el-container) {
  min-height: 0;
  overflow: hidden;
}

.aside {
  background-color: #1d2935;
  transition: width 0.2s;
  /* 菜单过长时侧边栏自身滚动 */
  overflow-y: auto;
}

.logo {
  height: 56px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: #fff;
  font-size: 16px;
  font-weight: 600;
  white-space: nowrap;
}

.header {
  display: flex;
  align-items: center;
  gap: 12px;
  border-bottom: 1px solid #e4e7ed;
  background: #fff;
}

.collapse-btn {
  font-size: 20px;
  cursor: pointer;
}

.header-right {
  margin-left: auto;
}

.user-info {
  display: flex;
  align-items: center;
  gap: 4px;
  cursor: pointer;
  color: #303133;
}

.main {
  background: #f5f7fa;
  overflow: auto;
}
</style>
