<script setup lang="ts">
/**
 * 表格首载骨架屏：首次进入页面（列表尚未有任何数据）时替代 v-loading 遮罩，
 * 后续刷新仍走表格内 loading，避免整块闪烁。
 * <TableSkeleton v-if="firstLoading" />
 * <el-table v-else ... />
 */
withDefaults(defineProps<{ rows?: number }>(), {
  rows: 6
})
</script>

<template>
  <div class="table-skeleton">
    <div class="skeleton-header">
      <el-skeleton-item variant="text" style="width: 22%" />
      <el-skeleton-item variant="text" style="width: 14%" />
      <el-skeleton-item variant="text" style="width: 18%" />
      <el-skeleton-item variant="text" style="width: 12%" />
      <el-skeleton-item variant="button" style="width: 72px; margin-left: auto" />
    </div>
    <el-skeleton :rows="rows" animated />
  </div>
</template>

<style scoped>
.table-skeleton {
  padding: 14px 4px;
}

.skeleton-header {
  display: flex;
  align-items: center;
  gap: 12%;
  margin-bottom: 18px;
  padding-bottom: 10px;
  border-bottom: 1px solid var(--el-border-color-lighter);
}

.skeleton-header .el-skeleton-item {
  height: 14px;
}
</style>
