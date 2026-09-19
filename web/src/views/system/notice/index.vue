<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Plus, Refresh, Search } from '@element-plus/icons-vue'
import RichTextEditor from '@/components/RichTextEditor.vue'
import { sanitizeHtml } from '@/utils/sanitize'
import type { NoticeInfo } from '@/api/notice'
import { createNotice, deleteNotice, updateNotice } from '@/api/notice'
import { formatDateTime } from '@/utils/format'
import { usePageList } from '@/composables/usePageList'

defineOptions({ name: 'SystemNoticeView' })

const NOTICE_TYPE: Record<number, string> = { 1: '通知', 2: '公告' }

const {
  loading,
  list,
  total,
  query,
  loadData,
  handleSearch
} = usePageList<NoticeInfo, { pageIndex: number; pageSize: number; keyword: string; noticeType?: number }>({
  url: '/sys/notice/page',
  defaultQuery: { pageIndex: 1, pageSize: 10, keyword: '', noticeType: undefined }
})

function handleReset(): void {
  query.keyword = ''
  query.noticeType = undefined
  handleSearch()
}

// ---------- 新增 / 编辑 ----------
const dialogVisible = ref(false)
const saving = ref(false)
const editingId = ref<number | null>(null)
const formRef = ref()
const form = ref({
  title: '',
  noticeType: 1,
  content: '',
  status: 1,
  publishTime: ''
})
const rules = {
  title: [{ required: true, message: '请输入标题', trigger: 'blur' }],
  content: [{ required: true, message: '请输入内容', trigger: 'blur' }]
}

function openCreate(): void {
  editingId.value = null
  form.value = { title: '', noticeType: 1, content: '', status: 1, publishTime: '' }
  dialogVisible.value = true
}

function openEdit(row: NoticeInfo): void {
  editingId.value = row.id
  form.value = {
    title: row.title,
    noticeType: row.noticeType,
    content: row.content,
    status: row.status,
    publishTime: row.publishTime ?? ''
  }
  dialogVisible.value = true
}

async function handleSave(): Promise<void> {
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid) return
  if (form.value.status === 2 && !form.value.publishTime) {
    ElMessage.warning('定时发布请选择发布时间')
    return
  }
  saving.value = true
  try {
    if (editingId.value == null) {
      await createNotice({ ...form.value })
      ElMessage.success('创建成功')
    } else {
      await updateNotice(editingId.value, { ...form.value })
      ElMessage.success('更新成功')
    }
    dialogVisible.value = false
    loadData()
  } finally {
    saving.value = false
  }
}

async function handleDelete(row: NoticeInfo): Promise<void> {
  await ElMessageBox.confirm(`确定删除「${row.title}」吗？`, '提示', { type: 'warning' })
  await deleteNotice(row.id)
  ElMessage.success('删除成功')
  loadData()
}

// ---------- 查看详情 ----------
const detailVisible = ref(false)
const current = ref<NoticeInfo | null>(null)

function openDetail(row: NoticeInfo): void {
  current.value = row
  detailVisible.value = true
}

onMounted(loadData)
</script>

<template>
  <el-card>
    <div class="toolbar">
      <el-input
        v-model="query.keyword"
        placeholder="标题关键字"
        clearable
        style="width: 200px"
        :prefix-icon="Search"
        @keyup.enter="handleSearch"
      />
      <el-select v-model="query.noticeType" placeholder="类型" clearable style="width: 110px">
        <el-option label="通知" :value="1" />
        <el-option label="公告" :value="2" />
      </el-select>
      <el-button type="primary" :icon="Search" @click="handleSearch">查询</el-button>
      <el-button :icon="Refresh" @click="handleReset">重置</el-button>
      <el-button v-permission="'sys:notice:add'" type="primary" :icon="Plus" @click="openCreate">新建公告</el-button>
    </div>

    <el-table v-loading="loading" :data="list" border stripe>
      <el-table-column prop="title" label="标题" min-width="220" show-overflow-tooltip>
        <template #default="{ row }">
          <el-link type="primary" @click="openDetail(row as NoticeInfo)">{{ (row as NoticeInfo).title }}</el-link>
        </template>
      </el-table-column>
      <el-table-column label="类型" width="80" align="center">
        <template #default="{ row }">
          <el-tag :type="(row as NoticeInfo).noticeType === 2 ? 'warning' : 'primary'" size="small">
            {{ NOTICE_TYPE[(row as NoticeInfo).noticeType] ?? '通知' }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column label="状态" width="80" align="center">
        <template #default="{ row }">
          <el-tag :type="(row as NoticeInfo).status === 1 ? 'success' : (row as NoticeInfo).status === 2 ? 'warning' : 'info'" size="small">
            {{ (row as NoticeInfo).status === 1 ? '发布' : (row as NoticeInfo).status === 2 ? '定时中' : '停用' }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="createBy" label="发布人" min-width="100" />
      <el-table-column prop="createTime" label="发布时间" width="165" :formatter="formatDateTime" />
      <el-table-column label="操作" width="140" align="center">
        <template #default="{ row }">
          <el-button v-permission="'sys:notice:edit'" link type="primary" @click="openEdit(row as NoticeInfo)">编辑</el-button>
          <el-button v-permission="'sys:notice:delete'" link type="danger" @click="handleDelete(row as NoticeInfo)">删除</el-button>
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

    <el-dialog
      v-model="dialogVisible"
      :title="editingId == null ? '新建公告' : '编辑公告'"
      width="860px"
      class="dialog-scroll"
    >
      <el-form ref="formRef" :model="form" :rules="rules" label-position="top">
        <el-form-item prop="title">
          <el-input v-model="form.title" maxlength="100" placeholder="请输入公告标题" size="large" />
        </el-form-item>
        <el-form-item label="类型">
          <el-radio-group v-model="form.noticeType">
            <el-radio :value="1">通知</el-radio>
            <el-radio :value="2">公告</el-radio>
          </el-radio-group>
        </el-form-item>
        <el-form-item label="状态">
          <el-radio-group v-model="form.status">
            <el-radio :value="1">立即发布</el-radio>
            <el-radio :value="2">定时发布</el-radio>
            <el-radio :value="0">存为停用</el-radio>
          </el-radio-group>
        </el-form-item>
        <el-form-item v-if="form.status === 2" label="定时发布时间">
          <el-date-picker
            v-model="form.publishTime"
            type="datetime"
            placeholder="到达时间后自动发布"
            value-format="YYYY-MM-DDTHH:mm:ss"
            style="width: 100%"
          />
        </el-form-item>
        <el-form-item prop="content">
          <RichTextEditor v-if="dialogVisible" v-model="form.content" :height="380" placeholder="请输入公告正文…" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="saving" @click="handleSave">保存</el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="detailVisible" :title="current?.title" width="560px">
      <template v-if="current">
        <div class="detail-meta">
          <el-tag :type="current.noticeType === 2 ? 'warning' : 'primary'" size="small">
            {{ NOTICE_TYPE[current.noticeType] ?? '通知' }}
          </el-tag>
          <span>{{ current.createBy }}</span>
          <span>{{ formatDateTime(null, null, current.createTime) }}</span>
        </div>
        <div class="detail-content rich" v-html="sanitizeHtml(current.content)" />
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

.detail-meta {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 12px;
  color: var(--el-text-color-secondary);
  font-size: 13px;
}

.detail-content {
  line-height: 1.7;
  color: var(--el-text-color-regular);
}

.detail-content.rich :deep(img) {
  max-width: 100%;
}
</style>
