<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import type { FormInstance, FormRules } from 'element-plus'
import { Plus, Refresh, Search } from '@element-plus/icons-vue'
import {
  assignUserRoles,
  createUser,
  deleteUser,
  getUserPage,
  resetUserPassword,
  updateUser
} from '@/api/user'
import { getRoleList } from '@/api/role'
import type { RoleSimple, User } from '@/types/api'

const loading = ref(false)
const list = ref<User[]>([])
const total = ref(0)
const roleOptions = ref<RoleSimple[]>([])

const query = reactive({ pageIndex: 1, pageSize: 10, keyword: '', status: undefined as number | undefined })

async function loadData() {
  loading.value = true
  try {
    const page = await getUserPage(query)
    list.value = page.items
    total.value = page.total
  } finally {
    loading.value = false
  }
}

function handleSearch() {
  query.pageIndex = 1
  loadData()
}

function handleReset() {
  query.keyword = ''
  query.status = undefined
  handleSearch()
}

// ---------- 新增 / 编辑 ----------
const dialogVisible = ref(false)
const editingId = ref<number | null>(null)
const formRef = ref<FormInstance>()
const form = reactive({
  userName: '',
  nickName: '',
  phone: '',
  email: '',
  password: '',
  status: 1,
  roleIds: [] as number[]
})

const rules: FormRules = {
  userName: [{ required: true, message: '请输入用户名', trigger: 'blur' }]
}

function openCreate() {
  editingId.value = null
  Object.assign(form, { userName: '', nickName: '', phone: '', email: '', password: '', status: 1, roleIds: [] })
  dialogVisible.value = true
}

function openEdit(row: User) {
  editingId.value = row.id
  Object.assign(form, {
    userName: row.userName,
    nickName: row.nickName ?? '',
    phone: row.phone ?? '',
    email: row.email ?? '',
    password: '',
    status: row.status,
    roleIds: row.roles.map((r) => r.id)
  })
  dialogVisible.value = true
}

async function handleSave() {
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid) return

  if (editingId.value == null) {
    await createUser({
      userName: form.userName,
      nickName: form.nickName,
      phone: form.phone,
      email: form.email,
      password: form.password || undefined,
      status: form.status,
      roleIds: form.roleIds
    })
    ElMessage.success('创建成功')
  } else {
    await updateUser(editingId.value, {
      nickName: form.nickName,
      phone: form.phone,
      email: form.email,
      status: form.status,
      roleIds: form.roleIds
    })
    ElMessage.success('更新成功')
  }
  dialogVisible.value = false
  loadData()
}

// ---------- 删除 / 重置密码 ----------
async function handleDelete(row: User) {
  await ElMessageBox.confirm(`确定删除用户「${row.userName}」吗？`, '提示', { type: 'warning' })
  await deleteUser(row.id)
  ElMessage.success('删除成功')
  loadData()
}

async function handleResetPassword(row: User) {
  await ElMessageBox.confirm(
    `确定将用户「${row.userName}」的密码重置为默认密码 123456 吗？`,
    '重置密码',
    { type: 'warning' }
  )
  await resetUserPassword(row.id)
  ElMessage.success('密码已重置')
}

onMounted(() => {
  loadData()
  getRoleList().then((roles) => (roleOptions.value = roles))
})
</script>

<template>
  <el-card>
    <!-- 搜索栏 -->
    <div class="toolbar">
      <el-input
        v-model="query.keyword"
        placeholder="用户名/昵称"
        clearable
        style="width: 200px"
        :prefix-icon="Search"
        @keyup.enter="handleSearch"
      />
      <el-select v-model="query.status" placeholder="状态" clearable style="width: 120px">
        <el-option label="启用" :value="1" />
        <el-option label="停用" :value="0" />
      </el-select>
      <el-button type="primary" :icon="Search" @click="handleSearch">查询</el-button>
      <el-button :icon="Refresh" @click="handleReset">重置</el-button>
      <el-button v-permission="'sys:user:add'" type="success" :icon="Plus" @click="openCreate">
        新增用户
      </el-button>
    </div>

    <!-- 数据表格 -->
    <el-table v-loading="loading" :data="list" border stripe>
      <el-table-column prop="userName" label="用户名" min-width="120" />
      <el-table-column prop="nickName" label="昵称" min-width="120" />
      <el-table-column prop="phone" label="手机号" min-width="130" />
      <el-table-column prop="email" label="邮箱" min-width="170" show-overflow-tooltip />
      <el-table-column label="角色" min-width="160">
        <template #default="{ row }">
          <el-tag v-for="role in row.roles" :key="role.id" size="small" style="margin-right: 4px">
            {{ role.roleName }}
          </el-tag>
          <span v-if="row.roles.length === 0">-</span>
        </template>
      </el-table-column>
      <el-table-column label="状态" width="90" align="center">
        <template #default="{ row }">
          <el-tag :type="row.status === 1 ? 'success' : 'danger'">
            {{ row.status === 1 ? '启用' : '停用' }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="createTime" label="创建时间" width="170" />
      <el-table-column label="操作" width="240" fixed="right">
        <template #default="{ row }">
          <el-button v-permission="'sys:user:edit'" link type="primary" @click="openEdit(row)">
            编辑
          </el-button>
          <el-button v-permission="'sys:user:edit'" link type="warning" @click="handleResetPassword(row)">
            重置密码
          </el-button>
          <el-button
            v-permission="'sys:user:delete'"
            link
            type="danger"
            :disabled="row.userName === 'admin'"
            @click="handleDelete(row)"
          >
            删除
          </el-button>
        </template>
      </el-table-column>
    </el-table>

    <!-- 分页 -->
    <el-pagination
      v-model:current-page="query.pageIndex"
      v-model:page-size="query.pageSize"
      class="pagination"
      background
      layout="total, sizes, prev, pager, next, jumper"
      :total="total"
      :page-sizes="[10, 20, 50]"
      @current-change="loadData"
      @size-change="handleSearch"
    />

    <!-- 新增/编辑弹窗 -->
    <el-dialog
      v-model="dialogVisible"
      :title="editingId == null ? '新增用户' : `编辑用户：${form.userName}`"
      width="520px"
    >
      <el-form ref="formRef" :model="form" :rules="rules" label-width="90px">
        <el-form-item label="用户名" prop="userName">
          <el-input v-model="form.userName" :disabled="editingId != null" placeholder="登录账号" />
        </el-form-item>
        <el-form-item label="昵称">
          <el-input v-model="form.nickName" />
        </el-form-item>
        <el-form-item label="手机号">
          <el-input v-model="form.phone" />
        </el-form-item>
        <el-form-item label="邮箱">
          <el-input v-model="form.email" />
        </el-form-item>
        <el-form-item v-if="editingId == null" label="初始密码">
          <el-input v-model="form.password" type="password" show-password placeholder="留空则使用默认密码 123456" />
        </el-form-item>
        <el-form-item label="状态">
          <el-radio-group v-model="form.status">
            <el-radio :value="1">启用</el-radio>
            <el-radio :value="0">停用</el-radio>
          </el-radio-group>
        </el-form-item>
        <el-form-item label="角色">
          <el-checkbox-group v-model="form.roleIds">
            <el-checkbox v-for="role in roleOptions" :key="role.id" :value="role.id">
              {{ role.roleName }}
            </el-checkbox>
          </el-checkbox-group>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" @click="handleSave">确定</el-button>
      </template>
    </el-dialog>
  </el-card>
</template>

<style scoped>
.toolbar {
  display: flex;
  gap: 8px;
  margin-bottom: 12px;
  flex-wrap: wrap;
}

.pagination {
  margin-top: 12px;
  justify-content: flex-end;
}
</style>
