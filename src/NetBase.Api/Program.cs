using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.AspNetCore.Mvc;
using NetBase.Api.Filters;
using NetBase.Middleware;
using NetBase.Repository;
using NetBase.Repository.DbContexts;
using NetBase.Service;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// 日志：Serilog（配置见 appsettings.json 的 Serilog 节点）
builder.Host.UseSerilog((context, configuration) => configuration.ReadFrom.Configuration(context.Configuration));

// 分层服务注册
builder.Services.AddNetBaseRepository(builder.Configuration);
builder.Services.AddNetBaseService();
builder.Services.AddNetBaseMiddleware(builder.Configuration);

// 控制器 + 全局过滤器（模型验证、异常处理）
builder.Services
    .AddControllers(options =>
    {
        options.Filters.Add<ModelValidationFilter>();
        options.Filters.Add<GlobalExceptionFilter>();
    })
    .AddJsonOptions(options =>
    {
        // 中文不转义，前端可读
        options.JsonSerializerOptions.Encoder = JavaScriptEncoder.Create(UnicodeRanges.All);
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        // 关闭 [ApiController] 默认的 400 ProblemDetails 响应，统一走 ModelValidationFilter
        options.SuppressModelStateInvalidFilter = true;
    });

// OpenAPI：.NET 10 内置文档 + Swagger UI 可视化
builder.Services.AddOpenApi();

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

app.UseSerilogRequestLogging();
app.UseHttpsRedirection();
app.MapControllers();

// 健康检查端点
app.MapGet("/health", () => Results.Ok(new { status = "ok", time = DateTime.Now }));

app.Run();
