<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { Bottom, Delete, Plus, Rank, Top, User as IconUser } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import type { DeptTree } from '@/api/dept'
import { getDeptTree } from '@/api/dept'
import { getRoleList } from '@/api/role'
import { getPositionList } from '@/api/position'
import type { Position } from '@/api/position'
import { getUserList } from '@/api/user'
import type { RoleSimple, User } from '@/types/api'

/**
 * 轻量流程设计器：直接编辑 FlowGraph 节点树（后端规范 DSL，免适配层）。
 * 结构：发起 → [审批/条件/抄送 节点链] → 结束。条件分支可指向链中任意节点（菱形流程）。
 * v-model：FlowGraph JSON 字符串。
 */
const props = defineProps<{ modelValue: string }>()
const emit = defineEmits<{ 'update:modelValue': [value: string] }>()

interface DesignerNode {
  code: string
  type: 'approval' | 'condition' | 'cc'
  name: string
  mode: 'counterSign' | 'orSign' | 'sequential'
  /** 会签通过比例（百分比，默认 100=全员通过；仅会签生效） */
  approveRatio: number
  /** 审批人规则（提交给后端的规范结构） */
  approvers: Array<{ type: 'user' | 'role' | 'position' | 'deptLeader'; userIds: number[]; roleCodes: string[]; positionCodes: string[]; scope: string; deptId: number }>
  branches: Array<{ name: string; variable: string; op: string; value: string; next: string }>
  defaultNext: string
  ccUserIds: number[]
  ccRoleCodes: string[]
}

const nodes = ref<DesignerNode[]>([])
const users = ref<User[]>([])
const roles = ref<RoleSimple[]>([])
const positions = ref<Position[]>([])
const deptTree = ref<DeptTree[]>([])

const editing = ref<DesignerNode | null>(null)
const editingBackup = ref<DesignerNode | null>(null)

const MODE_LABEL: Record<string, string> = { counterSign: '会签', orSign: '或签', sequential: '依次' }
const TYPE_LABEL: Record<string, string> = { approval: '审批', condition: '条件分支', cc: '抄送' }
const OPS = ['eq', 'ne', 'gt', 'gte', 'lt', 'lte', 'in', 'contains']
const OP_LABEL: Record<string, string> = {
  eq: '等于', ne: '不等于', gt: '大于', gte: '大于等于',
  lt: '小于', lte: '小于等于', in: '属于', contains: '包含'
}

onMounted(async () => {
  ;[users.value, roles.value, deptTree.value, positions.value] = await Promise.all([
    getUserList(), getRoleList(), getDeptTree(), getPositionList()
  ])
  importModel(props.modelValue)
})

function importModel(json: string): void {
  nodes.value = []
  if (!json) return
  try {
    const graph = JSON.parse(json)
    const byCode = new Map<string, any>((graph.nodes ?? []).map((n: any) => [n.code, n]))
    // 沿 start 链展开线性主链（条件分支的目标自由配置，不在线性链上展开）
    let cur: string | undefined = graph.entry === 'start' ? byCode.get('start')?.next : undefined
    const seen = new Set<string>()
    while (cur && cur !== 'end' && !seen.has(cur)) {
      seen.add(cur)
      const raw = byCode.get(cur)
      if (!raw) break
      nodes.value.push(toDesigner(raw))
      cur = raw.next
    }
  } catch {
    // 容错：解析失败按空流程处理
  }
}

function toDesigner(raw: any): DesignerNode {
  return {
    code: raw.code,
    type: raw.type,
    name: raw.name ?? '',
    mode: raw.mode ?? 'orSign',
    approvers: (raw.approvers ?? []).map((r: any) => ({
      type: r.type,
      userIds: r.userIds ?? [],
      roleCodes: r.roleCodes ?? [],
      positionCodes: r.positionCodes ?? [],
      scope: r.scope ?? 'company',
      deptId: r.deptId ?? 0
    })),
    approveRatio: raw.approveRatio ?? 100,
    branches: (raw.branches ?? []).map((b: any) => ({
      name: b.name ?? '',
      variable: b.conditions?.[0]?.variable ?? '',
      op: b.conditions?.[0]?.op ?? 'lt',
      value: b.conditions?.[0]?.value != null ? String(typeof b.conditions[0].value === 'object' ? JSON.stringify(b.conditions[0].value) : b.conditions[0].value) : '',
      next: b.next ?? ''
    })),
    defaultNext: raw.defaultNext ?? '',
    ccUserIds: raw.ccUserIds ?? [],
    ccRoleCodes: raw.ccRoleCodes ?? []
  }
}

/** 导出为后端 FlowGraph JSON（线性主链 + start/end） */
function exportModel(): string {
  const graphNodes: any[] = [{ code: 'start', type: 'start', next: nodes.value.length ? nodes.value[0].code : 'end' }]
  nodes.value.forEach((n, i) => {
    const next = i < nodes.value.length - 1 ? nodes.value[i + 1].code : 'end'
    if (n.type === 'approval') {
      graphNodes.push({
        code: n.code, type: 'approval', name: n.name, mode: n.mode, next,
        ...(n.mode === 'counterSign' && n.approveRatio < 100 ? { approveRatio: n.approveRatio } : {}),
        approvers: n.approvers.map((r) => ({
          type: r.type,
          ...(r.type === 'user' ? { userIds: r.userIds } : {}),
          ...(r.type === 'role' ? { roleCodes: r.roleCodes } : {}),
          ...(r.type === 'position'
            ? { positionCodes: r.positionCodes, scope: r.scope || 'company' }
            : {}),
          ...(r.type === 'deptLeader' ? { deptId: r.deptId } : {})
        }))
      })
    } else if (n.type === 'condition') {
      graphNodes.push({
        code: n.code, type: 'condition',
        branches: n.branches.map((b, idx) => ({
          name: b.name || `分支${idx + 1}`, priority: idx + 1, next: b.next,
          conditions: [{ variable: b.variable, op: b.op, value: parseValue(b.value) }]
        })),
        defaultNext: n.defaultNext || undefined, next
      })
    } else {
      graphNodes.push({ code: n.code, type: 'cc', next, ccUserIds: n.ccUserIds, ccRoleCodes: n.ccRoleCodes })
    }
  })
  graphNodes.push({ code: 'end', type: 'end' })
  return JSON.stringify({ nodes: graphNodes, entry: 'start' })
}

function parseValue(raw: string): unknown {
  if (raw === '') return null
  try {
    return JSON.parse(raw)
  } catch {
    return raw // 非 JSON 字面量按字符串处理
  }
}

function syncOut(): void {
  emit('update:modelValue', exportModel())
}

function newNode(type: DesignerNode['type']): DesignerNode {
  return {
    code: `n_${Date.now().toString(36)}${Math.floor(Math.random() * 1000)}`,
    type,
    name: type === 'approval' ? '审批' : type === 'condition' ? '条件分支' : '抄送',
    mode: 'orSign',
    approveRatio: 100,
    approvers: type === 'approval' ? [{ type: 'user', userIds: [], roleCodes: [], positionCodes: [], scope: 'company', deptId: 0 }] : [],
    branches: type === 'condition'
      ? [{ name: '', variable: 'amount', op: 'lt', value: '', next: '' }]
      : [],
    defaultNext: '',
    ccUserIds: [],
    ccRoleCodes: []
  }
}

/** el-dropdown command 回调（模板内不做类型运算） */
function onAddCommand(cmd: string | number | object, index: number): void {
  addNode(cmd as DesignerNode['type'], index)
}

function addNode(type: DesignerNode['type'], index: number): void {
  const node = newNode(type)
  nodes.value.splice(index, 0, node)
  openEdit(node)
}

function removeNode(index: number): void {
  nodes.value.splice(index, 1)
  syncOut()
}

function move(index: number, delta: number): void {
  const target = index + delta
  if (target < 0 || target >= nodes.value.length) return
  const [n] = nodes.value.splice(index, 1)
  nodes.value.splice(target, 0, n)
  syncOut()
}

function openEdit(node: DesignerNode): void {
  editingBackup.value = JSON.parse(JSON.stringify(node))
  editing.value = node
}

function saveEdit(): void {
  const node = editing.value
  if (!node) return
  if (node.type === 'approval') {
    if (node.approvers.length === 0) {
      ElMessage.warning('请至少配置一条审批人规则')
      return
    }
    for (const r of node.approvers) {
      if (r.type === 'user' && r.userIds.length === 0) {
        ElMessage.warning('请选择指定成员')
        return
      }
      if (r.type === 'role' && r.roleCodes.length === 0) {
        ElMessage.warning('请选择角色')
        return
      }
      if (r.type === 'position' && r.positionCodes.length === 0) {
        ElMessage.warning('请选择岗位')
        return
      }
    }
  }
  if (node.type === 'condition') {
    for (const b of node.branches) {
      if (!b.variable || !b.next) {
        ElMessage.warning('分支的变量与目标节点不能为空')
        return
      }
    }
  }
  syncOut()
  editing.value = null
}

function cancelEdit(): void {
  if (editing.value && editingBackup.value) {
    Object.assign(editing.value, editingBackup.value)
  }
  editing.value = null
}

function addRule(node: DesignerNode): void {
  node.approvers.push({ type: 'user', userIds: [], roleCodes: [], positionCodes: [], scope: 'company', deptId: 0 })
}

function addBranch(node: DesignerNode): void {
  node.branches.push({ name: '', variable: '', op: 'eq', value: '', next: '' })
}

/** 条件分支/默认分支可指向的可选目标：链中任意节点 */
const targetOptions = computed(() =>
  nodes.value.map((n, i) => ({ label: `${i + 1}. ${TYPE_LABEL[n.type]}：${n.name}`, value: n.code }))
)

function nodeSummary(n: DesignerNode): string {
  if (n.type === 'approval') {
    const parts = n.approvers.map((r) =>
      r.type === 'user' ? `成员×${r.userIds.length}`
        : r.type === 'role' ? `角色×${r.roleCodes.length}`
        : r.type === 'position' ? `岗位×${r.positionCodes.length}`
        : '部门主管')
    const ratio = n.mode === 'counterSign' && n.approveRatio < 100 ? `（${n.approveRatio}% 通过）` : ''
    return `${MODE_LABEL[n.mode]}${ratio} · ${parts.join(' / ')}`
  }
  if (n.type === 'condition') {
    return `${n.branches.length} 个分支`
  }
  const parts: string[] = []
  if (n.ccUserIds.length) parts.push(`成员×${n.ccUserIds.length}`)
  if (n.ccRoleCodes.length) parts.push(`角色×${n.ccRoleCodes.length}`)
  return parts.join(' / ') || '未配置'
}

const userName = (id: number) => users.value.find((u) => u.id === id)?.nickName || users.value.find((u) => u.id === id)?.userName || `用户${id}`
const roleName = (code: string) => roles.value.find((r) => r.roleCode === code)?.roleName ?? code
const positionName = (code: string) => positions.value.find((p) => p.positionCode === code)?.positionName ?? code

defineExpose({
  /** 供外层保存前校验：审批节点必须配置审批人规则 */
  validate(): string | null {
    for (const n of nodes.value) {
      if (n.type === 'approval' && n.approvers.length === 0) {
        return `审批节点「${n.name}」未配置审批人规则`
      }
    }
    return null
  }
})
</script>

<template>
  <div class="flow-designer">
    <!-- 发起节点 -->
    <div class="node start">
      <el-avatar :size="36" class="avatar start-avatar"><el-icon><IconUser /></el-icon></el-avatar>
      <div class="node-body">
        <div class="node-title">发起人</div>
        <div class="node-desc">谁提交单据，流程从这里开始</div>
      </div>
    </div>

    <template v-for="(n, index) in nodes" :key="n.code">
      <!-- 插入按钮 -->
      <div class="insert-row">
        <el-dropdown trigger="click" @command="(cmd) => onAddCommand(cmd, index)">
          <el-button circle type="primary" plain size="small" :icon="Plus" />
          <template #dropdown>
            <el-dropdown-menu>
              <el-dropdown-item command="approval">审批节点</el-dropdown-item>
              <el-dropdown-item command="condition">条件分支</el-dropdown-item>
              <el-dropdown-item command="cc">抄送</el-dropdown-item>
            </el-dropdown-menu>
          </template>
        </el-dropdown>
      </div>

      <div class="node" :class="`node-${n.type}`">
        <div class="node-body clickable" @click="openEdit(n)">
          <div class="node-title">
            {{ index + 1 }}. {{ TYPE_LABEL[n.type] }}：{{ n.name }}
            <el-tag v-if="n.type === 'approval'" size="small" type="info">{{ MODE_LABEL[n.mode] }}</el-tag>
          </div>
          <div class="node-desc">{{ nodeSummary(n) }}</div>
        </div>
        <div class="node-tools">
          <el-button link size="small" :icon="Top" @click.stop="move(index, -1)" />
          <el-button link size="small" :icon="Bottom" @click.stop="move(index, 1)" />
          <el-button link size="small" type="danger" :icon="Delete" @click.stop="removeNode(index)" />
        </div>
      </div>
    </template>

    <!-- 末尾插入 -->
    <div class="insert-row">
      <el-dropdown trigger="click" @command="(cmd) => onAddCommand(cmd, nodes.length)">
        <el-button circle type="primary" size="small" :icon="Plus" />
        <template #dropdown>
          <el-dropdown-menu>
            <el-dropdown-item command="approval">审批节点</el-dropdown-item>
            <el-dropdown-item command="condition">条件分支</el-dropdown-item>
            <el-dropdown-item command="cc">抄送</el-dropdown-item>
          </el-dropdown-menu>
        </template>
      </el-dropdown>
    </div>

    <!-- 结束节点 -->
    <div class="node end">
      <el-avatar :size="36" class="avatar end-avatar"><el-icon><Rank /></el-icon></el-avatar>
      <div class="node-body">
        <div class="node-title">流程结束</div>
        <div class="node-desc">全部节点通过后回调业务单据</div>
      </div>
    </div>

    <!-- 节点编辑 -->
    <el-dialog
      :model-value="editing != null"
      :title="`编辑${editing ? TYPE_LABEL[editing.type] : ''}节点`"
      width="640px"
      append-to-body
      @close="cancelEdit"
    >
      <el-form v-if="editing" label-width="90px">
          <el-form-item label="节点名称">
            <el-input v-model="editing.name" maxlength="50" />
          </el-form-item>

          <template v-if="editing.type === 'approval'">
            <el-form-item label="审批方式">
              <el-radio-group v-model="editing.mode">
                <el-radio value="orSign">或签（一人通过即过）</el-radio>
                <el-radio value="counterSign">会签（全部通过才过）</el-radio>
                <el-radio value="sequential">依次审批</el-radio>
              </el-radio-group>
            </el-form-item>
            <el-form-item v-if="editing.mode === 'counterSign'" label="通过比例">
              <el-input-number
                v-model="editing.approveRatio"
                :min="1"
                :max="100"
                style="width: 140px"
              />
              <span class="ratio-tip">% 通过即节点通过（100 = 全员）</span>
            </el-form-item>
            <el-form-item
              v-for="(rule, ri) in editing.approvers"
              :key="ri"
              :label="ri === 0 ? '审批人' : ''"
            >
              <div class="rule-row">
                <el-select v-model="rule.type" style="width: 130px" @change="rule.userIds = []; rule.roleCodes = []; rule.positionCodes = []">
                  <el-option label="指定成员" value="user" />
                  <el-option label="指定角色" value="role" />
                  <el-option label="指定岗位" value="position" />
                  <el-option label="部门主管" value="deptLeader" />
                </el-select>
                <el-select v-if="rule.type === 'user'" v-model="rule.userIds" multiple filterable placeholder="选择成员" style="flex: 1">
                  <el-option v-for="u in users" :key="u.id" :label="u.nickName || u.userName" :value="u.id" />
                </el-select>
                <el-select v-if="rule.type === 'role'" v-model="rule.roleCodes" multiple filterable placeholder="选择角色" style="flex: 1">
                  <el-option v-for="r in roles" :key="r.roleCode" :label="r.roleName" :value="r.roleCode" />
                </el-select>
                <el-select v-if="rule.type === 'position'" v-model="rule.positionCodes" multiple filterable placeholder="选择岗位" style="width: 150px">
                  <el-option v-for="p in positions" :key="p.positionCode" :label="p.positionName" :value="p.positionCode" />
                </el-select>
                <el-select
                  v-if="rule.type === 'position'"
                  v-model="rule.scope"
                  style="width: 150px"
                  title="岗位审批范围"
                >
                  <el-option label="全公司" value="company" />
                  <el-option label="发起人所在部门" value="submitterDept" />
                </el-select>
                <template v-if="rule.type === 'deptLeader'">
                  <el-tree-select
                    v-model="rule.deptId"
                    :data="[{ id: 0, deptName: '发起人所在部门', children: deptTree }]"
                    :props="{ label: 'deptName', children: 'children' }"
                    node-key="id"
                    check-strictly
                    style="flex: 1"
                  />
                </template>
                <el-button link type="danger" :icon="Delete" @click="editing.approvers.splice(ri, 1)" />
              </div>
              <div v-if="rule.type === 'user' && rule.userIds.length" class="rule-preview">
                {{ rule.userIds.map(userName).join('、') }}
              </div>
              <div v-if="rule.type === 'role' && rule.roleCodes.length" class="rule-preview">
                {{ rule.roleCodes.map(roleName).join('、') }}
              </div>
              <div v-if="rule.type === 'position' && rule.positionCodes.length" class="rule-preview">
                {{ rule.positionCodes.map(positionName).join('、') }}
              </div>
            </el-form-item>
            <el-form-item label=" ">
              <el-button link type="primary" :icon="Plus" @click="addRule(editing)">添加审批人规则（并集）</el-button>
            </el-form-item>
          </template>

          <template v-if="editing.type === 'condition'">
            <el-form-item v-for="(b, bi) in editing.branches" :key="bi" :label="bi === 0 ? '分支' : ''">
              <div class="branch-row">
                <el-input v-model="b.name" placeholder="分支名" style="width: 90px" />
                <el-input v-model="b.variable" placeholder="变量名" style="width: 110px" />
                <el-select v-model="b.op" style="width: 100px">
                  <el-option v-for="op in OPS" :key="op" :label="OP_LABEL[op]" :value="op" />
                </el-select>
                <el-input v-model="b.value" placeholder="比较值" style="width: 100px" />
                <el-select v-model="b.next" placeholder="命中跳转" style="flex: 1">
                  <el-option v-for="opt in targetOptions" :key="opt.value" :label="opt.label" :value="opt.value" />
                </el-select>
                <el-button link type="danger" :icon="Delete" @click="editing.branches.splice(bi, 1)" />
              </div>
            </el-form-item>
            <el-form-item label=" ">
              <el-button link type="primary" :icon="Plus" @click="addBranch(editing)">添加分支</el-button>
            </el-form-item>
            <el-form-item label="默认分支">
              <el-select v-model="editing.defaultNext" placeholder="全不命中时跳转（建议必选）" clearable style="width: 100%">
                <el-option v-for="opt in targetOptions" :key="opt.value" :label="opt.label" :value="opt.value" />
              </el-select>
            </el-form-item>
          </template>

          <template v-if="editing.type === 'cc'">
            <el-form-item label="抄送成员">
              <el-select v-model="editing.ccUserIds" multiple filterable placeholder="选择成员" style="width: 100%">
                <el-option v-for="u in users" :key="u.id" :label="u.nickName || u.userName" :value="u.id" />
              </el-select>
            </el-form-item>
            <el-form-item label="抄送角色">
              <el-select v-model="editing.ccRoleCodes" multiple filterable placeholder="选择角色" style="width: 100%">
                <el-option v-for="r in roles" :key="r.roleCode" :label="r.roleName" :value="r.roleCode" />
              </el-select>
            </el-form-item>
          </template>
      </el-form>
      <template #footer>
        <el-button @click="cancelEdit">取消</el-button>
        <el-button type="primary" @click="saveEdit">确定</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.flow-designer {
  display: flex;
  flex-direction: column;
  align-items: center;
  max-height: 62vh;
  overflow: auto;
  padding: 8px 0;
}

.node {
  width: 320px;
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 10px 12px;
  border: 1px solid var(--el-border-color-light);
  border-radius: 8px;
  background: var(--el-bg-color);
}

.node.clickable {
  cursor: pointer;
}

.node-body {
  flex: 1;
  min-width: 0;
}

.node-title {
  font-weight: 600;
  display: flex;
  align-items: center;
  gap: 6px;
}

.node-desc {
  margin-top: 2px;
  color: var(--el-text-color-secondary);
  font-size: 12px;
}

.node-tools {
  display: flex;
  flex-direction: column;
}

.node-condition {
  border-style: dashed;
  border-color: var(--el-color-warning-light-5);
}

.insert-row {
  padding: 8px 0;
  display: flex;
  justify-content: center;
}

.ratio-tip {
  margin-left: 8px;
  color: var(--el-text-color-secondary);
  font-size: 12px;
}

.rule-row,
.branch-row {
  display: flex;
  gap: 6px;
  align-items: center;
  width: 100%;
  margin-bottom: 4px;
}

.rule-preview {
  width: 100%;
  color: var(--el-text-color-secondary);
  font-size: 12px;
}
</style>
