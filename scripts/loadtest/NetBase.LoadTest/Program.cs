// NetBase 接口压测：NBomber 阶梯加压（每个档位独立一次 Run，控制台输出该档位 RPS/延迟百分位）。
// 用法：先 curl 登录取 accessToken，设 NETBASE_TOKEN 环境变量后 `dotnet run`（可选参数 baseUrl）。
// 场景：匿名缓存（captcha/enabled）/ 认证缓存读（dict by-code）/ 认证列表（user 分页）/ 聚合（dashboard/stats）/ 低频登录。
using System.Net.Http.Headers;
using System.Text;
using NBomber.Contracts;
using NBomber.CSharp;

var baseUrl = (args.Length > 0 ? args[0] : "http://localhost:8680").TrimEnd('/');
var token = Environment.GetEnvironmentVariable("NETBASE_TOKEN");

using var http = new HttpClient(new SocketsHttpHandler { MaxConnectionsPerServer = 512 })
{
    BaseAddress = new Uri(baseUrl),
    Timeout = TimeSpan.FromSeconds(10)
};
if (!string.IsNullOrEmpty(token))
{
    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
}

static ScenarioProps CreateGetScenario(HttpClient http, string name, string path) =>
    Scenario.Create(name, async _ =>
    {
        var resp = await http.GetAsync(path);
        return resp.IsSuccessStatusCode
            ? Response.Ok()
            : Response.Fail();
    });

void RunLadder(string name, string path, int[] rates)
{
    Console.WriteLine($"\n########## 场景 {name}：GET {path} ##########");
    foreach (var rate in rates)
    {
        var scenarioName = $"{name}@{rate}rps";
        var scenario = CreateGetScenario(http, scenarioName, path)
            // Inject = 开放模型恒定到达率：每秒注入 rate 个请求，持续 25s
            .WithLoadSimulations(Simulation.Inject(rate, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(25)));

        Console.WriteLine($"\n===== {scenarioName} =====");
        NBomberRunner.RegisterScenarios(scenario).WithoutReports().Run();
    }
}

// 1. 匿名 + 全缓存命中（读参数缓存）：管道 + nginx + L1 缓存的天花板
RunLadder("anon_cached", "/api/v1/auth/captcha/enabled", [100, 300, 600, 1000]);

// 2. 认证 + 字典缓存读（10 分钟缓存命中）
RunLadder("authed_dict", "/api/v1/sys/dict/data/by-code/sys_sex", [100, 300, 600, 1000]);

// 3. 认证 + 分页列表（权限校验 + PG 查询 + 用户名关联）
RunLadder("authed_userlist", "/api/v1/sys/user/page?pageIndex=1&pageSize=20", [50, 150, 300, 500]);

// 4. 认证 + 聚合统计（多表聚合，最重）
RunLadder("authed_dashboard", "/api/v1/dashboard/stats", [25, 75, 150, 300]);

// 5. 低频登录（每 8 秒 1 次 ≈ 7.5 次/分，低于 10 次/分 限流；测 bcrypt 校验 + 会话写库的真实延迟）
Console.WriteLine("\n########## 场景 login@lowrate：POST /api/v1/auth/login ##########");
var loginScenario = Scenario.Create("login@lowrate", async _ =>
{
    await Task.Delay(8000);
    var content = new StringContent("""{"userName":"admin","password":"Abcd1234"}""", Encoding.UTF8, "application/json");
    var resp = await http.PostAsync("/api/v1/auth/login", content);
    return resp.IsSuccessStatusCode ? Response.Ok() : Response.Fail();
}).WithLoadSimulations(Simulation.KeepConstant(copies: 1, during: TimeSpan.FromSeconds(80)));

NBomberRunner.RegisterScenarios(loginScenario).WithoutReports().Run();
