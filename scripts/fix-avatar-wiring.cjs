const fs = require('fs');

// 1) flow.ts：operatorName 行后插 avatar（行数组方式，兼容 CRLF）
{
  const file = 'web/src/api/flow.ts';
  const lines = fs.readFileSync(file, 'utf8').split(/\r?\n/);
  const idx = lines.findIndex((l) => l.trim() === 'operatorName: string');
  if (idx < 0) throw new Error('operatorName line not found');
  if (!lines.some((l) => l.includes('avatar?: string'))) {
    lines.splice(idx + 1, 0, '  /** 操作人头像（无则前端降级首字） */', '  avatar?: string');
    fs.writeFileSync(file, lines.join('\r\n'));
    console.log('flow.ts avatar added');
  } else console.log('flow.ts already has avatar');
}

// 2) profile：ElMessage.success('头像已更新') 行后插 setAvatar
{
  const file = 'web/src/views/profile/index.vue';
  const lines = fs.readFileSync(file, 'utf8').split(/\r?\n/);
  const idx = lines.findIndex((l) => l.includes("ElMessage.success('头像已更新')"));
  if (idx < 0) throw new Error('profile anchor line not found');
  if (!lines.some((l) => l.includes('userStore.setAvatar'))) {
    lines.splice(idx + 1, 0, '  userStore.setAvatar(url) // 顶栏等全站位置即时生效');
    fs.writeFileSync(file, lines.join('\r\n'));
    console.log('profile setAvatar inserted');
  } else console.log('profile already has setAvatar');
}
