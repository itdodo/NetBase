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
const form = reactive({ userName: '', password: '', captchaId: '', captchaCode: '' })
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

/** 品牌区特性亮点 */
const features = [
  { title: '权限与审批流', desc: 'RBAC 双维度 · 钉钉式审批引擎' },
  { title: '实时通知', desc: '站内信与消息秒达，待办即时提醒' },
  { title: '数据审计', desc: '操作/登录/字段级变更全链路留痕' },
  { title: '开箱即用', desc: '租户级参数配置 · 定时任务 · 数据备份' }
]
</script>

<template>
  <div class="login-page">
    <!-- 左侧品牌区 -->
    <div class="brand-pane">
      <div class="brand-grid" aria-hidden="true" />
      <div class="brand-orb orb-a" aria-hidden="true" />
      <div class="brand-orb orb-b" aria-hidden="true" />

      <div class="brand-head">
        <div class="brand-logo">
          <svg viewBox="0 0 32 32" width="34" height="34" fill="none" aria-hidden="true">
            <rect x="2" y="2" width="28" height="28" rx="8" fill="currentColor" opacity="0.16" />
            <path d="M10 21V11l6 6 6-6v10" stroke="currentColor" stroke-width="2.4"
                  stroke-linecap="round" stroke-linejoin="round" />
          </svg>
          <span>NetBase</span>
        </div>
        <h1 class="brand-title">NetBase 管理系统</h1>
        <p class="brand-slogan">企业级应用底座 · 开箱即用</p>
      </div>

      <ul class="brand-features">
        <li v-for="f in features" :key="f.title">
          <span class="dot" aria-hidden="true" />
          <div>
            <div class="feature-title">{{ f.title }}</div>
            <div class="feature-desc">{{ f.desc }}</div>
          </div>
        </li>
      </ul>

      <p class="brand-foot">权限 · 审批 · 审计 · 实时 · 备份，一个底座全搞定</p>
    </div>

    <!-- 右侧表单区 -->
    <div class="form-pane">
      <div class="form-box">
        <div class="form-logo">
          <svg viewBox="0 0 32 32" width="30" height="30" fill="none" aria-hidden="true">
            <rect x="2" y="2" width="28" height="28" rx="8" fill="var(--el-color-primary)" opacity="0.14" />
            <path d="M10 21V11l6 6 6-6v10" stroke="var(--el-color-primary)" stroke-width="2.4"
                  stroke-linecap="round" stroke-linejoin="round" />
          </svg>
        </div>
        <h2 class="form-title">欢迎登录</h2>
        <p class="form-sub">NetBase 管理系统</p>

        <el-form ref="formRef" :model="form" :rules="rules" size="large" @keyup.enter="handleLogin">
          <el-form-item prop="userName">
            <el-input v-model="form.userName" placeholder="账号" :prefix-icon="User" autocomplete="username" />
          </el-form-item>
          <el-form-item prop="password">
            <el-input
              v-model="form.password"
              type="password"
              placeholder="密码"
              :prefix-icon="Lock"
              show-password
              autocomplete="current-password"
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
      </div>

      <p class="copyright">© 2026 NetBase · NetBase 管理系统</p>
    </div>
  </div>
</template>

<style scoped>
.login-page {
  height: 100vh;
  display: flex;
  overflow: hidden;
}

/* ---------------- 品牌区（固定深色，两主题通用） ---------------- */
.brand-pane {
  position: relative;
  flex: 0 0 58%;
  display: flex;
  flex-direction: column;
  justify-content: space-between;
  padding: 56px 64px 40px;
  color: #e8eefc;
  background:
    radial-gradient(1100px 500px at -10% -10%, rgba(64, 128, 255, 0.28), transparent 60%),
    linear-gradient(150deg, #101b2d 0%, #16263f 55%, #0e1826 100%);
  overflow: hidden;
}

.brand-grid {
  position: absolute;
  inset: 0;
  background-image:
    linear-gradient(rgba(140, 170, 255, 0.07) 1px, transparent 1px),
    linear-gradient(90deg, rgba(140, 170, 255, 0.07) 1px, transparent 1px);
  background-size: 44px 44px;
  mask-image: radial-gradient(700px 500px at 30% 40%, #000 30%, transparent 75%);
}

.brand-orb {
  position: absolute;
  border-radius: 50%;
  filter: blur(70px);
  animation: orb-breathe 9s ease-in-out infinite;
}

.orb-a {
  width: 380px;
  height: 380px;
  right: -80px;
  top: -60px;
  background: rgba(64, 158, 255, 0.32);
}

.orb-b {
  width: 300px;
  height: 300px;
  left: -70px;
  bottom: -80px;
  background: rgba(103, 194, 58, 0.16);
  animation-delay: -4.5s;
}

@keyframes orb-breathe {
  0%, 100% { transform: scale(1); opacity: 0.85; }
  50% { transform: scale(1.12); opacity: 1; }
}

.brand-head,
.brand-features,
.brand-foot {
  position: relative;
  z-index: 1;
}

.brand-logo {
  display: flex;
  align-items: center;
  gap: 10px;
  font-size: 20px;
  font-weight: 700;
  letter-spacing: 0.5px;
  color: #9ec3ff;
  margin-bottom: 42px;
}

.brand-title {
  font-size: 34px;
  font-weight: 700;
  margin: 0 0 12px;
  color: #f4f7ff;
}

.brand-slogan {
  font-size: 16px;
  color: rgba(214, 227, 252, 0.75);
  margin: 0 0 52px;
}

.brand-features {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 26px;
  max-width: 420px;
}

.brand-features li {
  display: flex;
  gap: 14px;
  align-items: flex-start;
}

.brand-features .dot {
  flex-shrink: 0;
  width: 9px;
  height: 9px;
  margin-top: 7px;
  border-radius: 50%;
  background: #6cb0ff;
  box-shadow: 0 0 10px rgba(108, 176, 255, 0.9);
}

.feature-title {
  font-size: 15px;
  font-weight: 600;
  color: #eef3ff;
}

.feature-desc {
  margin-top: 3px;
  font-size: 13px;
  color: rgba(203, 219, 248, 0.62);
}

.brand-foot {
  font-size: 12.5px;
  color: rgba(190, 208, 240, 0.45);
  letter-spacing: 1px;
}

/* ---------------- 表单区（自适应亮/暗） ---------------- */
.form-pane {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 40px 48px;
  background: var(--el-bg-color-page);
  position: relative;
}

.form-box {
  width: 100%;
  max-width: 360px;
}

.form-logo {
  margin-bottom: 18px;
}

.form-title {
  font-size: 26px;
  font-weight: 700;
  margin: 0 0 6px;
  color: var(--el-text-color-primary);
}

.form-sub {
  margin: 0 0 30px;
  font-size: 13.5px;
  color: var(--el-text-color-secondary);
}

.login-btn {
  width: 100%;
  letter-spacing: 6px;
  margin-top: 4px;
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

.copyright {
  position: absolute;
  bottom: 22px;
  font-size: 12.5px;
  color: var(--el-text-color-secondary);
}

/* ---------------- 响应式 ---------------- */
@media (max-width: 900px) {
  .brand-pane {
    display: none;
  }

  .form-pane {
    padding: 32px 24px;
  }
}
</style>
