<template>
  <div class="forbidden">
    <h1>403</h1>
    <p>暂无功能权限，请联系管理员分配角色</p>
    <div class="actions">
      <el-button @click="$router.push('/login')">重新登录</el-button>
      <el-button type="primary" @click="$router.push('/')">返回首页</el-button>
    </div>
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

.actions {
  display: flex;
  gap: 12px;
  margin-top: 4px;
}

.forbidden h1 {
  font-size: 72px;
  color: #e6a23c;
  margin: 0;
}
</style>
