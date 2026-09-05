<template>
  <div class="forbidden">
    <h1>403</h1>
    <p>没有操作权限，请联系管理员</p>
    <el-button type="primary" @click="$router.push('/')">返回首页</el-button>
  </div>
</template>

<style scoped>
.forbidden {
  height: 100vh;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 12px;
}

.forbidden h1 {
  font-size: 72px;
  color: #e6a23c;
  margin: 0;
}
</style>
