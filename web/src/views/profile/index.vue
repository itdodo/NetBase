<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage } from 'element-plus'
import type { FormInstance, FormRules, UploadRequestOptions } from 'element-plus'
import { User as UserIcon } from '@element-plus/icons-vue'
import { getProfile, updateProfile, uploadAvatar } from '@/api/auth'
import { useUserStore } from '@/stores/user'
import type { LoginResponse } from '@/api/auth'
import { formatDateTime } from '@/utils/format'

defineOptions({ name: 'ProfileView' })

const userStore = useUserStore()

const profile = ref<LoginResponse['user'] | null>(null)

const formRef = ref<FormInstance>()
const form = reactive({ nickName: '', phone: '', email: '' })
const saving = ref(false)

const rules: FormRules = {
  phone: [{ pattern: /^1[3-9]\d{9}$/, message: '手机号格式不正确', trigger: 'blur' }],
  email: [{ type: 'email', message: '邮箱格式不正确', trigger: 'blur' }]
}

async function loadProfile(): Promise<void> {
  const res = await getProfile()
  profile.value = res.user
  Object.assign(form, { nickName: res.user.nickName ?? '', phone: '', email: '' })
  // profile 接口未返回手机邮箱（UserDto 有），直接用返回值
  form.phone = (res.user as { phone?: string }).phone ?? ''
  form.email = (res.user as { email?: string }).email ?? ''
}

async function handleSave(): Promise<void> {
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid) return
  saving.value = true
  try {
    await updateProfile({ ...form })
    ElMessage.success('资料已更新')
    await loadProfile()
  } finally {
    saving.value = false
  }
}

/** 自定义头像上传（走统一 axios 实例带 token） */
async function handleAvatarUpload(options: UploadRequestOptions): Promise<unknown> {
  const url = await uploadAvatar(options.file)
  ElMessage.success('头像已更新')
  userStore.setAvatar(url) // 顶栏等全站位置即时生效
  if (profile.value) {
    profile.value.avatar = url
  }
  return url
}

function beforeAvatarUpload(file: File): boolean {
  const okType = ['image/jpeg', 'image/png', 'image/gif', 'image/webp'].includes(file.type)
  const okSize = file.size / 1024 / 1024 <= 2
  if (!okType) ElMessage.error('仅支持 jpg/png/gif/webp')
  if (!okSize) ElMessage.error('头像不能超过 2MB')
  return okType && okSize
}

onMounted(loadProfile)
</script>

<template>
  <el-row :gutter="16">
    <!-- 左：头像与角色 -->
    <el-col :span="8">
      <el-card>
        <div class="avatar-panel">
          <el-upload
            :show-file-list="false"
            :http-request="handleAvatarUpload"
            :before-upload="beforeAvatarUpload"
            accept="image/jpeg,image/png,image/gif,image/webp"
          >
            <el-avatar :size="96" :src="profile?.avatar" :icon="UserIcon" class="avatar" />
            <div class="avatar-tip">点击更换头像（2MB 内）</div>
          </el-upload>
          <h3>{{ profile?.userName }}</h3>
          <p class="nick">{{ profile?.nickName || '未设置昵称' }}</p>
          <el-tag v-for="role in profile?.roles ?? []" :key="role.id" size="small" class="role-tag">
            {{ role.roleName }}
          </el-tag>
        </div>
      </el-card>
    </el-col>

    <!-- 右：资料编辑 -->
    <el-col :span="16">
      <el-card>
        <template #header>基本资料</template>
        <el-form ref="formRef" :model="form" :rules="rules" label-width="90px" style="max-width: 460px">
          <el-form-item label="用户名">
            <el-input :model-value="profile?.userName" disabled />
          </el-form-item>
          <el-form-item label="昵称" prop="nickName">
            <el-input v-model="form.nickName" maxlength="50" />
          </el-form-item>
          <el-form-item label="手机号" prop="phone">
            <el-input v-model="form.phone" maxlength="20" />
          </el-form-item>
          <el-form-item label="邮箱" prop="email">
            <el-input v-model="form.email" maxlength="100" />
          </el-form-item>
          <el-form-item>
            <el-button type="primary" :loading="saving" @click="handleSave">保存修改</el-button>
          </el-form-item>
        </el-form>
        <el-descriptions v-if="profile" :column="1" border style="max-width: 460px">
          <el-descriptions-item label="注册时间">{{ formatDateTime(null, null, profile.createTime) }}</el-descriptions-item>
          <el-descriptions-item label="最后登录">{{ profile.lastLoginTime ? formatDateTime(null, null, profile.lastLoginTime) : '-' }}</el-descriptions-item>
        </el-descriptions>
      </el-card>
    </el-col>
  </el-row>
</template>

<style scoped>
.avatar-panel {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 8px;
  padding: 12px 0;
}

.avatar {
  cursor: pointer;
  background: #409eff;
}

.avatar-tip {
  font-size: 12px;
  color: #909399;
}

.nick {
  color: #909399;
  margin: 0;
}

.role-tag {
  margin: 2px;
}
</style>
