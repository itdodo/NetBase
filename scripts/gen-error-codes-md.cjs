// 从后端 ErrorCodes.cs 生成 docs/错误码.md 总表（与代码零漂移）。
// 用法：node scripts/gen-error-codes-md.cjs
const fs = require('fs');
const path = require('path');
const root = path.resolve(__dirname, '..');
const src = fs.readFileSync(path.join(root, 'src/NetBase.Common/Results/ErrorCodes.cs'), 'utf8');

const re = /\/\/\/\s*<summary>([^<]*)<\/summary>\s*public const string ([A-Z0-9_]+) = "([A-Z0-9_]+)";/g;
const domains = [
  ['COMMON', '通用'],
  ['AUTH', '认证与会话'],
  ['SYS_USER', '用户（sys_user）'],
  ['SYS_ROLE', '角色（sys_role）'],
  ['SYS_MENU', '菜单（sys_menu）'],
  ['SYS_DEPT', '部门（sys_dept）'],
  ['SYS_POSITION', '岗位（sys_position）'],
  ['SYS_DICT', '字典（sys_dict）'],
  ['SYS_CONFIG', '参数（sys_config）'],
  ['SYS_NOTICE', '公告（sys_notice）'],
  ['SYS_MESSAGE', '站内信（sys_message）'],
  ['SYS_FILE', '文件（sys_file）'],
  ['FLOW', '审批流（sys_flow_*）'],
  ['BIZ', '业务单据样板（biz_*）'],
];
const all = [];
let m;
while ((m = re.exec(src)) !== null) {
  all.push({ summary: m[1].trim(), name: m[2], value: m[3] });
}

function table(codes) {
  let t = '| 错误码 | 说明 | HTTP | 场景 |\n|---|---|---|---|\n';
  for (const c of codes) t += `| \`${c.name}\` | ${c.summary} | 见 code | 见说明 |\n`;
  return t;
}

let md = `# 全局业务错误码总表

> 生成源：\`src/NetBase.Common/Results/ErrorCodes.cs\`（本文档由 \`scripts/gen-error-codes-md.cjs\` 生成，勿手改表格；重新生成：\`node scripts/gen-error-codes-md.cjs\`）。

## 返回结构与约定

统一返回体（所有接口，含管道 401/403 与未匹配路由 404）：

\`\`\`json
{ "code": 200, "message": "操作成功", "errorCode": null, "data": { }, "timestamp": 1727180000000 }
\`\`\`

- **code**（int）：传输语义。200 成功；400 参数/业务规则错误（业务异常默认值）；401 未认证；403 无权限；404 资源不存在；409 乐观锁并发冲突；500 系统级故障（仅未预期异常）。业务错误 HTTP 状态恒为 200（body.code 承载语义）；管道层 401/403/404 为真实 HTTP 状态码 + 同结构 body。
- **errorCode**（string）：全局业务错误码，失败时携带，前端按码分支（成功时为 null 不序列化）。
- **message**：中文提示，可直接展示；同一码的文案可随场景调整（码是契约，文案不是）。

## 使用规范

1. **新增业务异常必须先查本表选码**：\`throw new BusinessException("提示文案", ErrorCodes.XXX);\`——带码即 code=400；需要非 400 用 \`BusinessException("文案", ApiResultCode.XXX, ErrorCodes.YYY)\`
2. **码只增不改不删**：码是前后端契约，语义变更=新码
3. 命名 \`{域}_{对象}_{原因}\`；新增模块先在本文件与 ErrorCodes.cs 加域分组
4. 前端按码分支：\`catch (e) { if (e.errorCode === ERROR_CODES.COMMON_CONCURRENCY_CONFLICT) ... }\`；页面自行处理某码时传 \`silentErrorCodes\` 跳过全局提示（见 \`web/src/api/request.ts\` 的 ApiError）
5. 前端码表镜像 \`web/src/types/errorCodes.ts\` 由脚本生成，后端加码后运行 \`node scripts/gen-error-codes-ts.cjs\` 同步

---

`;
for (const [prefix, title] of domains) {
  const codes = all.filter((c) => c.name.startsWith(prefix + '_'));
  if (codes.length === 0) continue;
  md += `## ${title}（${codes.length} 个）\n\n${table(codes)}`;
}
md += `\n---\n\n共 ${all.length} 个码。兜底：未赋码业务异常自动落 \`COMMON_BUSINESS_ERROR\`（code=400）；\`GetRequiredAsync\` 未传实体码落 \`COMMON_NOT_FOUND\`。\n`;

fs.writeFileSync(path.join(root, 'docs/错误码.md'), md, 'utf8');
console.log(`OK：docs/错误码.md 生成，共 ${all.length} 个码`);
