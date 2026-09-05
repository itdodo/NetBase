<script setup lang="ts">
defineOptions({ name: 'MonitorOnlineView' })
</script>

<template>
  <el-card>
    <el-empty description="在线用户功能需要接入认证后会话支持后实现">
      <el-text type="info" size="small">
        规划：基于 JWT 黑名单/会话表展示当前在线用户，并支持强制下线。
      </el-text>
    </el-empty>
  </el-card>
</template>
