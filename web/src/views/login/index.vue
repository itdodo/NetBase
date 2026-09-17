<script setup lang="ts">
import { reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, FormInstance, FormRules } from 'element-plus'
import { Lock, User, Refresh } from '@element-plus/icons-vue'
import { getCaptcha } from '@/api/auth'
import { useUserStore } from '@/stores/user'

const router = useRouter()
const route = useRoute()
const userStore = useUserStore()

const formRef = ref<FormInstance>()
const loading = ref(false)
const form = reactive({ userName: 'admin', password: '123456', captchaId: '', captchaCode: '' })
const captchaSvg = ref('')
const defaultCaptchaSvg = ''

async function refreshCaptcha(): Promise<void> {
  try {
    const captcha = await getCaptcha()
    form.captchaId = captcha.captchaId
    captchaSvg.value = captcha.svg
  } catch {
    captchaSvg.value = '' // 验证码获取失败时允许输入用户名密码后由后端提示
  }
}

refreshCaptcha()

const rules: FormRules = {
  userName: [{ required: true, message: '请输入账号', trigger: 'blur' }],
  password: [
    { required: true, message: '请输入密码', trigger: 'blur' },
    { min: 6, message: '密码至少 6 位', trigger: 'blur' }
  ],
  captchaCode: [{ required: true, message: '请输入验证码', trigger: 'blur' }]
}

async function handleLogin() {
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid) return

  loading.value = true
  try {
    await userStore.login(form)
    if (userStore.mustChangePassword) {
      ElMessage.warning('密码已超期，请先修改密码')
      await router.push((route.query.redirect as string) || '/?mustChangePwd=1')
      return
    }
    ElMessage.success('登录成功')
    await router.push((route.query.redirect as string) || '/')
  } catch {
    // 错误提示由 request 拦截器统一处理
  } finally {
    // 登录失败（含验证码错误）后刷新验证码
    form.captchaCode = ''
    refreshCaptcha()
    loading.value = false
  }
}
</script>

<template>
  <div class="login-page">
    <el-card class="login-card">
      <h2 class="title">NetBase 管理系统</h2>
      <el-form ref="formRef" :model="form" :rules="rules" size="large" @keyup.enter="handleLogin">
        <el-form-item prop="userName">
          <el-input v-model="form.userName" placeholder="账号" :prefix-icon="User" />
        </el-form-item>
        <el-form-item prop="password">
          <el-input
            v-model="form.password"
            type="password"
            placeholder="密码"
            :prefix-icon="Lock"
            show-password
          />
        </el-form-item>
        <el-form-item prop="captchaCode">
          <div class="captcha-row">
            <el-input v-model="form.captchaCode" placeholder="验证码" :prefix-icon="Refresh" maxlength="4" />
            <img
              class="captcha-img"
              title="点击刷新"
              :src="captchaSvg || defaultCaptchaSvg"
              @click="refreshCaptcha"
            />
          </div>
        </el-form-item>
        <el-form-item>
          <el-button type="primary" class="login-btn" :loading="loading" @click="handleLogin">
            登 录
          </el-button>
        </el-form-item>
      </el-form>
    </el-card>
  </div>
</template>

<style scoped>
.login-page {
  height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  background: linear-gradient(135deg, #1d2935 0%, #2c3e50 100%);
}

.login-card {
  width: 400px;
  padding: 12px 8px;
}

.title {
  text-align: center;
  margin: 8px 0 16px;
  color: #303133;
}

.notice {
  margin-bottom: 20px;
}

.login-btn {
  width: 100%;
}

.captcha-row {
  display: flex;
  gap: 8px;
  width: 100%;
}

.captcha-img {
  cursor: pointer;
  flex-shrink: 0;
  border-radius: 4px;
  height: 40px;
  display: block;
}
</style>
