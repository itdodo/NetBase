// 从后端 ErrorCodes.cs 生成前端 types/errorCodes.ts 镜像（防手抄漂移）。
// 用法：node scripts/gen-error-codes-ts.cjs
const fs = require('fs');
const path = require('path');
const root = path.resolve(__dirname, '..');
const src = fs.readFileSync(path.join(root, 'src/NetBase.Common/Results/ErrorCodes.cs'), 'utf8');

const re = /\/\/\/\s*<summary>([^<]*)<\/summary>\s*public const string ([A-Z0-9_]+) = "([A-Z0-9_]+)";/g;
const groups = new Map();
let m;
while ((m = re.exec(src)) !== null) {
  const [, summary, name, value] = m;
  if (name !== value) throw new Error(`码名与值不一致: ${name} != ${value}`);
  const domain = name.split('_')[0];
  if (!groups.has(domain)) groups.set(domain, []);
  groups.get(domain).push({ summary, name });
}

let ts = `/**
 * 全局业务错误码镜像（源：src/NetBase.Common/Results/ErrorCodes.cs，本文档由 scripts/gen-error-codes-ts.cjs 生成勿手改）。
 * 随 ApiResult.errorCode 返回；配合 request.ts 的 ApiError 使用，页面按码分支时从中取值。
 * 码只增不改不删（后端为准）；重新生成：node scripts/gen-error-codes-ts.cjs
 */
export const ERROR_CODES = {
` ;
for (const [domain, codes] of groups) {
  ts += `  // ---------- ${domain} ----------\n`;
  for (const { summary, name } of codes) {
    ts += `  /** ${summary} */\n  ${name}: '${name}',\n`;
  }
  ts += '\n';
}
ts += `} as const

/** 业务错误码字面量类型 */
export type ErrorCode = (typeof ERROR_CODES)[keyof typeof ERROR_CODES]
`;

const out = path.join(root, 'web/src/types/errorCodes.ts');
fs.writeFileSync(out, ts, 'utf8');
const total = [...groups.values()].reduce((n, c) => n + c.length, 0);
console.log(`OK：${groups.size} 个域 ${total} 个码 -> web/src/types/errorCodes.ts`);
