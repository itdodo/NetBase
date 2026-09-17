<script setup lang="ts">
import { ref } from 'vue'
import { ElMessage } from 'element-plus'
import { actFlowTask, addSignFlowTask, transferFlowTask } from '@/api/flow'
import { getUserList } from '@/api/user'
import type { User } from '@/types/api'

/**
 * 审批操作条：传入待办任务，提供 同意/拒绝/转办/加签（前/后）操作。
 * 操作成功后 emit('acted') 由父页面刷新列表/详情。
 */
const props = defineProps<{ taskId: string | number }>()
const emit = defineEmits<{ acted: [] }>()

const comment = ref('')
const submitting = ref(false)
const transferVisible = ref(false)
const addSignVisible = ref(false)
const targetUserIds = ref<number[]>([])
const addSignBefore = ref(true)
const users = ref<User[]>([])
const usersLoaded = ref(false)

async function ensureUsers(): Promise<void> {
  if (!usersLoaded.value) {
    users.value = await getUserList()
    usersLoaded.value = true
  }
}

async function act(action: 'approve' | 'reject'): Promise<void> {
  if (action === 'reject' && !comment.value.trim()) {
    ElMessage.warning('拒绝时请填写审批意见')
    return
  }
  submitting.value = true
  try {
    await actFlowTask(props.taskId, action, comment.value.trim() || undefined)
    ElMessage.success(action === 'approve' ? '已同意' : '已拒绝')
    comment.value = ''
    emit('acted')
  } finally {
    submitting.value = false
  }
}

async function openDialog(kind: 'transfer' | 'addsign'): Promise<void> {
  targetUserIds.value = []
  await ensureUsers()
  if (kind === 'transfer') {
    transferVisible.value = true
  } else {
    addSignVisible.value = true
  }
}

async function submitTransfer(): Promise<void> {
  if (targetUserIds.value.length === 0) {
    ElMessage.warning('请选择转办目标人')
    return
  }
  await transferFlowTask(props.taskId, targetUserIds.value, comment.value.trim() || undefined)
  ElMessage.success('已转办')
  transferVisible.value = false
  comment.value = ''
  emit('acted')
}

async function submitAddSign(): Promise<void> {
  if (targetUserIds.value.length === 0) {
    ElMessage.warning('请选择加签人')
    return
  }
  await addSignFlowTask(props.taskId, targetUserIds.value, addSignBefore.value, comment.value.trim() || undefined)
  ElMessage.success(addSignBefore.value ? '前加签成功' : '后加签成功')
  addSignVisible.value = false
  comment.value = ''
  emit('acted')
}
</script>

<template>
  <div class="approval-actions">
    <el-input
      v-model="comment"
      type="textarea"
      :rows="2"
      maxlength="500"
      placeholder="审批意见（拒绝时必填）"
      class="comment-input"
    />
    <div class="buttons">
      <el-button type="success" :loading="submitting" @click="act('approve')">同意</el-button>
      <el-button type="danger" plain :loading="submitting" @click="act('reject')">拒绝</el-button>
      <el-button plain @click="openDialog('transfer')">转办</el-button>
      <el-button plain @click="openDialog('addsign')">加签</el-button>
    </div>

    <el-dialog v-model="transferVisible" title="转办" width="420px" append-to-body>
      <el-select v-model="targetUserIds" multiple filterable placeholder="选择转办目标人" style="width: 100%">
        <el-option v-for="u in users" :key="u.id" :label="u.nickName || u.userName" :value="u.id" />
      </el-select>
      <template #footer>
        <el-button @click="transferVisible = false">取消</el-button>
        <el-button type="primary" @click="submitTransfer">确定</el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="addSignVisible" title="加签" width="420px" append-to-body>
      <el-select v-model="targetUserIds" multiple filterable placeholder="选择加签人" style="width: 100%">
        <el-option v-for="u in users" :key="u.id" :label="u.nickName || u.userName" :value="u.id" />
      </el-select>
      <el-radio-group v-model="addSignBefore" class="sign-mode">
        <el-radio :value="true">前加签（与当前节点共同审批）</el-radio>
        <el-radio :value="false">后加签（本节点通过后追加审批）</el-radio>
      </el-radio-group>
      <template #footer>
        <el-button @click="addSignVisible = false">取消</el-button>
        <el-button type="primary" @click="submitAddSign">确定</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.comment-input {
  margin-bottom: 10px;
}

.buttons {
  display: flex;
  gap: 8px;
}

.sign-mode {
  display: flex;
  flex-direction: column;
  gap: 8px;
  margin-top: 12px;
}
</style>
