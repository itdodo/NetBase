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
  firstLoading,
  list,
  total,
  query,
  loadData,
  handleSearch
} = usePageList<Contract, { pageIndex: number; pageSize: number; }>({
  url: '/biz/contract/page',
  defaultQuery: { pageIndex: 1, pageSize: 10 }
})

function handleReset(): void {

  handleSearch()
}

// ---------- 新增 / 编辑 ----------
const dialogVisible = ref(false)
const saving = ref(false)
const editingId = ref<string | null>(null)
const formRef = ref()
const form = reactive<Record<string, any>>({

  amount: 0,

  remark: '',

  contractName: '',

  signDate: undefined,

  version: 0
})



function openCreate(): void {
  editingId.value = null

  form.amount = 0

  form.remark = ''

  form.contractName = ''

  form.signDate = undefined

  form.version = 0

  dialogVisible.value = true
}

async function openEdit(row: Contract): Promise<void> {
  editingId.value = row.id
  // 打开编辑即拉最新详情：版本取最新值，缩小并发冲突窗口
  const src = await getContractDetail(row.id).catch(() => row)
  Object.assign(form, {

    amount: src.amount,

    remark: src.remark,

    contractName: src.contractName,

    signDate: src.signDate,

    version: src.version
  })
  dialogVisible.value = true
}

async function handleSave(): Promise<void> {
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid) return

  const payload = {
    ...form

  }
  if (editingId.value == null) {
    await createContract(payload as any)
    ElMessage.success('创建成功')
  } else {
    await updateContract(editingId.value, payload as any)
    ElMessage.success('更新成功')
  }
  dialogVisible.value = false
  loadData()
}


async function handleDelete(row: Contract): Promise<void> {
  await ElMessageBox.confirm(`确定删除该合同管理记录吗？`, '提示', { type: 'warning' })
  await deleteContract(row.id)
  ElMessage.success('删除成功')
  loadData()
}

onMounted(loadData)
  import TableEmpty from '@/components/TableEmpty.vue'
  import TableSkeleton from '@/components/TableSkeleton.vue'
</script>

<template>
  <el-card>
    <div class="toolbar">

      <el-button type="primary" :icon="Search" @click="handleSearch">查询</el-button>
      <el-button :icon="Refresh" @click="handleReset">重置</el-button>
      <el-button v-permission="'biz:contract:add'" type="primary" :icon="Plus" @click="openCreate">新增</el-button>
    </div>

    <TableSkeleton v-if="firstLoading" />
      <el-table v-else v-loading="loading" :data="list" border stripe>


      <el-table-column prop="amount" label="合同金额" min-width="140" show-overflow-tooltip />



      <el-table-column prop="remark" label="备注" min-width="140" show-overflow-tooltip />



      <el-table-column prop="contractName" label="合同名称" min-width="140" show-overflow-tooltip />



      <el-table-column prop="signDate" label="签订日期" width="165" :formatter="formatDateTime" />


      <el-table-column label="操作" width="150" align="center">
        <template #default="{ row }">
          <el-button v-permission="'biz:contract:edit'" link type="primary" @click="openEdit(row as Contract)">编辑</el-button>
          <el-button v-permission="'biz:contract:delete'" link type="danger" @click="handleDelete(row as Contract)">删除</el-button>

        </template>
      </el-table-column>
    <template #empty>
      <TableEmpty />
    </template>
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

    <el-dialog v-model="dialogVisible" :title="editingId == null ? '新增合同管理' : '编辑合同管理'" width="640px" class="dialog-scroll">
      <el-form ref="formRef" :model="form" label-width="110px">


        <el-form-item label="合同金额">
          <el-input v-model="form.amount" maxlength="0" />
        </el-form-item>



        <el-form-item label="备注">
          <el-input v-model="form.remark" maxlength="500" />
        </el-form-item>



        <el-form-item label="合同名称">
          <el-input v-model="form.contractName" maxlength="200" />
        </el-form-item>



        <el-form-item label="签订日期">
          <el-input v-model="form.signDate" maxlength="0" />
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
