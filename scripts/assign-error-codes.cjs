// 批量给 BusinessException / ApiResult.Fail 赋全局错误码（ErrorCodes 常量）。
// 安全策略：先全量校验每个替换的匹配次数，全部吻合才写盘；任一不符则整体失败不写入。
const fs = require('fs');
const path = require('path');
const root = path.resolve(__dirname, '..');

// [相对路径, [老片段, 新片段, 期望次数], ...]
// 约定：老片段是含 message 的唯一子串，新片段在 ) 前插入 , ErrorCodes.XXX
const jobs = [
  ['src/NetBase.Service/Sys/SysAuthService.cs', [
    ['new BusinessException("用户名或密码错误", ApiResultCode.BadRequest)', 'new BusinessException("用户名或密码错误", ApiResultCode.BadRequest, ErrorCodes.AUTH_BAD_CREDENTIALS)', 1],
    ['new BusinessException("用户名和密码不能为空", ApiResultCode.BadRequest)', 'new BusinessException("用户名和密码不能为空", ApiResultCode.BadRequest, ErrorCodes.AUTH_PARAM_MISSING)', 1],
    ['new BusinessException("验证码错误或已过期", ApiResultCode.BadRequest)', 'new BusinessException("验证码错误或已过期", ApiResultCode.BadRequest, ErrorCodes.AUTH_CAPTCHA_INVALID)', 1],
    ['new BusinessException($"密码错误次数过多，账号已锁定，请 {lockMinutes} 分钟后重试", ApiResultCode.BadRequest)', 'new BusinessException($"密码错误次数过多，账号已锁定，请 {lockMinutes} 分钟后重试", ApiResultCode.BadRequest, ErrorCodes.AUTH_ACCOUNT_LOCKED)', 1],
    ['new BusinessException("账号已被停用，请联系管理员", ApiResultCode.Forbidden)', 'new BusinessException("账号已被停用，请联系管理员", ApiResultCode.Forbidden, ErrorCodes.AUTH_ACCOUNT_DISABLED)', 2],
    ['new BusinessException("无效的刷新令牌", ApiResultCode.Unauthorized)', 'new BusinessException("无效的刷新令牌", ApiResultCode.Unauthorized, ErrorCodes.AUTH_REFRESH_TOKEN_INVALID)', 1],
    ['new BusinessException("登录已过期，请重新登录", ApiResultCode.Unauthorized)', 'new BusinessException("登录已过期，请重新登录", ApiResultCode.Unauthorized, ErrorCodes.AUTH_SESSION_EXPIRED)', 1],
    ['new BusinessException("账号不存在", ApiResultCode.Unauthorized)', 'new BusinessException("账号不存在", ApiResultCode.Unauthorized, ErrorCodes.AUTH_ACCOUNT_NOT_FOUND)', 2],
    ['new BusinessException(policyError, ApiResultCode.BadRequest)', 'new BusinessException(policyError, ApiResultCode.BadRequest, ErrorCodes.AUTH_PWD_POLICY_VIOLATION)', 1],
    ['new BusinessException("旧密码不正确", ApiResultCode.BadRequest)', 'new BusinessException("旧密码不正确", ApiResultCode.BadRequest, ErrorCodes.AUTH_OLD_PWD_WRONG)', 1],
    ['new BusinessException("新密码不能与旧密码相同", ApiResultCode.BadRequest)', 'new BusinessException("新密码不能与旧密码相同", ApiResultCode.BadRequest, ErrorCodes.AUTH_PWD_SAME_AS_OLD)', 1],
  ]],
  ['src/NetBase.Service/Sys/SysUserService.cs', [
    ['new BusinessException("仅内置管理员可以分配超级管理员角色", ApiResultCode.Forbidden)', 'new BusinessException("仅内置管理员可以分配超级管理员角色", ApiResultCode.Forbidden, ErrorCodes.SYS_USER_ADMIN_ROLE_GRANT_FORBIDDEN)', 1],
    ['ApiResultCode.NotFound, "SYS_USER_NOT_FOUND")', 'ApiResultCode.NotFound, ErrorCodes.SYS_USER_NOT_FOUND)', 1],
    ['new BusinessException("用户名不能为空", ApiResultCode.BadRequest)', 'new BusinessException("用户名不能为空", ApiResultCode.BadRequest, ErrorCodes.SYS_USER_NAME_REQUIRED)', 1],
    ['new BusinessException($"用户名 {dto.UserName} 已存在", ApiResultCode.BadRequest)', 'new BusinessException($"用户名 {dto.UserName} 已存在", ApiResultCode.BadRequest, ErrorCodes.SYS_USER_NAME_EXISTS)', 1],
    ['new BusinessException(policyError, ApiResultCode.BadRequest)', 'new BusinessException(policyError, ApiResultCode.BadRequest, ErrorCodes.SYS_USER_PWD_POLICY_VIOLATION)', 2],
    ['new BusinessException("所属部门不存在", ApiResultCode.BadRequest)', 'new BusinessException("所属部门不存在", ApiResultCode.BadRequest, ErrorCodes.SYS_USER_DEPT_NOT_FOUND)', 2],
    ['new BusinessException("不允许停用内置管理员账号")', 'new BusinessException("不允许停用内置管理员账号", ErrorCodes.SYS_USER_ADMIN_DISABLE_FORBIDDEN)', 1],
    ['new BusinessException("内置管理员账号的角色仅允许本人修改", ApiResultCode.Forbidden)', 'new BusinessException("内置管理员账号的角色仅允许本人修改", ApiResultCode.Forbidden, ErrorCodes.SYS_USER_ADMIN_ROLE_SELF_ONLY)', 1],
    ['new BusinessException("不允许删除内置管理员账号")', 'new BusinessException("不允许删除内置管理员账号", ErrorCodes.SYS_USER_ADMIN_DELETE_FORBIDDEN)', 1],
    ['new BusinessException("内置管理员账号的密码不允许重置", ApiResultCode.Forbidden)', 'new BusinessException("内置管理员账号的密码不允许重置", ApiResultCode.Forbidden, ErrorCodes.SYS_USER_ADMIN_RESET_FORBIDDEN)', 1],
    ['new BusinessException("存在无效的角色ID", ApiResultCode.BadRequest)', 'new BusinessException("存在无效的角色ID", ApiResultCode.BadRequest, ErrorCodes.SYS_USER_ROLE_INVALID)', 1],
  ]],
  ['src/NetBase.Service/Sys/SysRoleService.cs', [
    ['new BusinessException($"角色编码 {dto.RoleCode} 已存在", ApiResultCode.BadRequest)', 'new BusinessException($"角色编码 {dto.RoleCode} 已存在", ApiResultCode.BadRequest, ErrorCodes.SYS_ROLE_CODE_EXISTS)', 2],
    ['new BusinessException("不允许停用内置管理员角色")', 'new BusinessException("不允许停用内置管理员角色", ErrorCodes.SYS_ROLE_ADMIN_DISABLE_FORBIDDEN)', 1],
    ['new BusinessException("不允许删除内置管理员角色")', 'new BusinessException("不允许删除内置管理员角色", ErrorCodes.SYS_ROLE_ADMIN_DELETE_FORBIDDEN)', 1],
    ['new BusinessException("存在无效的菜单ID", ApiResultCode.BadRequest)', 'new BusinessException("存在无效的菜单ID", ApiResultCode.BadRequest, ErrorCodes.SYS_ROLE_MENU_INVALID)', 1],
    ['new BusinessException("角色名称不能为空", ApiResultCode.BadRequest)', 'new BusinessException("角色名称不能为空", ApiResultCode.BadRequest, ErrorCodes.SYS_ROLE_NAME_REQUIRED)', 1],
    ['new BusinessException("角色编码不能为空", ApiResultCode.BadRequest)', 'new BusinessException("角色编码不能为空", ApiResultCode.BadRequest, ErrorCodes.SYS_ROLE_CODE_REQUIRED)', 1],
  ]],
  ['src/NetBase.Service/Sys/SysMenuService.cs', [
    ['new BusinessException($"角色不存在（Id={roleId}）", ApiResultCode.NotFound)', 'new BusinessException($"角色不存在（Id={roleId}）", ApiResultCode.NotFound, ErrorCodes.SYS_ROLE_NOT_FOUND)', 1],
    ['new BusinessException("父级菜单不存在", ApiResultCode.BadRequest)', 'new BusinessException("父级菜单不存在", ApiResultCode.BadRequest, ErrorCodes.SYS_MENU_PARENT_NOT_FOUND)', 2],
    ['new BusinessException("父级菜单不能是自身", ApiResultCode.BadRequest)', 'new BusinessException("父级菜单不能是自身", ApiResultCode.BadRequest, ErrorCodes.SYS_MENU_PARENT_SELF)', 1],
    ['new BusinessException("父级菜单不能是自身的子孙节点", ApiResultCode.BadRequest)', 'new BusinessException("父级菜单不能是自身的子孙节点", ApiResultCode.BadRequest, ErrorCodes.SYS_MENU_PARENT_CYCLE)', 1],
    ['new BusinessException("存在子菜单，不允许删除")', 'new BusinessException("存在子菜单，不允许删除", ErrorCodes.SYS_MENU_HAS_CHILDREN)', 1],
    ['new BusinessException("菜单已被角色引用，请先取消角色授权")', 'new BusinessException("菜单已被角色引用，请先取消角色授权", ErrorCodes.SYS_MENU_IN_USE)', 1],
    ['new BusinessException("菜单名称不能为空", ApiResultCode.BadRequest)', 'new BusinessException("菜单名称不能为空", ApiResultCode.BadRequest, ErrorCodes.SYS_MENU_NAME_REQUIRED)', 1],
    ['new BusinessException("菜单类型无效（1-目录 2-菜单 3-按钮）", ApiResultCode.BadRequest)', 'new BusinessException("菜单类型无效（1-目录 2-菜单 3-按钮）", ApiResultCode.BadRequest, ErrorCodes.SYS_MENU_TYPE_INVALID)', 1],
  ]],
  ['src/NetBase.Service/Sys/SysDeptService.cs', [
    ['new BusinessException("父级部门不存在", ApiResultCode.BadRequest)', 'new BusinessException("父级部门不存在", ApiResultCode.BadRequest, ErrorCodes.SYS_DEPT_PARENT_NOT_FOUND)', 2],
    ['new BusinessException($"部门编码 {dto.DeptCode} 已存在", ApiResultCode.BadRequest)', 'new BusinessException($"部门编码 {dto.DeptCode} 已存在", ApiResultCode.BadRequest, ErrorCodes.SYS_DEPT_CODE_EXISTS)', 2],
    ['new BusinessException($"部门不存在（Id={id}）", ApiResultCode.NotFound)', 'new BusinessException($"部门不存在（Id={id}）", ApiResultCode.NotFound, ErrorCodes.SYS_DEPT_NOT_FOUND)', 2],
    ['new BusinessException("父级部门不能是自身", ApiResultCode.BadRequest)', 'new BusinessException("父级部门不能是自身", ApiResultCode.BadRequest, ErrorCodes.SYS_DEPT_PARENT_SELF)', 1],
    ['new BusinessException("父级部门不能是自身的子孙部门", ApiResultCode.BadRequest)', 'new BusinessException("父级部门不能是自身的子孙部门", ApiResultCode.BadRequest, ErrorCodes.SYS_DEPT_PARENT_CYCLE)', 1],
    ['new BusinessException("存在下级部门，不允许删除")', 'new BusinessException("存在下级部门，不允许删除", ErrorCodes.SYS_DEPT_HAS_CHILDREN)', 1],
    ['new BusinessException("部门下存在用户，不允许删除")', 'new BusinessException("部门下存在用户，不允许删除", ErrorCodes.SYS_DEPT_HAS_USERS)', 1],
    ['new BusinessException("部门名称不能为空", ApiResultCode.BadRequest)', 'new BusinessException("部门名称不能为空", ApiResultCode.BadRequest, ErrorCodes.SYS_DEPT_NAME_REQUIRED)', 1],
    ['new BusinessException("部门编码不能为空", ApiResultCode.BadRequest)', 'new BusinessException("部门编码不能为空", ApiResultCode.BadRequest, ErrorCodes.SYS_DEPT_CODE_REQUIRED)', 1],
  ]],
  ['src/NetBase.Service/Sys/SysPositionService.cs', [
    ['new BusinessException($"岗位编码 {dto.PositionCode} 已存在", ApiResultCode.BadRequest)', 'new BusinessException($"岗位编码 {dto.PositionCode} 已存在", ApiResultCode.BadRequest, ErrorCodes.SYS_POSITION_CODE_EXISTS)', 2],
    ['new BusinessException("岗位不存在")', 'new BusinessException("岗位不存在", ErrorCodes.SYS_POSITION_NOT_FOUND)', 2],
    ['new BusinessException("该岗位下仍有用户，请先移出")', 'new BusinessException("该岗位下仍有用户，请先移出", ErrorCodes.SYS_POSITION_HAS_USERS)', 1],
  ]],
  ['src/NetBase.Service/Sys/SysDictService.cs', [
    ['new BusinessException($"字典编码 {dto.DictCode} 已存在", ApiResultCode.BadRequest)', 'new BusinessException($"字典编码 {dto.DictCode} 已存在", ApiResultCode.BadRequest, ErrorCodes.SYS_DICT_CODE_EXISTS)', 2],
    ['new BusinessException($"字典类型不存在（Id={id}）", ApiResultCode.NotFound)', 'new BusinessException($"字典类型不存在（Id={id}）", ApiResultCode.NotFound, ErrorCodes.SYS_DICT_TYPE_NOT_FOUND)', 2],
    ['new BusinessException("字典类型不存在", ApiResultCode.BadRequest)', 'new BusinessException("字典类型不存在", ApiResultCode.BadRequest, ErrorCodes.SYS_DICT_TYPE_NOT_FOUND)', 2],
    ['new BusinessException($"字典值 {dto.Value} 已存在", ApiResultCode.BadRequest)', 'new BusinessException($"字典值 {dto.Value} 已存在", ApiResultCode.BadRequest, ErrorCodes.SYS_DICT_VALUE_EXISTS)', 2],
    ['new BusinessException($"字典项不存在（Id={id}）", ApiResultCode.BadRequest)', 'new BusinessException($"字典项不存在（Id={id}）", ApiResultCode.BadRequest, ErrorCodes.SYS_DICT_DATA_NOT_FOUND)', 1],
    ['new BusinessException($"字典项不存在（Id={id}）", ApiResultCode.NotFound)', 'new BusinessException($"字典项不存在（Id={id}）", ApiResultCode.NotFound, ErrorCodes.SYS_DICT_DATA_NOT_FOUND)', 1],
  ]],
  ['src/NetBase.Service/Sys/SysConfigService.cs', [
    ['new BusinessException($"参数键 {dto.ConfigKey} 已存在", ApiResultCode.BadRequest)', 'new BusinessException($"参数键 {dto.ConfigKey} 已存在", ApiResultCode.BadRequest, ErrorCodes.SYS_CONFIG_KEY_EXISTS)', 2],
    ['new BusinessException($"参数不存在（Id={id}）", ApiResultCode.NotFound)', 'new BusinessException($"参数不存在（Id={id}）", ApiResultCode.NotFound, ErrorCodes.SYS_CONFIG_NOT_FOUND)', 2],
    ['new BusinessException("内置参数不允许修改参数键，仅可修改参数值", ApiResultCode.BadRequest)', 'new BusinessException("内置参数不允许修改参数键，仅可修改参数值", ApiResultCode.BadRequest, ErrorCodes.SYS_CONFIG_BUILTIN_KEY_LOCKED)', 1],
    ['new BusinessException("内置参数不允许删除，仅可修改值")', 'new BusinessException("内置参数不允许删除，仅可修改值", ErrorCodes.SYS_CONFIG_BUILTIN_DELETE_FORBIDDEN)', 1],
  ]],
  ['src/NetBase.Service/Sys/SysNoticeService.cs', [
    ['new BusinessException($"公告不存在（Id={id}）", ApiResultCode.NotFound)', 'new BusinessException($"公告不存在（Id={id}）", ApiResultCode.NotFound, ErrorCodes.SYS_NOTICE_NOT_FOUND)', 1],
    ['new BusinessException("定时发布时间必须为当前时间之后", ApiResultCode.BadRequest)', 'new BusinessException("定时发布时间必须为当前时间之后", ApiResultCode.BadRequest, ErrorCodes.SYS_NOTICE_PUBLISH_TIME_INVALID)', 1],
  ]],
  ['src/NetBase.Service/Sys/SysMessageService.cs', [
    ['new BusinessException($"接收用户不存在（Id={dto.ReceiverId}）", ApiResultCode.BadRequest)', 'new BusinessException($"接收用户不存在（Id={dto.ReceiverId}）", ApiResultCode.BadRequest, ErrorCodes.SYS_MESSAGE_RECEIVER_NOT_FOUND)', 1],
    ['new BusinessException("消息不存在", ApiResultCode.NotFound)', 'new BusinessException("消息不存在", ApiResultCode.NotFound, ErrorCodes.SYS_MESSAGE_NOT_FOUND)', 1],
    ['new BusinessException("无权操作该消息", ApiResultCode.Forbidden)', 'new BusinessException("无权操作该消息", ApiResultCode.Forbidden, ErrorCodes.SYS_MESSAGE_ACCESS_DENIED)', 1],
  ]],
  ['src/NetBase.Service/Sys/SysFileService.cs', [
    ['new BusinessException("文件名无效", NetBase.Common.Results.ApiResultCode.BadRequest)', 'new BusinessException("文件名无效", NetBase.Common.Results.ApiResultCode.BadRequest, NetBase.Common.Results.ErrorCodes.SYS_FILE_NAME_INVALID)', 1],
    ['new BusinessException("头像仅支持 jpg/png/gif/webp 格式", NetBase.Common.Results.ApiResultCode.BadRequest)', 'new BusinessException("头像仅支持 jpg/png/gif/webp 格式", NetBase.Common.Results.ApiResultCode.BadRequest, NetBase.Common.Results.ErrorCodes.SYS_FILE_AVATAR_TYPE_INVALID)', 1],
    ['new BusinessException($"不支持的文件类型：{extension}", NetBase.Common.Results.ApiResultCode.BadRequest)', 'new BusinessException($"不支持的文件类型：{extension}", NetBase.Common.Results.ApiResultCode.BadRequest, NetBase.Common.Results.ErrorCodes.SYS_FILE_TYPE_INVALID)', 1],
    ['new BusinessException("文件内容与扩展名不符，已拒绝上传", NetBase.Common.Results.ApiResultCode.BadRequest)', 'new BusinessException("文件内容与扩展名不符，已拒绝上传", NetBase.Common.Results.ApiResultCode.BadRequest, NetBase.Common.Results.ErrorCodes.SYS_FILE_SIGNATURE_MISMATCH)', 1],
    ['new BusinessException($"文件大小超过限制（最大 {maxSize / 1024 / 1024}MB）", NetBase.Common.Results.ApiResultCode.BadRequest)', 'new BusinessException($"文件大小超过限制（最大 {maxSize / 1024 / 1024}MB）", NetBase.Common.Results.ApiResultCode.BadRequest, NetBase.Common.Results.ErrorCodes.SYS_FILE_SIZE_EXCEEDED)', 1],
  ]],
  ['src/NetBase.Service/Sys/Flow/FlowEngine.cs', [
    ['new BusinessException("未登录无法提交审批")', 'new BusinessException("未登录无法提交审批", ErrorCodes.FLOW_NOT_AUTHENTICATED)', 1],
    ['中配置");', '中配置", ErrorCodes.FLOW_BINDING_MISSING);', 1],
    ['new BusinessException($"流程 {flowCode} 未配置或未启用")', 'new BusinessException($"流程 {flowCode} 未配置或未启用", ErrorCodes.FLOW_DEF_NOT_ENABLED)', 1],
    ['new BusinessException($"流程 {request.FlowCode} 节点配置无效")', 'new BusinessException($"流程 {request.FlowCode} 节点配置无效", ErrorCodes.FLOW_DEF_NODES_INVALID)', 1],
    ['new BusinessException("未登录")', 'new BusinessException("未登录", ErrorCodes.FLOW_NOT_AUTHENTICATED)', 5],
    ['new BusinessException("审批任务不存在")', 'new BusinessException("审批任务不存在", ErrorCodes.FLOW_TASK_NOT_FOUND)', 3],
    ['new BusinessException("流程实例不存在")', 'new BusinessException("流程实例不存在", ErrorCodes.FLOW_INSTANCE_NOT_FOUND)', 5],
    ['new BusinessException("驳回请选择退回目标节点")', 'new BusinessException("驳回请选择退回目标节点", ErrorCodes.FLOW_RETURN_TARGET_REQUIRED)', 1],
    ['new BusinessException("退回目标节点不存在")', 'new BusinessException("退回目标节点不存在", ErrorCodes.FLOW_RETURN_TARGET_NOT_FOUND)', 1],
    ['new BusinessException("不能驳回至当前节点自身")', 'new BusinessException("不能驳回至当前节点自身", ErrorCodes.FLOW_RETURN_TARGET_SELF)', 1],
    ['new BusinessException("转办目标人无效")', 'new BusinessException("转办目标人无效", ErrorCodes.FLOW_TRANSFER_TARGET_INVALID)', 1],
    ['new BusinessException("加签人无效")', 'new BusinessException("加签人无效", ErrorCodes.FLOW_ADDSIGN_TARGET_INVALID)', 1],
    ['new BusinessException("仅发起人可撤回")', 'new BusinessException("仅发起人可撤回", ErrorCodes.FLOW_WITHDRAW_FORBIDDEN)', 1],
    ['new BusinessException("审批已开始处理，无法撤回")', 'new BusinessException("审批已开始处理，无法撤回", ErrorCodes.FLOW_WITHDRAW_PROCESSING)', 1],
    ['new BusinessException("仅发起人可催办")', 'new BusinessException("仅发起人可催办", ErrorCodes.FLOW_URGE_FORBIDDEN)', 1],
    ['new BusinessException("4 小时内已催办过，请稍后再试")', 'new BusinessException("4 小时内已催办过，请稍后再试", ErrorCodes.FLOW_URGE_RATE_LIMITED)', 1],
    ['new BusinessException("当前没有待处理的审批任务")', 'new BusinessException("当前没有待处理的审批任务", ErrorCodes.FLOW_URGE_NO_PENDING_TASK)', 1],
    ['new BusinessException("流程节点配置成环，请检查流程设计")', 'new BusinessException("流程节点配置成环，请检查流程设计", ErrorCodes.FLOW_DEF_CYCLE)', 1],
    ['未配置默认分支");', '未配置默认分支", ErrorCodes.FLOW_CONDITION_NO_BRANCH);', 1],
    ['new BusinessException("流程缺少入口节点")', 'new BusinessException("流程缺少入口节点", ErrorCodes.FLOW_DEF_NO_ENTRY)', 1],
    ['new BusinessException($"节点编码重复: {dup.Key}")', 'new BusinessException($"节点编码重复: {dup.Key}", ErrorCodes.FLOW_DEF_NODE_CODE_DUP)', 1],
    ['new BusinessException($"审批节点 {node.Name ?? node.Code} 未配置审批人规则")', 'new BusinessException($"审批节点 {node.Name ?? node.Code} 未配置审批人规则", ErrorCodes.FLOW_DEF_APPROVER_MISSING)', 1],
    ['new BusinessException("流程定义已删除")', 'new BusinessException("流程定义已删除", ErrorCodes.FLOW_DEF_DELETED)', 1],
    ['new BusinessException("流程节点配置无效")', 'new BusinessException("流程节点配置无效", ErrorCodes.FLOW_DEF_NODES_INVALID)', 1],
    ['new BusinessException("仅任务归属人可处理")', 'new BusinessException("仅任务归属人可处理", ErrorCodes.FLOW_TASK_OWNER_ONLY)', 1],
    ['new BusinessException("该任务已处理或已失效")', 'new BusinessException("该任务已处理或已失效", ErrorCodes.FLOW_TASK_ALREADY_HANDLED)', 1],
    ['new BusinessException("流程已结束")', 'new BusinessException("流程已结束", ErrorCodes.FLOW_INSTANCE_FINISHED)', 1],
  ]],
  ['src/NetBase.Service/Sys/Flow/SysFlowDefinitionService.cs', [
    ['new BusinessException("流程定义不存在")', 'new BusinessException("流程定义不存在", ErrorCodes.FLOW_DEF_NOT_FOUND)', 3],
    ['new BusinessException("启用中的流程不允许删除，请先停用")', 'new BusinessException("启用中的流程不允许删除，请先停用", ErrorCodes.FLOW_DEF_DELETE_ENABLED_FORBIDDEN)', 1],
    ['new BusinessException("节点配置 JSON 解析失败")', 'new BusinessException("节点配置 JSON 解析失败", ErrorCodes.FLOW_DEF_JSON_INVALID)', 1],
    ['new BusinessException("节点配置缺少入口节点")', 'new BusinessException("节点配置缺少入口节点", ErrorCodes.FLOW_DEF_NO_ENTRY)', 1],
    ['new BusinessException($"节点编码重复: {dup.Key}")', 'new BusinessException($"节点编码重复: {dup.Key}", ErrorCodes.FLOW_DEF_NODE_CODE_DUP)', 1],
    ['new BusinessException($"审批节点「{node.Name ?? node.Code}」未配置审批人规则")', 'new BusinessException($"审批节点「{node.Name ?? node.Code}」未配置审批人规则", ErrorCodes.FLOW_DEF_APPROVER_MISSING)', 1],
    ['new BusinessException("分类名不能为空")', 'new BusinessException("分类名不能为空", ErrorCodes.FLOW_CATEGORY_NAME_REQUIRED)', 1],
    ['new BusinessException($"分类「{name}」下还有 {used} 个流程，请先在流程上移出该分类")', 'new BusinessException($"分类「{name}」下还有 {used} 个流程，请先在流程上移出该分类", ErrorCodes.FLOW_CATEGORY_IN_USE)', 1],
  ]],
  ['src/NetBase.Service/Sys/Flow/SysFlowDelegateService.cs', [
    ['new BusinessException("未登录", ApiResultCode.Unauthorized)', 'new BusinessException("未登录", ApiResultCode.Unauthorized, ErrorCodes.FLOW_NOT_AUTHENTICATED)', 1],
    ['new BusinessException("结束时间必须晚于开始时间", ApiResultCode.BadRequest)', 'new BusinessException("结束时间必须晚于开始时间", ApiResultCode.BadRequest, ErrorCodes.FLOW_DELEGATE_TIME_INVALID)', 1],
    ['new BusinessException("不能委托给自己", ApiResultCode.BadRequest)', 'new BusinessException("不能委托给自己", ApiResultCode.BadRequest, ErrorCodes.FLOW_DELEGATE_SELF)', 1],
    ['new BusinessException("代理人不存在或已停用", ApiResultCode.BadRequest)', 'new BusinessException("代理人不存在或已停用", ApiResultCode.BadRequest, ErrorCodes.FLOW_DELEGATE_AGENT_INVALID)', 1],
    ['new BusinessException("委托不存在")', 'new BusinessException("委托不存在", ErrorCodes.FLOW_DELEGATE_NOT_FOUND)', 1],
    ['new BusinessException("仅可删除自己的委托", ApiResultCode.Forbidden)', 'new BusinessException("仅可删除自己的委托", ApiResultCode.Forbidden, ErrorCodes.FLOW_DELEGATE_DELETE_FORBIDDEN)', 1],
  ]],
  ['src/NetBase.Service/Sys/Flow/SysFlowQueryService.cs', [
    ['new BusinessException("流程实例不存在")', 'new BusinessException("流程实例不存在", ErrorCodes.FLOW_INSTANCE_NOT_FOUND)', 1],
  ]],
  ['src/NetBase.Service/Biz/BizDocumentService.cs', [
    ['new BusinessException("报销单不存在")', 'new BusinessException("报销单不存在", ErrorCodes.BIZ_EXPENSE_NOT_FOUND)', 3],
    ['new BusinessException("仅草稿或被拒绝的单据可提交审批")', 'new BusinessException("仅草稿或被拒绝的单据可提交审批", ErrorCodes.BIZ_DOC_SUBMIT_STATUS_INVALID)', 2],
    ['new BusinessException("仅草稿可修改")', 'new BusinessException("仅草稿可修改", ErrorCodes.BIZ_DOC_MODIFY_STATUS_INVALID)', 1],
    ['new BusinessException("采购申请单不存在")', 'new BusinessException("采购申请单不存在", ErrorCodes.BIZ_PURCHASE_NOT_FOUND)', 3],
    ['new BusinessException("仅草稿可操作")', 'new BusinessException("仅草稿可操作", ErrorCodes.BIZ_DOC_MODIFY_STATUS_INVALID)', 1],
  ]],
  ['src/NetBase.Api/Controllers/Auth/AuthController.cs', [
    ['ApiResult<string>.Fail("请选择头像文件", ApiResultCode.BadRequest)', 'ApiResult<string>.Fail("请选择头像文件", ApiResultCode.BadRequest, ErrorCodes.COMMON_FILE_REQUIRED)', 1],
  ]],
  ['src/NetBase.Api/Controllers/System/SysUserController.cs', [
    ['ApiResult<UserImportResultDto>.Fail("请选择导入文件", ApiResultCode.BadRequest)', 'ApiResult<UserImportResultDto>.Fail("请选择导入文件", ApiResultCode.BadRequest, ErrorCodes.SYS_USER_IMPORT_FILE_REQUIRED)', 1],
    ['ApiResult<UserImportResultDto>.Fail("仅支持 xlsx/xls 文件", ApiResultCode.BadRequest)', 'ApiResult<UserImportResultDto>.Fail("仅支持 xlsx/xls 文件", ApiResultCode.BadRequest, ErrorCodes.SYS_USER_IMPORT_TYPE_INVALID)', 1],
    ['ApiResult<UserImportResultDto>.Fail("导入文件中没有数据行", ApiResultCode.BadRequest)', 'ApiResult<UserImportResultDto>.Fail("导入文件中没有数据行", ApiResultCode.BadRequest, ErrorCodes.SYS_USER_IMPORT_EMPTY)', 1],
  ]],
  ['src/NetBase.Api/Controllers/System/FileController.cs', [
    ['ApiResult<FileUploadResult>.Fail("请选择要上传的文件", ApiResultCode.BadRequest)', 'ApiResult<FileUploadResult>.Fail("请选择要上传的文件", ApiResultCode.BadRequest, ErrorCodes.COMMON_FILE_REQUIRED)', 1],
  ]],
];

const NEED_USING = new Set([
  'src/NetBase.Service/Sys/SysFileService.cs',
  'src/NetBase.Service/Sys/Flow/FlowEngine.cs',
]);

// Pass 1: 校验全部匹配次数
const pendingWrites = [];
let failures = [];
for (const [rel, patterns] of jobs) {
  const full = path.join(root, rel);
  let content = fs.readFileSync(full, 'utf8');
  for (const [oldStr, newStr, expected] of patterns) {
    const count = content.split(oldStr).length - 1;
    if (count !== expected) {
      failures.push(`${rel}: "${oldStr.slice(0, 50)}..." 期望 ${expected} 次，实际 ${count} 次`);
      continue;
    }
    content = content.split(oldStr).join(newStr);
  }
  if (NEED_USING.has(rel) && !content.includes('using NetBase.Common.Results;')) {
    const lines = content.split(/\r?\n/);
    const lastUsing = lines.map((l, i) => [l, i]).filter(([l]) => l.startsWith('using ')).pop();
    if (!lastUsing) { failures.push(`${rel}: 找不到 using 区`); }
    else { lines.splice(lastUsing[1] + 1, 0, 'using NetBase.Common.Results;'); content = lines.join('\r\n'); }
  }
  pendingWrites.push([full, content]);
}
if (failures.length) {
  console.error('校验失败，未写入任何文件：');
  for (const f of failures) console.error('  ' + f);
  process.exit(1);
}
// Pass 2: 写盘
for (const [full, content] of pendingWrites) fs.writeFileSync(full, content, 'utf8');
console.log(`OK：${jobs.length} 个文件铺码完成，共 ${jobs.reduce((n, [, p]) => n + p.length, 0)} 条替换规则全部命中`);
