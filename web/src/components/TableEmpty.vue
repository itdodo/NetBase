<script setup lang="ts">
/**
 * 表格统一空状态：配合 el-table 的 #empty 插槽使用，全站空态样式一致。
 * <template #empty><TableEmpty description="暂无用户" /></template>
 */
withDefaults(defineProps<{ description?: string; sub?: string }>(), {
  description: '暂无数据',
  sub: ''
})
</script>

<template>
  <el-empty :description="description" :image-size="72">
    <p v-if="sub" class="table-empty-sub">{{ sub }}</p>
  </el-empty>
</template>

<style scoped>
.table-empty-sub {
  margin: -8px 0 0;
  color: var(--el-text-color-secondary);
  font-size: 12px;
}
</style>
