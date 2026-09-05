<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
defineOptions({ name: 'SystemMenuView' })

import { ElMessage, ElMessageBox } from 'element-plus'
import type { FormInstance, FormRules } from 'element-plus'
import { Plus, Refresh } from '@element-plus/icons-vue'
import { createMenu, deleteMenu, getMenuTree, updateMenu } from '@/api/menu'
import type { MenuSave, MenuTree } from '@/types/api'
import { MENU_TYPE } from '@/types/api'

const loading = ref(false)
const tree = ref<MenuTree[]>([])

/** 搜索关键字（前端过滤） */
const keyword = ref('')

const filteredTree = computed(() => {
  if (!keyword.value) return tree.value
  const match = (nodes: MenuTree[]): MenuTree[] =>
    nodes
      .map((node) => {
        const children = match(node.children)
        if (node.menuName.includes(keyword.value) || children.length > 0) {
          return { ...node, children }
        }
        return null
      })
      .filter((n): n is MenuTree => n !== null)
  return match(tree.value)
})

async function loadData() {
  loading.value = true
  try {
    tree.value = await getMenuTree()
  } finally {
    loading.value = false
  }
}

// ---------- 新增 / 编辑 ----------
const dialogVisible = ref(false)
const editingId = ref<number | null>(null)
const formRef = ref<FormInstance>()
const form = reactive<MenuSave>({
  parentId: 0,
  menuName: '',
  menuType: MENU_TYPE.MENU,
  path: '',
  component: '',
  permission: '',
  icon: '',
  sort: 0,
  visible: true,
  status: 1
})

const rules: FormRules = {
  menuName: [{ required: true, message: '请输入菜单名称', trigger: 'blur' }]
}

/** 上级菜单可选项（目录或菜单，排除按钮），供 tree-select 选择 */
const parentOptions = computed(() => {
  const toNode = (m: MenuTree): MenuTree & { disabled?: boolean } => ({
    ...m,
    disabled: m.menuType === MENU_TYPE.BUTTON || m.id === editingId.value,
    children: m.children.map(toNode)
  })
  return [{ id: 0, menuName: '顶级菜单', children: tree.value.map(toNode) } as never]
})

function openCreate(parentId = 0) {
  editingId.value = null
  Object.assign(form, {
    parentId,
    menuName: '',
    menuType: parentId === 0 ? MENU_TYPE.DIRECTORY : MENU_TYPE.MENU,
    path: '',
    component: '',
    permission: '',
    icon: '',
    sort: 0,
    visible: true,
    status: 1
  })
  dialogVisible.value = true
}

function openEdit(row: MenuTree) {
  editingId.value = row.id
  Object.assign(form, {
    parentId: row.parentId,
    menuName: row.menuName,
    menuType: row.menuType,
    path: row.path ?? '',
    component: row.component ?? '',
    permission: row.permission ?? '',
    icon: row.icon ?? '',
    sort: row.sort,
    visible: row.visible,
    status: row.status
  })
  dialogVisible.value = true
}

async function handleSave() {
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid) return

  if (editingId.value == null) {
    await createMenu({ ...form })
    ElMessage.success('创建成功')
  } else {
    await updateMenu(editingId.value, { ...form })
    ElMessage.success('更新成功')
  }
  dialogVisible.value = false
  loadData()
}

async function handleDelete(row: MenuTree) {
  await ElMessageBox.confirm(`确定删除菜单「${row.menuName}」吗？`, '提示', { type: 'warning' })
  await deleteMenu(row.id)
  ElMessage.success('删除成功')
  loadData()
}

const menuTypeText: Record<number, string> = { 1: '目录', 2: '菜单', 3: '按钮' }
const menuTypeTag: Record<number, 'primary' | 'success' | 'warning'> = {
  1: 'primary',
  2: 'success',
  3: 'warning'
}

onMounted(loadData)
</script>

<template>
  <el-card>
    <div class="toolbar">
      <el-input v-model="keyword" placeholder="菜单名称过滤" clearable style="width: 200px" />
      <el-button :icon="Refresh" @click="keyword = ''">重置</el-button>
      <el-button v-permission="'sys:menu:add'" type="success" :icon="Plus" @click="openCreate(0)">
        新增目录
      </el-button>
    </div>

    <el-table
      v-loading="loading"
      :data="filteredTree"
      row-key="id"
      border
      default-expand-all
      :tree-props="{ children: 'children' }"
    >
      <el-table-column prop="menuName" label="菜单名称" min-width="180" />
      <el-table-column label="类型" width="90" align="center">
        <template #default="{ row }">
          <el-tag :type="menuTypeTag[row.menuType]" size="small">
            {{ menuTypeText[row.menuType] }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="icon" label="图标" width="90" align="center" />
      <el-table-column prop="path" label="路由地址" min-width="150" show-overflow-tooltip />
      <el-table-column prop="component" label="组件路径" min-width="170" show-overflow-tooltip />
      <el-table-column prop="permission" label="权限标识" min-width="150" show-overflow-tooltip />
      <el-table-column prop="sort" label="排序" width="70" align="center" />
      <el-table-column label="可见" width="70" align="center">
        <template #default="{ row }">
          <el-tag :type="row.visible ? 'success' : 'info'" size="small">
            {{ row.visible ? '是' : '否' }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column label="状态" width="80" align="center">
        <template #default="{ row }">
          <el-tag :type="row.status === 1 ? 'success' : 'danger'" size="small">
            {{ row.status === 1 ? '启用' : '停用' }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column label="操作" width="210">
        <template #default="{ row }">
          <el-button
            v-if="row.menuType !== 3"
            v-permission="'sys:menu:add'"
            link
            type="success"
            @click="openCreate(row.id)"
          >
            添加下级
          </el-button>
          <el-button v-permission="'sys:menu:edit'" link type="primary" @click="openEdit(row as MenuTree)">
            编辑
          </el-button>
          <el-button v-permission="'sys:menu:delete'" link type="danger" @click="handleDelete(row as MenuTree)">
            删除
          </el-button>
        </template>
      </el-table-column>
    </el-table>

    <!-- 新增/编辑弹窗 -->
    <el-dialog
      v-model="dialogVisible"
      :title="editingId == null ? '新增菜单' : `编辑菜单：${form.menuName}`"
      width="560px"
    >
      <el-form ref="formRef" :model="form" :rules="rules" label-width="90px">
        <el-form-item label="上级菜单">
          <el-tree-select
            v-model="form.parentId"
            :data="parentOptions"
            :props="{ label: 'menuName', children: 'children', disabled: 'disabled' }"
            node-key="id"
            check-strictly
            :render-after-expand="false"
            default-expand-all
            style="width: 100%"
          />
        </el-form-item>
        <el-form-item label="菜单类型">
          <el-radio-group v-model="form.menuType">
            <el-radio :value="MENU_TYPE.DIRECTORY">目录</el-radio>
            <el-radio :value="MENU_TYPE.MENU">菜单</el-radio>
            <el-radio :value="MENU_TYPE.BUTTON">按钮</el-radio>
          </el-radio-group>
        </el-form-item>
        <el-form-item label="菜单名称" prop="menuName">
          <el-input v-model="form.menuName" placeholder="如：用户管理" />
        </el-form-item>
        <el-form-item v-if="form.menuType !== 3" label="路由地址">
          <el-input v-model="form.path" placeholder="如：/system/user" />
        </el-form-item>
        <el-form-item v-if="form.menuType === 2" label="组件路径">
          <el-input v-model="form.component" placeholder="如：system/user/index（对应 views 目录）" />
        </el-form-item>
        <el-form-item v-if="form.menuType === 3" label="权限标识">
          <el-input v-model="form.permission" placeholder="如：sys:user:add" />
        </el-form-item>
        <el-form-item v-if="form.menuType !== 3" label="图标">
          <el-input v-model="form.icon" placeholder="如：setting" />
        </el-form-item>
        <el-form-item label="排序">
          <el-input-number v-model="form.sort" :min="0" />
        </el-form-item>
        <el-form-item v-if="form.menuType !== 3" label="是否可见">
          <el-switch v-model="form.visible" />
        </el-form-item>
        <el-form-item label="状态">
          <el-radio-group v-model="form.status">
            <el-radio :value="1">启用</el-radio>
            <el-radio :value="0">停用</el-radio>
          </el-radio-group>
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
</style>
