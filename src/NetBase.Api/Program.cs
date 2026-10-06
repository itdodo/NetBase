using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using NetBase.Api.Auth;
using NetBase.Api.Filters;
using NetBase.Api.Hubs;
using NetBase.Api.Middlewares;
using NetBase.Api.Realtime;
using NetBase.Model.Entities;
using NetBase.Service.Sys;
using Hangfire;
using Hangfire.PostgreSql;
using NetBase.Api.Services;
using NetBase.Common.Realtime;
using NetBase.Common.Results;
using NetBase.Common.Users;
using NetBase.Middleware;
using NetBase.Repository;
using NetBase.Repository.DbContexts;
using NetBase.Service;
using Serilog;

// Npgsql timestamptz 兼容：本框架时间字段全程 DateTime.Now（Local Kind），开启 legacy 时间戳语义
// （Npgsql 6+ 默认 timestamptz 仅接受 UTC；此开关必须在进程内首次使用 Npgsql 之前设置，故置于文件首）
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// 敏感配置可逆加密主密钥（SMTP 授权码等）：派生自 Jwt:SecretKey，须早于任何加解密使用
NetBase.Common.Security.SensitiveCrypto.Init(
    builder.Configuration.GetSection("Jwt:SecretKey").Value
    ?? throw new InvalidOperationException("缺少 Jwt:SecretKey 配置（SensitiveCrypto 主密钥来源）"));

// 日志：Serilog（配置见 appsettings.json 的 Serilog 节点）
builder.Host.UseSerilog((context, configuration) => configuration.ReadFrom.Configuration(context.Configuration));

// 分层服务注册
builder.Services.AddNetBaseRepository(builder.Configuration);
builder.Services.AddNetBaseService(builder.Configuration);
builder.Services.AddNetBaseMiddleware(builder.Configuration);
// 业务模块注册（业务代码只进 Biz 目录）
builder.Services.AddNetBaseBiz();

// 当前用户（认证接入后自动从 Claims 解析，业务代码已按此取审计操作人）
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ICurrentUserService, CurrentUserService>();
builder.Services.AddSingleton<NetBase.Repository.Auditing.IOperatorProvider, NetBase.Api.Auditing.OperatorProvider>();

// 控制器 + 全局过滤器（模型验证、异常处理）
builder.Services
    .AddControllers(options =>
    {
        options.Filters.Add<ModelValidationFilter>();
        options.Filters.Add<GlobalExceptionFilter>();
        options.Filters.Add<OperationLogFilter>();
        options.Filters.Add<NoRepeatSubmitFilter>();
        options.Filters.Add<UnitOfWorkFilter>();
    })
    .AddJsonOptions(options =>
    {
        // 中文不转义，前端可读
        options.JsonSerializerOptions.Encoder = JavaScriptEncoder.Create(UnicodeRanges.All);
        // 雪花ID配套：允许前端传字符串形式的数字；写出侧仅 ID 字段经 LongToStringConverter 转字符串（精准）
        options.JsonSerializerOptions.NumberHandling =
            System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString;
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        // 关闭 [ApiController] 默认的 400 ProblemDetails 响应，统一走 ModelValidationFilter
        options.SuppressModelStateInvalidFilter = true;
    });

// 操作日志：自动记录全部写操作（参数脱敏）；防重复提交过滤器
builder.Services.AddScoped<OperationLogFilter>();
builder.Services.AddScoped<NoRepeatSubmitFilter>();
builder.Services.AddScoped<UnitOfWorkFilter>();
builder.Services.Configure<FileStorageOptions>(builder.Configuration.GetSection(FileStorageOptions.SectionName));

// 登录限流：每 IP 每分钟最多 10 次登录尝试（防暴力破解，与失败锁定互为补充）
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", context =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            $"login:{context.Connection.RemoteIpAddress}",
            _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1)
            }));
    // 验证码获取：每 IP 每分钟 20 次（防刷缓存空间）
    options.AddPolicy("captcha", context =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            $"captcha:{context.Connection.RemoteIpAddress}",
            _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1)
            }));
});

// 健康检查：数据库连通性探针（供负载均衡/K8s 使用）
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database");

// 定时任务：Hangfire（PostgreSQL 存储，免费版核心组件）
builder.Services.Configure<NetBase.Api.Jobs.HangfireOptions>(builder.Configuration.GetSection(NetBase.Api.Jobs.HangfireOptions.SectionName));
var hangfireOptions = builder.Configuration.GetSection(NetBase.Api.Jobs.HangfireOptions.SectionName).Get<NetBase.Api.Jobs.HangfireOptions>() ?? new NetBase.Api.Jobs.HangfireOptions();
var hangfireConnectionString = builder.Configuration.GetConnectionString("Hangfire")
    ?? builder.Configuration.GetSection("Db:ConnectionString").Value
    ?? "Host=localhost;Port=5544;Database=netbase;Username=netbase;Password=netbase123";
builder.Services.AddHangfire((sp, config) => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    // 作业执行日志过滤器：所有作业执行前后自动落 sys_job_log（成功/失败/耗时/触发方式）
    .UseFilter(new NetBase.Api.Jobs.JobExecutionLogFilter(
        sp.GetRequiredService<IServiceScopeFactory>(),
        sp.GetRequiredService<ILogger<NetBase.Api.Jobs.JobExecutionLogFilter>>()))
    .UsePostgreSqlStorage(hangfireConnectionString, new Hangfire.PostgreSql.PostgreSqlStorageOptions
    {
        SchemaName = "hangfire",
        QueuePollInterval = TimeSpan.FromSeconds(15),
        InvisibilityTimeout = TimeSpan.FromMinutes(5),
        UseSlidingInvisibilityTimeout = true
    }));
builder.Services.AddHangfireServer();
builder.Services.AddScoped<NetBase.Api.Jobs.ISystemJobService, NetBase.Api.Jobs.SystemJobService>();
// 数据备份：pg_dump 全量 + 上传文件镜像（Backup 节点配置，失败经站内信通知管理员）
builder.Services.Configure<NetBase.Api.Jobs.BackupOptions>(builder.Configuration.GetSection(NetBase.Api.Jobs.BackupOptions.SectionName));
builder.Services.AddScoped<NetBase.Api.Jobs.IBackupService, NetBase.Api.Jobs.BackupService>();

// 实时通知：SignalR（用户连接映射 + 落库推送双写）
builder.Services.AddSingleton<NetBase.Api.Hubs.IUserConnectionMapping, NetBase.Api.Hubs.UserConnectionMapping>();
builder.Services.AddScoped<NetBase.Common.Realtime.INotifyService, NetBase.Api.Realtime.NotifyService>();
builder.Services.AddSignalR();
// 多实例部署时启用 Redis backplane（SignalR:UseRedisBackplane=true 且需 Redis 可用）：
// if (builder.Configuration.GetValue<bool>("SignalR:UseRedisBackplane"))
//     builder.Services.AddSignalR().AddStackExchangeRedis(cacheRedisConnectionString);

// 认证授权：JWT Bearer + 动态权限码策略
var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
builder.Services.Configure<JwtOptions>(jwtSection);
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwt = jwtSection.Get<JwtOptions>() ?? new JwtOptions();
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SecretKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        // 事件：SignalR WS 握手从 query 取 token + 会话校验（登出/强制下线/停用立即生效）
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) &&
                    context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            },
            OnTokenValidated = async context =>
            {
                var tokenId = context.Principal?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
                if (string.IsNullOrEmpty(tokenId))
                {
                    context.Fail("无效令牌");
                    return;
                }
                // 会话存在性缓存（60 秒）：免去每请求一次会话表查询——500qps 下该查询纯属 DB 陪跑。
                // 登出/踢线/批量踢线/改密清会话各路径均已同步清标记，无宽限窗口
                var cache = context.HttpContext.RequestServices
                    .GetRequiredService<NetBase.Common.Cache.ICacheService>();
                var validKey = $"session:valid:{tokenId}";
                if (await cache.GetAsync<bool>(validKey))
                {
                    return;
                }
                var sessionRepository = context.HttpContext.RequestServices
                    .GetRequiredService<NetBase.Repository.Repositories.IRepository<NetBase.Model.Entities.SysUserSession>>();
                var session = await sessionRepository.GetFirstAsync(x => x.TokenId == tokenId);
                if (session == null || session.ExpireTime <= DateTime.Now)
                {
                    context.Fail("会话已失效");
                    return;
                }
                await cache.SetAsync(validKey, true, TimeSpan.FromSeconds(60));
            },
            // 统一返回：管道 401 也带 ApiResult 结构（HTTP 状态仍为真实 401，前端按状态码走刷新/回登录）
            OnChallenge = async context =>
            {
                context.HandleResponse();
                await WriteAuthError(context.HttpContext, ApiResultCode.Unauthorized,
                    "未登录或登录已过期，请重新登录", ErrorCodes.AUTH_SESSION_EXPIRED);
            },
            // 统一返回：管道 403（[HasPermission] 不通过）带 ApiResult 结构
            OnForbidden = async context =>
            {
                await WriteAuthError(context.HttpContext, ApiResultCode.Forbidden,
                    "没有操作权限，请联系管理员", ErrorCodes.AUTH_FORBIDDEN);
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

// OpenAPI：.NET 10 内置文档 + Swagger UI 可视化
builder.Services.AddOpenApi(options =>
{
    // Swagger UI 对 OpenAPI 3.1 渲染兼容性一般，显式输出 3.0
    options.OpenApiVersion = Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_0;
    // Bearer 认证：Swagger UI 出现 Authorize 按钮
    options.AddDocumentTransformer<BearerSecurityDocumentTransformer>();
});

// CORS：跨域部署前端时在 appsettings.json 的 Cors:AllowedOrigins 配置来源白名单；
// 未配置时仅开发环境放开，生产默认同源（前端由 API 托管或 Nginx 反代）
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    if (corsOrigins.Length > 0)
    {
        policy.WithOrigins(corsOrigins);
    }
    else if (builder.Environment.IsDevelopment())
    {
        policy.SetIsOriginAllowed(_ => true);
    }
    policy.AllowAnyHeader().AllowAnyMethod();
}));

var app = builder.Build();

// 数据库初始化：CodeFirst 建表 + 种子数据（Db:InitEnabled=true 时执行，失败不阻断启动）
if (app.Configuration.GetValue<bool>($"{SqlSugarOptions.SectionName}:InitEnabled"))
{
    using (var scope = app.Services.CreateScope())
    {
        try
        {
            scope.ServiceProvider.GetRequiredService<DbSeeder>().Run();
        }
        catch (Exception ex)
        {
            app.Logger.LogError(ex, "数据库初始化失败，请检查连接串或手动设置 Db:InitEnabled=false 后排查");
        }
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi(); // /openapi/v1.json
    app.UseSwaggerUI(options =>
    {
        options.DocumentTitle = "NetBase API";
        options.SwaggerEndpoint("/openapi/v1.json", "NetBase v1");
    }); // /swagger
}

// 前端静态托管：wwwroot 存在前端构建产物时启用（Docker 单容器部署形态）
var indexPage = Path.Combine(app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot"), "index.html");
if (File.Exists(indexPage))
{
    app.UseStaticFiles();
}

app.UseSerilogRequestLogging();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseCors();
app.UseHttpsRedirection();
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<DataScopeMiddleware>();
app.UseAuthorization();

// 统一返回：/api 未匹配路由返回 ApiResult 404。⚠️ 不能用 GetEndpoint()==null 前置判断——
// MapFallbackToFile 为所有路径注册 fallback endpoint，该判断恒 false（实测曾因此失效返回 HTML）。
// 正确方案：自定义 fallback（在 fallback 内区分 /api 与 SPA 路由，见文件尾 MapFallback）。


// Hangfire Dashboard（默认仅本机访问；生产建议保持关闭，管理动作走系统管理页）
if (hangfireOptions.DashboardEnabled)
{
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = new[] { new NetBase.Api.Jobs.HangfireDashboardAuthFilter() }
    });
}

// Hub 映射（JWT 认证已支持 query access_token）
app.MapHub<NotifyHub>("/hubs/notify");

// 注册内置定时任务（应用启动时；Scoped 服务须在 Scope 内解析）
using (var jobScope = app.Services.CreateScope())
{
    jobScope.ServiceProvider.GetRequiredService<NetBase.Api.Jobs.ISystemJobService>().RegisterJobs();
}
app.MapControllers();

// SPA 回退：非 API 路由刷新时返回 index.html；/api 未匹配路由返回 ApiResult 404（统一返回约定）
if (File.Exists(indexPage))
{
    app.MapFallback(async (HttpContext context) =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = ApiResultCode.NotFound;
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsync(JsonSerializer.Serialize(
                new ApiResult { Code = ApiResultCode.NotFound, Message = "接口不存在", ErrorCode = ErrorCodes.COMMON_ROUTE_NOT_FOUND },
                new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            return;
        }
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.SendFileAsync(indexPage);
    });
}

// 健康检查端点（含数据库探针）
app.MapHealthChecks("/health");

app.Run();

/// <summary>认证管道错误统一写 ApiResult 结构（HTTP 状态码保持真实 401/403）</summary>
static async Task WriteAuthError(HttpContext context, int code, string message, string errorCode)
{
    var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
    context.Response.StatusCode = code;
    context.Response.ContentType = "application/json; charset=utf-8";
    await context.Response.WriteAsync(JsonSerializer.Serialize(
        new ApiResult { Code = code, Message = message, ErrorCode = errorCode }, jsonOptions));
}
