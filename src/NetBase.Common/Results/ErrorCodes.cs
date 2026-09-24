namespace NetBase.Common.Results;

/// <summary>
/// 全局业务错误码表（字符串码，随 ApiResult.ErrorCode 返回，前端按码分支）。
/// 规范：命名 {域}_{对象}_{原因}；码只增不改不删；新增业务异常必须先查本表选码。
/// 总表见 docs/错误码.md。
/// </summary>
public static class ErrorCodes
{
    // ---------- 通用 ----------
    /// <summary>请求参数校验失败（模型验证过滤器）</summary>
    public const string COMMON_PARAM_INVALID = "COMMON_PARAM_INVALID";

    /// <summary>数据已被他人修改（乐观锁并发冲突，code=409）</summary>
    public const string COMMON_CONCURRENCY_CONFLICT = "COMMON_CONCURRENCY_CONFLICT";

    /// <summary>请勿重复提交（2 秒防重窗口）</summary>
    public const string COMMON_REPEAT_SUBMIT = "COMMON_REPEAT_SUBMIT";

    /// <summary>未选择文件</summary>
    public const string COMMON_FILE_REQUIRED = "COMMON_FILE_REQUIRED";

    /// <summary>资源不存在（GetRequiredAsync 未传实体码时的兜底）</summary>
    public const string COMMON_NOT_FOUND = "COMMON_NOT_FOUND";

    /// <summary>接口路径不存在（未匹配路由，code=404）</summary>
    public const string COMMON_ROUTE_NOT_FOUND = "COMMON_ROUTE_NOT_FOUND";

    /// <summary>未预期系统异常（全局异常过滤器兜底，code=500）</summary>
    public const string COMMON_SYSTEM_ERROR = "COMMON_SYSTEM_ERROR";

    /// <summary>业务规则不满足（未赋码业务异常的兜底，不建议新代码使用）</summary>
    public const string COMMON_BUSINESS_ERROR = "COMMON_BUSINESS_ERROR";

    // ---------- 认证与会话（auth） ----------
    /// <summary>用户名或密码错误</summary>
    public const string AUTH_BAD_CREDENTIALS = "AUTH_BAD_CREDENTIALS";

    /// <summary>用户名和密码不能为空</summary>
    public const string AUTH_PARAM_MISSING = "AUTH_PARAM_MISSING";

    /// <summary>验证码错误或已过期</summary>
    public const string AUTH_CAPTCHA_INVALID = "AUTH_CAPTCHA_INVALID";

    /// <summary>密码错误次数过多，账号已锁定</summary>
    public const string AUTH_ACCOUNT_LOCKED = "AUTH_ACCOUNT_LOCKED";

    /// <summary>账号已被停用</summary>
    public const string AUTH_ACCOUNT_DISABLED = "AUTH_ACCOUNT_DISABLED";

    /// <summary>无效的刷新令牌</summary>
    public const string AUTH_REFRESH_TOKEN_INVALID = "AUTH_REFRESH_TOKEN_INVALID";

    /// <summary>登录已过期/会话失效（含管道 401）</summary>
    public const string AUTH_SESSION_EXPIRED = "AUTH_SESSION_EXPIRED";

    /// <summary>账号不存在（刷新令牌/改密路径）</summary>
    public const string AUTH_ACCOUNT_NOT_FOUND = "AUTH_ACCOUNT_NOT_FOUND";

    /// <summary>密码不符合复杂度策略（改密路径）</summary>
    public const string AUTH_PWD_POLICY_VIOLATION = "AUTH_PWD_POLICY_VIOLATION";

    /// <summary>旧密码不正确</summary>
    public const string AUTH_OLD_PWD_WRONG = "AUTH_OLD_PWD_WRONG";

    /// <summary>新密码不能与旧密码相同</summary>
    public const string AUTH_PWD_SAME_AS_OLD = "AUTH_PWD_SAME_AS_OLD";

    /// <summary>无权限访问（管道 403）</summary>
    public const string AUTH_FORBIDDEN = "AUTH_FORBIDDEN";

    // ---------- 用户（sys_user） ----------
    /// <summary>用户不存在</summary>
    public const string SYS_USER_NOT_FOUND = "SYS_USER_NOT_FOUND";

    /// <summary>用户名不能为空</summary>
    public const string SYS_USER_NAME_REQUIRED = "SYS_USER_NAME_REQUIRED";

    /// <summary>用户名已存在</summary>
    public const string SYS_USER_NAME_EXISTS = "SYS_USER_NAME_EXISTS";

    /// <summary>密码不符合复杂度策略（创建/导入/重置路径）</summary>
    public const string SYS_USER_PWD_POLICY_VIOLATION = "SYS_USER_PWD_POLICY_VIOLATION";

    /// <summary>所属部门不存在</summary>
    public const string SYS_USER_DEPT_NOT_FOUND = "SYS_USER_DEPT_NOT_FOUND";

    /// <summary>存在无效的角色ID</summary>
    public const string SYS_USER_ROLE_INVALID = "SYS_USER_ROLE_INVALID";

    /// <summary>仅内置管理员可以分配超级管理员角色</summary>
    public const string SYS_USER_ADMIN_ROLE_GRANT_FORBIDDEN = "SYS_USER_ADMIN_ROLE_GRANT_FORBIDDEN";

    /// <summary>不允许停用内置管理员账号</summary>
    public const string SYS_USER_ADMIN_DISABLE_FORBIDDEN = "SYS_USER_ADMIN_DISABLE_FORBIDDEN";

    /// <summary>内置管理员账号的角色仅允许本人修改</summary>
    public const string SYS_USER_ADMIN_ROLE_SELF_ONLY = "SYS_USER_ADMIN_ROLE_SELF_ONLY";

    /// <summary>不允许删除内置管理员账号</summary>
    public const string SYS_USER_ADMIN_DELETE_FORBIDDEN = "SYS_USER_ADMIN_DELETE_FORBIDDEN";

    /// <summary>内置管理员账号的密码不允许重置</summary>
    public const string SYS_USER_ADMIN_RESET_FORBIDDEN = "SYS_USER_ADMIN_RESET_FORBIDDEN";

    /// <summary>请选择导入文件</summary>
    public const string SYS_USER_IMPORT_FILE_REQUIRED = "SYS_USER_IMPORT_FILE_REQUIRED";

    /// <summary>导入文件仅支持 xlsx/xls</summary>
    public const string SYS_USER_IMPORT_TYPE_INVALID = "SYS_USER_IMPORT_TYPE_INVALID";

    /// <summary>导入文件中没有数据行</summary>
    public const string SYS_USER_IMPORT_EMPTY = "SYS_USER_IMPORT_EMPTY";

    // ---------- 角色（sys_role） ----------
    /// <summary>角色不存在</summary>
    public const string SYS_ROLE_NOT_FOUND = "SYS_ROLE_NOT_FOUND";

    /// <summary>角色编码已存在</summary>
    public const string SYS_ROLE_CODE_EXISTS = "SYS_ROLE_CODE_EXISTS";

    /// <summary>角色编码不能为空</summary>
    public const string SYS_ROLE_CODE_REQUIRED = "SYS_ROLE_CODE_REQUIRED";

    /// <summary>角色名称不能为空</summary>
    public const string SYS_ROLE_NAME_REQUIRED = "SYS_ROLE_NAME_REQUIRED";

    /// <summary>存在无效的菜单ID</summary>
    public const string SYS_ROLE_MENU_INVALID = "SYS_ROLE_MENU_INVALID";

    /// <summary>不允许停用内置管理员角色</summary>
    public const string SYS_ROLE_ADMIN_DISABLE_FORBIDDEN = "SYS_ROLE_ADMIN_DISABLE_FORBIDDEN";

    /// <summary>不允许删除内置管理员角色</summary>
    public const string SYS_ROLE_ADMIN_DELETE_FORBIDDEN = "SYS_ROLE_ADMIN_DELETE_FORBIDDEN";

    // ---------- 菜单（sys_menu） ----------
    /// <summary>父级菜单不存在</summary>
    public const string SYS_MENU_PARENT_NOT_FOUND = "SYS_MENU_PARENT_NOT_FOUND";

    /// <summary>父级菜单不能是自身</summary>
    public const string SYS_MENU_PARENT_SELF = "SYS_MENU_PARENT_SELF";

    /// <summary>父级菜单不能是自身的子孙节点</summary>
    public const string SYS_MENU_PARENT_CYCLE = "SYS_MENU_PARENT_CYCLE";

    /// <summary>存在子菜单，不允许删除</summary>
    public const string SYS_MENU_HAS_CHILDREN = "SYS_MENU_HAS_CHILDREN";

    /// <summary>菜单已被角色引用，请先取消角色授权</summary>
    public const string SYS_MENU_IN_USE = "SYS_MENU_IN_USE";

    /// <summary>菜单名称不能为空</summary>
    public const string SYS_MENU_NAME_REQUIRED = "SYS_MENU_NAME_REQUIRED";

    /// <summary>菜单类型无效（1-目录 2-菜单 3-按钮）</summary>
    public const string SYS_MENU_TYPE_INVALID = "SYS_MENU_TYPE_INVALID";

    // ---------- 部门（sys_dept） ----------
    /// <summary>部门不存在</summary>
    public const string SYS_DEPT_NOT_FOUND = "SYS_DEPT_NOT_FOUND";

    /// <summary>父级部门不存在</summary>
    public const string SYS_DEPT_PARENT_NOT_FOUND = "SYS_DEPT_PARENT_NOT_FOUND";

    /// <summary>父级部门不能是自身</summary>
    public const string SYS_DEPT_PARENT_SELF = "SYS_DEPT_PARENT_SELF";

    /// <summary>父级部门不能是自身的子孙部门</summary>
    public const string SYS_DEPT_PARENT_CYCLE = "SYS_DEPT_PARENT_CYCLE";

    /// <summary>部门编码已存在</summary>
    public const string SYS_DEPT_CODE_EXISTS = "SYS_DEPT_CODE_EXISTS";

    /// <summary>部门名称不能为空</summary>
    public const string SYS_DEPT_NAME_REQUIRED = "SYS_DEPT_NAME_REQUIRED";

    /// <summary>部门编码不能为空</summary>
    public const string SYS_DEPT_CODE_REQUIRED = "SYS_DEPT_CODE_REQUIRED";

    /// <summary>存在下级部门，不允许删除</summary>
    public const string SYS_DEPT_HAS_CHILDREN = "SYS_DEPT_HAS_CHILDREN";

    /// <summary>部门下存在用户，不允许删除</summary>
    public const string SYS_DEPT_HAS_USERS = "SYS_DEPT_HAS_USERS";

    // ---------- 岗位（sys_position） ----------
    /// <summary>岗位不存在</summary>
    public const string SYS_POSITION_NOT_FOUND = "SYS_POSITION_NOT_FOUND";

    /// <summary>岗位编码已存在</summary>
    public const string SYS_POSITION_CODE_EXISTS = "SYS_POSITION_CODE_EXISTS";

    /// <summary>该岗位下仍有用户，请先移出</summary>
    public const string SYS_POSITION_HAS_USERS = "SYS_POSITION_HAS_USERS";

    // ---------- 字典（sys_dict_type / sys_dict_data） ----------
    /// <summary>字典类型不存在</summary>
    public const string SYS_DICT_TYPE_NOT_FOUND = "SYS_DICT_TYPE_NOT_FOUND";

    /// <summary>字典编码已存在</summary>
    public const string SYS_DICT_CODE_EXISTS = "SYS_DICT_CODE_EXISTS";

    /// <summary>字典值已存在</summary>
    public const string SYS_DICT_VALUE_EXISTS = "SYS_DICT_VALUE_EXISTS";

    /// <summary>字典项不存在</summary>
    public const string SYS_DICT_DATA_NOT_FOUND = "SYS_DICT_DATA_NOT_FOUND";

    // ---------- 参数（sys_config） ----------
    /// <summary>参数不存在</summary>
    public const string SYS_CONFIG_NOT_FOUND = "SYS_CONFIG_NOT_FOUND";

    /// <summary>参数键已存在</summary>
    public const string SYS_CONFIG_KEY_EXISTS = "SYS_CONFIG_KEY_EXISTS";

    /// <summary>内置参数不允许修改参数键，仅可修改参数值</summary>
    public const string SYS_CONFIG_BUILTIN_KEY_LOCKED = "SYS_CONFIG_BUILTIN_KEY_LOCKED";

    /// <summary>内置参数不允许删除，仅可修改值</summary>
    public const string SYS_CONFIG_BUILTIN_DELETE_FORBIDDEN = "SYS_CONFIG_BUILTIN_DELETE_FORBIDDEN";

    // ---------- 公告（sys_notice） ----------
    /// <summary>公告不存在</summary>
    public const string SYS_NOTICE_NOT_FOUND = "SYS_NOTICE_NOT_FOUND";

    /// <summary>定时发布时间必须为当前时间之后</summary>
    public const string SYS_NOTICE_PUBLISH_TIME_INVALID = "SYS_NOTICE_PUBLISH_TIME_INVALID";

    // ---------- 站内信（sys_message） ----------
    /// <summary>消息不存在</summary>
    public const string SYS_MESSAGE_NOT_FOUND = "SYS_MESSAGE_NOT_FOUND";

    /// <summary>接收用户不存在</summary>
    public const string SYS_MESSAGE_RECEIVER_NOT_FOUND = "SYS_MESSAGE_RECEIVER_NOT_FOUND";

    /// <summary>无权操作该消息</summary>
    public const string SYS_MESSAGE_ACCESS_DENIED = "SYS_MESSAGE_ACCESS_DENIED";

    // ---------- 文件（sys_file） ----------
    /// <summary>文件名无效</summary>
    public const string SYS_FILE_NAME_INVALID = "SYS_FILE_NAME_INVALID";

    /// <summary>不支持的文件类型</summary>
    public const string SYS_FILE_TYPE_INVALID = "SYS_FILE_TYPE_INVALID";

    /// <summary>头像仅支持 jpg/png/gif/webp 格式</summary>
    public const string SYS_FILE_AVATAR_TYPE_INVALID = "SYS_FILE_AVATAR_TYPE_INVALID";

    /// <summary>文件内容与扩展名不符，已拒绝上传</summary>
    public const string SYS_FILE_SIGNATURE_MISMATCH = "SYS_FILE_SIGNATURE_MISMATCH";

    /// <summary>文件大小超过限制</summary>
    public const string SYS_FILE_SIZE_EXCEEDED = "SYS_FILE_SIZE_EXCEEDED";

    // ---------- 审批流（sys_flow_*） ----------
    /// <summary>未登录无法执行审批操作</summary>
    public const string FLOW_NOT_AUTHENTICATED = "FLOW_NOT_AUTHENTICATED";

    /// <summary>流程定义不存在</summary>
    public const string FLOW_DEF_NOT_FOUND = "FLOW_DEF_NOT_FOUND";

    /// <summary>流程未配置或未启用</summary>
    public const string FLOW_DEF_NOT_ENABLED = "FLOW_DEF_NOT_ENABLED";

    /// <summary>流程节点配置无效</summary>
    public const string FLOW_DEF_NODES_INVALID = "FLOW_DEF_NODES_INVALID";

    /// <summary>节点配置 JSON 解析失败</summary>
    public const string FLOW_DEF_JSON_INVALID = "FLOW_DEF_JSON_INVALID";

    /// <summary>流程缺少入口节点</summary>
    public const string FLOW_DEF_NO_ENTRY = "FLOW_DEF_NO_ENTRY";

    /// <summary>节点编码重复</summary>
    public const string FLOW_DEF_NODE_CODE_DUP = "FLOW_DEF_NODE_CODE_DUP";

    /// <summary>审批节点未配置审批人规则</summary>
    public const string FLOW_DEF_APPROVER_MISSING = "FLOW_DEF_APPROVER_MISSING";

    /// <summary>流程节点配置成环</summary>
    public const string FLOW_DEF_CYCLE = "FLOW_DEF_CYCLE";

    /// <summary>流程定义已删除</summary>
    public const string FLOW_DEF_DELETED = "FLOW_DEF_DELETED";

    /// <summary>启用中的流程不允许删除，请先停用</summary>
    public const string FLOW_DEF_DELETE_ENABLED_FORBIDDEN = "FLOW_DEF_DELETE_ENABLED_FORBIDDEN";

    /// <summary>该单据未绑定审批流</summary>
    public const string FLOW_BINDING_MISSING = "FLOW_BINDING_MISSING";

    /// <summary>条件节点无命中分支且未配置默认分支</summary>
    public const string FLOW_CONDITION_NO_BRANCH = "FLOW_CONDITION_NO_BRANCH";

    /// <summary>流程实例不存在</summary>
    public const string FLOW_INSTANCE_NOT_FOUND = "FLOW_INSTANCE_NOT_FOUND";

    /// <summary>流程已结束</summary>
    public const string FLOW_INSTANCE_FINISHED = "FLOW_INSTANCE_FINISHED";

    /// <summary>审批任务不存在</summary>
    public const string FLOW_TASK_NOT_FOUND = "FLOW_TASK_NOT_FOUND";

    /// <summary>仅任务归属人可处理</summary>
    public const string FLOW_TASK_OWNER_ONLY = "FLOW_TASK_OWNER_ONLY";

    /// <summary>该任务已处理或已失效</summary>
    public const string FLOW_TASK_ALREADY_HANDLED = "FLOW_TASK_ALREADY_HANDLED";

    /// <summary>驳回请选择退回目标节点</summary>
    public const string FLOW_RETURN_TARGET_REQUIRED = "FLOW_RETURN_TARGET_REQUIRED";

    /// <summary>退回目标节点不存在</summary>
    public const string FLOW_RETURN_TARGET_NOT_FOUND = "FLOW_RETURN_TARGET_NOT_FOUND";

    /// <summary>不能驳回至当前节点自身</summary>
    public const string FLOW_RETURN_TARGET_SELF = "FLOW_RETURN_TARGET_SELF";

    /// <summary>转办目标人无效</summary>
    public const string FLOW_TRANSFER_TARGET_INVALID = "FLOW_TRANSFER_TARGET_INVALID";

    /// <summary>加签人无效</summary>
    public const string FLOW_ADDSIGN_TARGET_INVALID = "FLOW_ADDSIGN_TARGET_INVALID";

    /// <summary>仅发起人可撤回</summary>
    public const string FLOW_WITHDRAW_FORBIDDEN = "FLOW_WITHDRAW_FORBIDDEN";

    /// <summary>审批已开始处理，无法撤回</summary>
    public const string FLOW_WITHDRAW_PROCESSING = "FLOW_WITHDRAW_PROCESSING";

    /// <summary>仅发起人可催办</summary>
    public const string FLOW_URGE_FORBIDDEN = "FLOW_URGE_FORBIDDEN";

    /// <summary>催办限频（4 小时内已催办过）</summary>
    public const string FLOW_URGE_RATE_LIMITED = "FLOW_URGE_RATE_LIMITED";

    /// <summary>当前没有待处理的审批任务</summary>
    public const string FLOW_URGE_NO_PENDING_TASK = "FLOW_URGE_NO_PENDING_TASK";

    /// <summary>分类名不能为空</summary>
    public const string FLOW_CATEGORY_NAME_REQUIRED = "FLOW_CATEGORY_NAME_REQUIRED";

    /// <summary>分类下还有流程，不允许删除</summary>
    public const string FLOW_CATEGORY_IN_USE = "FLOW_CATEGORY_IN_USE";

    /// <summary>委托不存在</summary>
    public const string FLOW_DELEGATE_NOT_FOUND = "FLOW_DELEGATE_NOT_FOUND";

    /// <summary>结束时间必须晚于开始时间</summary>
    public const string FLOW_DELEGATE_TIME_INVALID = "FLOW_DELEGATE_TIME_INVALID";

    /// <summary>不能委托给自己</summary>
    public const string FLOW_DELEGATE_SELF = "FLOW_DELEGATE_SELF";

    /// <summary>代理人不存在或已停用</summary>
    public const string FLOW_DELEGATE_AGENT_INVALID = "FLOW_DELEGATE_AGENT_INVALID";

    /// <summary>仅可删除自己的委托</summary>
    public const string FLOW_DELEGATE_DELETE_FORBIDDEN = "FLOW_DELEGATE_DELETE_FORBIDDEN";

    // ---------- 业务单据样板（biz_*） ----------
    /// <summary>报销单不存在</summary>
    public const string BIZ_EXPENSE_NOT_FOUND = "BIZ_EXPENSE_NOT_FOUND";

    /// <summary>采购申请单不存在</summary>
    public const string BIZ_PURCHASE_NOT_FOUND = "BIZ_PURCHASE_NOT_FOUND";

    /// <summary>仅草稿或被拒绝的单据可提交审批</summary>
    public const string BIZ_DOC_SUBMIT_STATUS_INVALID = "BIZ_DOC_SUBMIT_STATUS_INVALID";

    /// <summary>仅草稿状态的单据可修改/操作</summary>
    public const string BIZ_DOC_MODIFY_STATUS_INVALID = "BIZ_DOC_MODIFY_STATUS_INVALID";
}
