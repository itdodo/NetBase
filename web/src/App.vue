<script setup lang="ts">
import zhCn from 'element-plus/es/locale/lang/zh-cn'
</script>

<template>
  <el-config-provider :locale="zhCn">
    <router-view />
  </el-config-provider>
</template>

<style>
* {
  margin: 0;
  padding: 0;
  box-sizing: border-box;
}

html,
body,
#app {
  height: 100%;
  font-family: 'Helvetica Neue', Helvetica, 'PingFang SC', 'Microsoft YaHei', Arial, sans-serif;
  background: var(--el-bg-color-page);
}

/* 侧边栏主题变量（全局，供 layout/Sidebar 引用；暗黑跟随 EP 暗黑背景） */
:root {
  --sidebar-bg: #1d2935;
  --sidebar-text: #a7b1c2;
  --sidebar-hover-bg: rgba(255, 255, 255, 0.08);
}

html.dark {
  --sidebar-bg: var(--el-bg-color);
  --sidebar-text: var(--el-text-color-regular);
  --sidebar-hover-bg: var(--el-fill-color-light);
}
</style>
