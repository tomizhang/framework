using EShop.Identity.Data;
using Microsoft.AspNetCore.Identity;
using OpenIddict.Abstractions;

namespace EShop.Identity
{
    public class TestDataWorker : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;

        public TestDataWorker(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();

            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

            // 1. 初始化核心角色
            string[] roleNames = { "Admin", "User", "Manager" };
            foreach (var roleName in roleNames)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await context.Database.EnsureCreatedAsync();

            var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();

            // 注册 SSO 专用客户端
            if (await manager.FindByClientIdAsync("eshop_web_spa") == null)
            {
                await manager.CreateAsync(new OpenIddictApplicationDescriptor
                {
                    ClientId = "eshop_web_spa",
                    // 对于纯前端 SPA (Vue/React)，甚至可以不需要 Secret，只需配置 RedirectUris

                    // 👇 极其关键：纯前端绝对不能有 ClientSecret = "..." 这行代码！删掉它！
                    //ClientSecret = "spa_secret",
                    // 👇 极其关键：告诉发证局，这是一个没有密码的公共客户端，但它必须使用 PKCE 安全机制
                    Requirements =
                    {
                        OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange
                    },
                    DisplayName = "EShop 前端商城网站",

                    // 👇 极其关键：登录成功后，认证中心要把授权码发到哪个网址？
                    // 假设你的前端运行在 7002 端口
                    RedirectUris = { new Uri("http://localhost:7002/signin-oidc"),new Uri("http://localhost:7002/index.html") },
                    PostLogoutRedirectUris = { new Uri("http://localhost:7002/signout-callback-oidc"),new Uri("http://localhost:7002/index.html") },

                    Permissions =
                    {

                                // 👇👇👇 1. 允许这个客户端使用 "刷新令牌模式" 换取新 Token 👇👇👇
                        OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
                
                        // 👇👇👇 2. 允许这个客户端申请 "offline_access" (离线访问) 权限范围 👇👇👇
                        OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConstants.Scopes.OfflineAccess,

                        // 端点权限
                        OpenIddictConstants.Permissions.Endpoints.Authorization,
                        OpenIddictConstants.Permissions.Endpoints.Token,
                        OpenIddictConstants.Permissions.Endpoints.EndSession,
                
                        // 模式权限：允许使用授权码模式 (SSO 的灵魂)
                        OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                        OpenIddictConstants.Permissions.ResponseTypes.Code,
                        OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
                
                        // 允许申请的 Scope
                        OpenIddictConstants.Permissions.Prefixes.Scope + "eshop.api",
                        OpenIddictConstants.Permissions.Prefixes.Scope + "openid",
                        OpenIddictConstants.Permissions.Prefixes.Scope + "profile"
                    }
                });
            }
            #region 其它系统
            // 场景 1：如果是纯前端 (Vue/React) 或手机 App 接入
            if (await manager.FindByClientIdAsync("admin_spa") == null)
            {
                await manager.CreateAsync(new OpenIddictApplicationDescriptor
                {
                    ClientId = "admin_spa",
                    DisplayName = "后台管理系统 (Vue)",
                    // 纯前端不安全，不需要 Secret，但必须走带有 PKCE 的授权码模式
                    RedirectUris = { new Uri("http://localhost:8000/callback") },
                    Permissions = { /* 给授权码模式的权限，参考你之前的配置 */ }
                });
            }

            // 场景 2：如果是后端微服务（比如一个外部的 Java/Go 系统）需要直接调你的 API
            if (await manager.FindByClientIdAsync("partner_backend") == null)
            {
                await manager.CreateAsync(new OpenIddictApplicationDescriptor
                {
                    ClientId = "partner_backend",
                    ClientSecret = "super_strong_password_123", // 后端安全，必须配置 Secret
                    DisplayName = "第三方数据分析系统",
                    // 后端通常不需要跳页面，直接用“客户端凭证模式 (Client Credentials)”发 Token
                    Permissions =
                    {
                        OpenIddictConstants.Permissions.Endpoints.Token,
                        OpenIddictConstants.Permissions.GrantTypes.ClientCredentials,
                        OpenIddictConstants.Permissions.Prefixes.Scope + "eshop.api"
                    }
                });
            }
            #endregion
            // 👇👇👇 【核心修改】推土机模式：先查找，如果有就删掉 👇👇👇
            var client = await manager.FindByClientIdAsync("postman_client");
            if (client != null)
            {
                await manager.DeleteAsync(client); // 删掉旧的
            }

            // 👇👇👇 重新创建 (名字还叫 postman_client，不用改 v2 了) 👇👇👇
            await manager.CreateAsync(new OpenIddictApplicationDescriptor
            {
                ClientId = "postman_client", // 这里的名字必须和 Postman 里填的一样
                ClientSecret = "secret",
                DisplayName = "Postman Client",
                Permissions ={
                        // 1. 允许访问端点
                        OpenIddictConstants.Permissions.Endpoints.Token,

                        // 2. 允许的授权模式
                        OpenIddictConstants.Permissions.GrantTypes.Password,
                        OpenIddictConstants.Permissions.GrantTypes.ClientCredentials,
                        OpenIddictConstants.Permissions.GrantTypes.RefreshToken,

                        // 3. 允许的 Scope (权限范围) 👇 把这些都加上
                        OpenIddictConstants.Permissions.Prefixes.Scope + "client_app", // 修复了拼写
                        OpenIddictConstants.Permissions.Prefixes.Scope + "eshop.api",  // 👈 允许申请 eshop.api
                        OpenIddictConstants.Permissions.Prefixes.Scope + "openid",     // 👈 允许申请 openid (标准)
                        OpenIddictConstants.Permissions.Prefixes.Scope + "profile"     // 👈 允许申请 profile (标准)
                    }

            });

            // 创建测试用户 (保持不变)
            //var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            if (await userManager.FindByNameAsync("admin") == null)
            {
                var user = new IdentityUser { UserName = "admin", Email = "admin@eshop.com" };
                var result =  await userManager.CreateAsync(user, "Password123!");
                if (result.Succeeded)
                {
                    // 极其关键：把用户塞进管理员角色里！
                    await userManager.AddToRolesAsync(user,new List<string> { "Admin" , "Manager" });
                }
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}