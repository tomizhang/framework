using EShop.Identity;
using Duende.IdentityServer.Test; // 引用测试用户

var builder = WebApplication.CreateBuilder(args);

// 1. 添加 IdentityServer 服务
builder.Services.AddIdentityServer()
    .AddInMemoryIdentityResources(Config.IdentityResources) // 加载刚才定义的身份资源
    .AddInMemoryApiScopes(Config.ApiScopes)           // 加载 API 范围
    .AddInMemoryClients(Config.Clients)               // 加载客户端
    .AddTestUsers(new List<TestUser>
    {
        // 创建一个内存测试用户
        new TestUser
        {
            SubjectId = "1",
            Username = "admin",
            Password = "password",
            Claims =
            {
                new System.Security.Claims.Claim("name", "G老师"),
                new System.Security.Claims.Claim("role", "admin")
            }
        }
    })
    .AddDeveloperSigningCredential(); // 生成临时的签名证书 (生产环境要用真证书)

var app = builder.Build();

// 2. 启用 IdentityServer 中间件
app.UseIdentityServer();

app.MapGet("/", () => "Hello from Auth Center (IdentityServer)!");

app.Run();