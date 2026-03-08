using EShop.Identity;
using EShop.Identity.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using System.Text; // 引用

var builder = WebApplication.CreateBuilder(args);

// 1. 配置数据库 (使用内存数据库，为了快速演示)
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseInMemoryDatabase(nameof(ApplicationDbContext));

    // 注册 OpenIddict 的实体 (Client, Authorization, Token 等)
    options.UseOpenIddict();
});

// 2. 配置 ASP.NET Core Identity (用户管理)
builder.Services.AddIdentity<IdentityUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddAuthentication()
    .AddGitHub(options =>
    {
        // 填入你刚才在 GitHub 申请到的 ID 和 Secret
        options.ClientId = "Ov2xxxx";
        options.ClientSecret = "1acf0xxx";

        // 我们想获取用户的邮箱，所以加上这个 Scope
        options.Scope.Add("user:email");

        // 👇👇👇 补上这行绝对核心的代码：告诉 GitHub 使用 Identity 的外部登录机制接管 👇👇👇
        options.SignInScheme = IdentityConstants.ExternalScheme;
    });

// 3. 配置 OpenIddict (核心配置)
builder.Services.AddOpenIddict()

    // 3.1 核心组件配置
    .AddCore(options =>
    {
        // 告诉它使用 EF Core 存数据
        options.UseEntityFrameworkCore()
               .UseDbContext<ApplicationDbContext>();
    })

    // 3.2 服务端配置 (Server)
    .AddServer(options =>
    {
        // 👇👇👇 必须显式声明这三个核心端点的门牌号 👇👇👇
        options.SetTokenEndpointUris("/connect/token")
               .SetAuthorizationEndpointUris("/connect/authorize")
               .SetEndSessionEndpointUris("/connect/logout");

        // 允许的授权模式
        options.AllowPasswordFlow();
        options.AllowClientCredentialsFlow();
        options.AllowRefreshTokenFlow();

        // SSO 核心模式
        options.AllowAuthorizationCodeFlow();
        options.RequireProofKeyForCodeExchange(); // 强制开启 PKCE (极致安全的行业标准)

        // 👇👇👇 【核心修复在这里】明确告诉服务器，这些 Scope 是合法的 👇👇👇
        options.RegisterScopes("client_app", "eshop.api", "openid", "profile");

        // 👇👇👇 新增这一行：关闭强加密，强制输出标准 JWT 👇👇👇
        options.DisableAccessTokenEncryption();
        var secretKey = "MySuperSecretKey_MustBeLongerThan16Chars";
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        options.AddSigningKey(securityKey);
        // 注册签名证书
        options.AddDevelopmentEncryptionCertificate()
               .AddDevelopmentSigningCertificate();

        // 接入 ASP.NET Core 的管道
        options.UseAspNetCore()
               .EnableTokenEndpointPassthrough()
               // 👇👇👇 补上下面这行！告诉 OpenIddict 把授权请求放行给我们的 Controller 👇👇👇
               .EnableAuthorizationEndpointPassthrough()
               // 👇👇👇 既然在做 SSO，顺便把登出请求也放行了（如果用的是 OpenIddict 6.x，可能叫 EnableEndSessionEndpointPassthrough）👇👇👇
               .EnableEndSessionEndpointPassthrough();
    })

    // 3.3 验证配置 (Validation)
    .AddValidation(options =>
    {
        options.UseLocalServer();
        options.UseAspNetCore();
    });

// 添加控制器 (OpenIddict 需要我们写 Controller)
//让项目支持 Controller 和 HTML 视图
builder.Services.AddControllersWithViews();

// --- 自动初始化数据的后台任务 (下面会写) ---
builder.Services.AddHostedService<TestDataWorker>();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultControllerRoute();

app.Run();

