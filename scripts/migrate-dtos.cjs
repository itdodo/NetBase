// 迁移 FlowControllers 内联请求 DTO 到 Model 层 + Profile 强类型化（一次性脚本）
const fs = require('fs');

// 1) FlowControllers：删内联 DTO + 补 using（幂等：已迁移则跳过）
let f = fs.readFileSync('src/NetBase.Api/Controllers/Workflow/FlowControllers.cs', 'utf8');
const start = f.indexOf('/// <summary>审批操作请求</summary>');
if (start >= 0) {
  const end = f.indexOf('/// <summary>流程定义管理');
  if (end < 0 || end <= start) throw new Error('anchor not found');
  f = f.slice(0, start) + f.slice(end);
  if (!f.includes('using NetBase.Model.Dtos;')) {
    f = f.replace('using NetBase.Service.Sys.Flow;', 'using NetBase.Model.Dtos;\nusing NetBase.Service.Sys.Flow;');
  }
  fs.writeFileSync('src/NetBase.Api/Controllers/Workflow/FlowControllers.cs', f);
  console.log('FlowControllers 已迁移');
} else {
  console.log('FlowControllers 已是迁移后状态，跳过');
}

// 2) AuthController.Profile 强类型（CRLF 容错：按方法块正则替换）
let a = fs.readFileSync('src/NetBase.Api/Controllers/Auth/AuthController.cs', 'utf8');
if (!/Task<ApiResult<ProfileDto>> Profile\(\)/.test(a)) {
  a = a.replace(
    /public async Task<ApiResult<object>> Profile\(\)\s*\{[\s\S]*?\n    \}/,
    `public async Task<ApiResult<ProfileDto>> Profile()
    {
        var userId = OperatorUserId ?? 0;
        var profile = new ProfileDto
        {
            User = userId > 0 ? await authService.GetUserProfileAsync(userId) : null,
            Permissions = userId > 0 ? await permissionService.GetUserPermissionsAsync(userId) : []
        };
        return Success(profile);
    }`
  );
  if (!/Task<ApiResult<ProfileDto>> Profile\(\)/.test(a)) throw new Error('Profile replace failed');
  if (!a.includes('using NetBase.Model.Dtos;')) {
    a = a.replace('using NetBase.Service.Sys;', 'using NetBase.Model.Dtos;\nusing NetBase.Service.Sys;');
  }
  fs.writeFileSync('src/NetBase.Api/Controllers/Auth/AuthController.cs', a);
}
console.log('Profile 已强类型化');
