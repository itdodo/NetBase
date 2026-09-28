<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Plus, Refresh, Search } from '@element-plus/icons-vue'
import type { Contract } from '@/api/biz/contract'
import {
  getContractDetail,
  createContract,
  updateContract,
  deleteContract
} from '@/api/biz/contract'
import { formatDateTime } from '@/utils/format'
import { usePageList } from '@/composables/usePageList'

defineOptions({ name: 'BizContractView' })

const {
  loading,
  list,
  total,
  query,
  loadData,
  handleSearch
} = usePageList<Contract, { pageIndex: number; pageSize: number; contractName?: string; }>({
  url: '/biz/contract/page',
  defaultQuery: { pageIndex: 1, pageSize: 10, contractName: undefined }
})

function handleReset(): void {

  query.contractName = undefined

  handleSearch()
}

// ---------- 新增 / 编辑 ----------
const dialogVisible = ref(false)
const saving = ref(false)
const editingId = ref<string | null>(null)
const formRef = ref()
const form = reactive<Record<string, any>>({

  contractName: '',

  amount: 0,

  signDate: undefined,

  remark: '',

  deptId: '',

  ownerUserId: '',

  status: '',

  version: 0
})

function openCreate(): void {
  editingId.value = null

  form.contractName = ''

  form.amount = 0

  form.signDate = undefined

  form.remark = ''

  form.deptId = ''

  form.ownerUserId = ''

  form.status = ''

  form.version = 0
  dialogVisible.value = true
}

async function openEdit(row: Contract): Promise<void> {
  editingId.value = row.id
  // 打开编辑即拉最新详情：版本取最新值，缩小并发冲突窗口
  const src = await getContractDetail(row.id).catch(() => row)
  Object.assign(form, {

    contractName: src.contractName,

    amount: src.amount,

    signDate: src.signDate,

    remark: src.remark,

    deptId: src.deptId,

    ownerUserId: src.ownerUserId,

    status: src.status,

    version: src.version
  })
  dialogVisible.value = true
}

async function handleSave(): Promise<void> {
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid) return
  if (editingId.value == null) {
    await createContract({ ...form } as any)
    ElMessage.success('创建成功')
  } else {
    await updateContract(editingId.value, { ...form } as any)
    ElMessage.success('更新成功')
  }
  dialogVisible.value = false
  loadData()
}


async function handleDelete(row: Contract): Promise<void> {
  await ElMessageBox.confirm(`确定删除该ContractMgr记录吗？`, '提示', { type: 'warning' })
  await deleteContract(row.id)
  ElMessage.success('删除成功')
  loadData()
}

onMounted(loadData)
</script>

<template>
  <el-card>
    <div class="toolbar">


      <el-input v-model="query.contractName" placeholder="合同名称" clearable style="width: 200px" :prefix-icon="Search" @keyup.enter="handleSearch" />


      <el-button type="primary" :icon="Search" @click="handleSearch">查询</el-button>
      <el-button :icon="Refresh" @click="handleReset">重置</el-button>
      <el-button v-permission="'biz:contract:add'" type="primary" :icon="Plus" @click="openCreate">新增</el-button>
    </div>

    <el-table v-loading="loading" :data="list" border stripe>


      <el-table-column prop="contractName" label="合同名称" min-width="140" show-overflow-tooltip />



      <el-table-column prop="amount" label="合同金额" min-width="140" show-overflow-tooltip />



      <el-table-column prop="signDate" label="签订日期" width="165" :formatter="formatDateTime" />



      <el-table-column prop="remark" label="备注" min-width="140" show-overflow-tooltip />



      <el-table-column prop="deptId" label="归属部门" min-width="140" show-overflow-tooltip />



      <el-table-column prop="ownerUserId" label="归属用户" min-width="140" show-overflow-tooltip />



      <el-table-column prop="status" label="单据状态" min-width="140" show-overflow-tooltip />


      <el-table-column label="操作" width="150" align="center">
        <template #default="{ row }">
          <el-button v-permission="'biz:contract:edit'" link type="primary" @click="openEdit(row as Contract)">编辑</el-button>
          <el-button v-permission="'biz:contract:delete'" link type="danger" @click="handleDelete(row as Contract)">删除</el-button>

        </template>
      </el-table-column>
    </el-table>

    <el-pagination
      v-model:current-page="query.pageIndex"
      v-model:page-size="query.pageSize"
      class="pagination"
      background
      layout="total, prev, pager, next"
      :total="total"
      @current-change="loadData"
    />

    <el-dialog v-model="dialogVisible" :title="editingId == null ? '新增ContractMgr' : '编辑ContractMgr'" width="640px" class="dialog-scroll">
      <el-form ref="formRef" :model="form" label-width="110px">


        <el-form-item label="合同名称">
          <el-input v-model="form.contractName" maxlength="200" />
        </el-form-item>



        <el-form-item label="合同金额">
          <el-input-number v-model="form.amount" :controls="false" style="width: 100%" />
        </el-form-item>



        <el-form-item label="签订日期">
          <el-date-picker v-model="form.signDate" type="date" value-format="YYYY-MM-DD" style="width: 100%" />
        </el-form-item>



        <el-form-item label="备注">
          <el-input v-model="form.remark" maxlength="500" />
        </el-form-item>



        <el-form-item label="归属部门">
          <el-input v-model="form.deptId" maxlength="0" />
        </el-form-item>



        <el-form-item label="归属用户">
          <el-input v-model="form.ownerUserId" maxlength="0" />
        </el-form-item>



        <el-form-item label="单据状态">
          <el-input v-model="form.status" maxlength="0" />
        </el-form-item>


      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="saving" @click="handleSave">保存</el-button>
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
