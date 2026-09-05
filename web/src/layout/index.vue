<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { Fold, Expand, ArrowDown } from '@element-plus/icons-vue'
import Sidebar from './components/Sidebar.vue'
import TagsView from './components/TagsView.vue'
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
                <el-dropdown-item @click="handleLogout">退出登录</el-dropdown-item>
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
