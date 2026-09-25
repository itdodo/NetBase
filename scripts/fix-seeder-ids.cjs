// DbSeeder 裸插入补显式雪花 Id（蓝图红线 #8；PG 下 SqlSugar 对 PK 的 AOP SetValue 不生效）。
// 规则：DbSeeder.cs 内所有实体对象初始化器（new SysXxx{...} 与 BuildCrudButtons 内的 new(){...}）
// 若块内无 "Id = " 则在 { 后补 "Id = NewId(),"。校验输出各规则命中数。
const fs = require('fs');
const path = require('path');
const file = path.resolve(__dirname, '../src/NetBase.Repository/DbContexts/DbSeeder.cs');
let src = fs.readFileSync(file, 'utf8');
const crlf = src.includes('\r\n') ? '\r\n' : '\n';

function findMatchingBrace(text, openIdx) {
  let depth = 0;
  for (let i = openIdx; i < text.length; i++) {
    if (text[i] === '{') depth++;
    else if (text[i] === '}') { depth--; if (depth === 0) return i; }
  }
  return -1;
}

// 收集插入点（倒序处理避免位移）：[插入位置, 是否已有 Id]
const inserts = [];
let stats = { sys: 0, target: 0, skipped: 0 };

// 规则 A：new SysXxx { 或 new SysXxx 换行 {
const reA = /new (Sys\w+)\s*\{/g;
let m;
while ((m = reA.exec(src)) !== null) {
  const open = m.index + m[0].length - 1;
  const close = findMatchingBrace(src, open);
  if (close < 0) continue;
  const block = src.slice(open, close + 1);
  if (/\bId\s*=/.test(block)) { stats.skipped++; continue; }
  const eol = block.includes('\r\n') ? '\r\n' : (block.includes('\n') ? '\n' : ' ');
  inserts.push([open + 1, eol]);
  stats.sys++;
}

// 规则 B：BuildCrudButtons 方法体内的目标类型 new() {
const defRe = /static List<SysMenu> BuildCrudButtons/; const defMatch = defRe.exec(src); const methodStart = defMatch ? defMatch.index : -1;
if (methodStart >= 0) {
  const regionStart = src.indexOf('{', methodStart);
  const regionEnd = findMatchingBrace(src, regionStart);
  const reB = /new\(\)\s*\{/g;
  reB.lastIndex = regionStart;
  let mb;
  while ((mb = reB.exec(src)) !== null && mb.index < regionEnd) {
    const open = mb.index + mb[0].length - 1;
    const close = findMatchingBrace(src, open);
    if (close < 0 || close > regionEnd) continue;
    const block = src.slice(open, close + 1);
    if (/\bId\s*=/.test(block)) { stats.skipped++; continue; }
    const eol = block.includes('\r\n') ? '\r\n' : (block.includes('\n') ? '\n' : ' ');
    inserts.push([open + 1, eol]);
    stats.target++;
  }
}

inserts.sort((a, b) => b[0] - a[0]);
for (const [pos, eol] of inserts) {
  src = src.slice(0, pos) + `${eol}    Id = NewId(),` + src.slice(pos);
}
fs.writeFileSync(file, src, 'utf8');
console.log(`OK：Sys 实体 ${stats.sys} 处、目标类型 ${stats.target} 处补 Id，跳过已带 Id ${stats.skipped} 处`);
