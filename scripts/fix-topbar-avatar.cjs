const fs = require('fs');
const file = 'web/src/layout/index.vue';
const lines = fs.readFileSync(file, 'utf8').split(/\r?\n/);
if (lines.some((l) => l.includes('user-avatar'))) {
  console.log('already has avatar');
} else {
  const idx = lines.findIndex((l) => l.includes('class="user-info"'));
  if (idx < 0) throw new Error('user-info span not found');
  // 在 <span class="user-info"> 之后插入头像（userName 行之前）
  lines.splice(idx + 1, 0,
    '              <el-avatar :size="26" :src="userStore.avatar || undefined" class="user-avatar">',
    '                {{ (userName || \'?\').charAt(0).toUpperCase() }}',
    '              </el-avatar>');
  fs.writeFileSync(file, lines.join('\r\n'));
  console.log('topbar avatar inserted');
}
