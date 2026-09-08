using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using NetBase.Api.Auth;
using NetBase.Api.Filters;
using NetBase.Api.Middlewares;
using NetBase.Model.Entities;
using NetBase.Service.Sys;
using Hangfire;
using NetBase.Api.Services;
using NetBase.Service.Sys;
using Hangfire;
using NetBase.Common.Users;
using NetBase.Middleware;
using NetBase.Repository;
using NetBase.Repository.DbContexts;
using NetBase.Service;
using NetBase.Service.Sys;
using Hangfire;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// 日志：Serilog（配置见 appsettings.json 的 Serilog 节点）
builder.Host.UseSerilog((context, configuration) => configuration.ReadFrom.Configuration(context.Configuration));

// 分层服务注册
builder.Services.AddNetBaseRepository(builder.Configuration);
builder.Services.AddNetBaseService(builder.Configuration);
builder.Services.AddNetBaseMiddleware(builder.Configuration);

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
});

// 健康检查：数据库连通性探针（供负载均衡/K8s 使用）
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database");

// 定时任务：Hangfire（SqlServer 存储，免费版核心组件）
builder.Services.Configure<NetBase.Api.Jobs.HangfireOptions>(builder.Configuration.GetSection(NetBase.Api.Jobs.HangfireOptions.SectionName));
var hangfireOptions = builder.Configuration.GetSection(NetBase.Api.Jobs.HangfireOptions.SectionName).Get<NetBase.Api.Jobs.HangfireOptions>() ?? new NetBase.Api.Jobs.HangfireOptions();
var hangfireConnectionString = builder.Configuration.GetConnectionString("Hangfire")
    ?? builder.Configuration.GetSection("Db:ConnectionString").Value
    ?? "Server=localhost;Database=NetBase;Uid=sa;Pwd=Abcd1234;TrustServerCertificate=True;";
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(hangfireConnectionString, new Hangfire.SqlServer.SqlServerStorageOptions
    {
        CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
        SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
        QueuePollInterval = TimeSpan.FromSeconds(15)
    }));
builder.Services.AddHangfireServer();
builder.Services.AddScoped<NetBase.Api.Jobs.ISystemJobService, NetBase.Api.Jobs.SystemJobService>();

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

        // 会话校验：签名/有效期之外，验证会话表存在性（登出/强制下线/停用立即生效）
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var tokenId = context.Principal?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
                if (string.IsNullOrEmpty(tokenId))
                {
                    context.Fail("无效令牌");
                    return;
                }
                var sessionRepository = context.HttpContext.RequestServices
                    .GetRequiredService<NetBase.Repository.Repositories.IRepository<NetBase.Model.Entities.SysUserSession>>();
                var session = await sessionRepository.GetFirstAsync(x => x.TokenId == tokenId);
                if (session == null || session.ExpireTime <= DateTime.Now)
                {
                    context.Fail("会话已失效");
                }
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

// Hangfire Dashboard（默认仅本机访问；生产建议保持关闭，管理动作走系统管理页）
if (hangfireOptions.DashboardEnabled)
{
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = new[] { new NetBase.Api.Jobs.HangfireDashboardAuthFilter() }
    });
}

// 注册内置定时任务（应用启动时；Scoped 服务须在 Scope 内解析）
using (var jobScope = app.Services.CreateScope())
{
    jobScope.ServiceProvider.GetRequiredService<NetBase.Api.Jobs.ISystemJobService>().RegisterJobs();
}
app.MapControllers();

// SPA 回退：非 API 路由刷新时返回 index.html（仅前端产物存在时）
if (File.Exists(indexPage))
{
    app.MapFallbackToFile("index.html");
}

// 健康检查端点（含数据库探针）
app.MapHealthChecks("/health");

app.Run();
