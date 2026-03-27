using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using System.Security.Claims;
using static OpenIddict.Abstractions.OpenIddictConstants;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using System.Data;

namespace EShop.Identity.Controllers
{
    [ApiController]
    public class AuthorizationController : ControllerBase
    {
        private readonly IOpenIddictApplicationManager _applicationManager;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;

        public AuthorizationController(
            IOpenIddictApplicationManager applicationManager,
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager)
        {
            _applicationManager = applicationManager;
            _userManager = userManager;
            _signInManager = signInManager;
        }

        [HttpGet("~/connect/authorize")]
        [HttpPost("~/connect/authorize")]
        public async Task<IActionResult> Authorize()
        {
            var request = HttpContext.GetOpenIddictServerRequest() ??
                throw new InvalidOperationException("无法获取 OpenID Connect 请求。");

            // 1. 检查当前用户在 Identity Server 这里登录了没有？(有没有 Cookie)
            var authenticateResult = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);

            if (!authenticateResult.Succeeded)
            {
                // 2. 如果没登录，直接重定向到你的登录页面 (比如 Account/Login)
                // 直接去掉参数名，干脆利落！
                return Challenge(
                    new AuthenticationProperties
                    {
                        RedirectUri = Request.PathBase + Request.Path + Request.QueryString
                    },
                    IdentityConstants.ApplicationScheme);
            }

            // 3. 如果已经登录了，开始给他签发一张带着他个人信息的 "门票" (ClaimsPrincipal)
            var claims = new List<Claim>
            {
                // 从当前登录的 Cookie 中获取用户 ID 和用户名
                new Claim(OpenIddictConstants.Claims.Subject, authenticateResult.Principal.FindFirstValue(ClaimTypes.NameIdentifier)!),
                new Claim(OpenIddictConstants.Claims.Name, authenticateResult.Principal.Identity!.Name!),
            };

            var identity = new ClaimsIdentity(claims, TokenValidationParameters.DefaultAuthenticationType);

            // 4. 分配权限 Scope (和你在 Password 模式里写的一样)
            identity.SetScopes(new[]
            {
                OpenIddictConstants.Scopes.OpenId,
                OpenIddictConstants.Scopes.Profile,
                "eshop.api"
            }.Intersect(request.GetScopes()));

            var principal = new ClaimsPrincipal(identity);

            // 5. 决定把哪些 Claim 暴露到 Token 里
            foreach (var claim in principal.Claims)
            {
                claim.SetDestinations(GetDestinations(claim, principal));
            }

            // 6. 核心动作：同意授权！OpenIddict 会自动生成一个 Code，通过 302 重定向发给业务系统 (eshop_web_spa)
            return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        // 辅助方法：决定信息存放在 AccessToken 还是 IdToken 里
        private static IEnumerable<string> GetDestinations(Claim claim, ClaimsPrincipal principal)
        {
            yield return OpenIddictConstants.Destinations.AccessToken;

            if (claim.Type == OpenIddictConstants.Claims.Name ||
                claim.Type == OpenIddictConstants.Claims.Subject)
            {
                yield return OpenIddictConstants.Destinations.IdentityToken;
            }
        }

        // POST: /connect/token
        [HttpPost("~/connect/token")]
        public async Task<IActionResult> Exchange()
        {
            // 1. 极其优雅地解析 OIDC 请求
            var request = HttpContext.GetOpenIddictServerRequest()
                ?? throw new InvalidOperationException("无法获取 OpenID Connect 请求。");

            // ====================================================================
            // 模式一：处理 "密码模式" (Password Flow - 用户第一次登录)
            // ====================================================================
            if (request.IsPasswordGrantType())
            {
                var user = await _userManager.FindByNameAsync(request.Username);
                if (user == null)
                {
                    return Forbid(
                        authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                        properties: new AuthenticationProperties(new Dictionary<string, string>
                        {
                            [OpenIddictServerAspNetCoreConstants.Properties.Error] = OpenIddictConstants.Errors.InvalidGrant,
                            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "用户名或密码错误"
                        }));
                }

                var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
                if (!result.Succeeded)
                {
                    return Forbid(
                        authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                        properties: new AuthenticationProperties(new Dictionary<string, string>
                        {
                            [OpenIddictServerAspNetCoreConstants.Properties.Error] = OpenIddictConstants.Errors.InvalidGrant,
                            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "用户名或密码错误"
                        }));
                }

                // 3. 登录成功，极其严谨地创建身份票据 (Identity)
                var identity = new ClaimsIdentity(
                    authenticationType: TokenValidationParameters.DefaultAuthenticationType,
                    nameType: OpenIddictConstants.Claims.Name,
                    roleType: OpenIddictConstants.Claims.Role);

                // 写入基础生命特征
                identity.AddClaim(OpenIddictConstants.Claims.Subject, await _userManager.GetUserIdAsync(user));
                identity.AddClaim(OpenIddictConstants.Claims.Name, await _userManager.GetUserNameAsync(user));

                // 👇👇👇 核心提权逻辑：从数据库查出该用户的所有角色，并挂载到身上 👇👇👇
                var roles = await _userManager.GetRolesAsync(user);
                foreach (var role in roles)
                {
                    identity.AddClaim(OpenIddictConstants.Claims.Role, role);
                }

                // 极其关键的权限边界：必须加上 OfflineAccess 才能生成 Refresh Token
                identity.SetScopes(new[]
                {
                    OpenIddictConstants.Scopes.OpenId,
                    OpenIddictConstants.Scopes.Profile,
                    "eshop.api",
                    OpenIddictConstants.Scopes.OfflineAccess
                }.Intersect(request.GetScopes()));

                var principal = new ClaimsPrincipal(identity);

                // 👇👇👇 终极放行魔法：告诉 OpenIddict 允许哪些信息被打包进 Token 👇👇👇
                foreach (var claim in principal.Claims)
                {
                    if (claim.Type == System.Security.Claims.ClaimTypes.Role || claim.Type == "role" ||
                        claim.Type == OpenIddictConstants.Claims.Subject || claim.Type == OpenIddictConstants.Claims.Name)
                    {
                        // 盖章放行！
                        claim.SetDestinations(OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken);
                    }
                }

                return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            // ====================================================================
            // 模式二：处理 "刷新令牌模式" (Refresh Token Flow - 极其硬核的无感续期)
            // ====================================================================
            else if (request.IsRefreshTokenGrantType() || request.IsAuthorizationCodeGrantType())
            {
                // 1. 极其冷酷地验证旧票据是否合法
                var authenticateResult = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
                var oldPrincipal = authenticateResult.Principal;

                if (oldPrincipal == null)
                {
                    return Forbid(
                        authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                        properties: new AuthenticationProperties(new Dictionary<string, string>
                        {
                            [OpenIddictServerAspNetCoreConstants.Properties.Error] = OpenIddictConstants.Errors.InvalidGrant,
                            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "提供的刷新票据无效或已过期。"
                        }));
                }

                // 👇👇👇 G老师的企业级防坑强化：刷新 Token 时，必须重新去数据库核对角色！ 👇👇👇
                // 防止用户在旧 Token 存活期间被撤销了 Admin 权限，却还能用旧的 Refresh Token 换出带有 Admin 的新 Token
                var userId = oldPrincipal.GetClaim(OpenIddictConstants.Claims.Subject);
                var user = await _userManager.FindByIdAsync(userId);

                if (user == null)
                {
                    // 人都被删了，直接拒绝刷新！
                    return Forbid(
                        authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                        properties: new AuthenticationProperties(new Dictionary<string, string>
                        {
                            [OpenIddictServerAspNetCoreConstants.Properties.Error] = OpenIddictConstants.Errors.InvalidGrant,
                            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "该用户已被系统除名。"
                        }));
                }

                // 创建一个全新的、干净的身份票据
                var identity = new ClaimsIdentity(oldPrincipal.Claims,
                    authenticationType: TokenValidationParameters.DefaultAuthenticationType,
                    nameType: OpenIddictConstants.Claims.Name,
                    roleType: OpenIddictConstants.Claims.Role);

                // 剔除旧票据里的历史角色（因为我们要重新赋予）
                var oldRoleClaims = identity.FindAll(OpenIddictConstants.Claims.Role).ToList();
                foreach (var claim in oldRoleClaims) { identity.RemoveClaim(claim); }

                // 重新查库，赋予最新鲜的角色权限！
                var currentRoles = await _userManager.GetRolesAsync(user);
                foreach (var role in currentRoles)
                {
                    identity.AddClaim(OpenIddictConstants.Claims.Role, role);
                }

                var newPrincipal = new ClaimsPrincipal(identity);

                // 👇👇👇 同样的放行魔法：确保新的角色信息被写入新生成的 Token 👇👇👇
                foreach (var claim in newPrincipal.Claims)
                {
                    if (claim.Type == System.Security.Claims.ClaimTypes.Role || claim.Type == "role" ||
                        claim.Type == OpenIddictConstants.Claims.Subject || claim.Type == OpenIddictConstants.Claims.Name)
                    {
                        claim.SetDestinations(OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken);
                    }
                }

                // 3. 极其干脆地签发全新的 Token 组合！
                return SignIn(newPrincipal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            // ====================================================================
            // 兜底：未知的授权模式，直接抹杀
            // ====================================================================
            throw new InvalidOperationException("不支持的授权模式。");
        }

        // 👇 1. 挂上保安：必须携带合法的 Access Token 才能访问这个接口！
        [Authorize(AuthenticationSchemes = OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)]
        [HttpGet("~/connect/userinfo")]
        [HttpPost("~/connect/userinfo")]
        public async Task<IActionResult> Userinfo()
        {
            // 2. 从极简 Token 里提取出最核心的用户 ID (Subject)
            var subject = User.GetClaim(OpenIddictConstants.Claims.Subject);
            if (string.IsNullOrEmpty(subject))
            {
                return Challenge(
                    authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                    properties: new AuthenticationProperties(new Dictionary<string, string>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = OpenIddictConstants.Errors.InvalidToken,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "Token 中缺少用户 ID。"
                    }));
            }

            // 3. 真正的企业级实战：用这个 ID 去查数据库！
            // 这里假设你注入了 _userManager，真实场景应该这样写：
             var user = await _userManager.FindByIdAsync(subject);
            if (user == null)
            {
                return Challenge(
               authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
               properties: new AuthenticationProperties(new Dictionary<string, string>
               {
                   [OpenIddictServerAspNetCoreConstants.Properties.Error] = OpenIddictConstants.Errors.InvalidGrant,
                   [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "未找到对应的用户信息"
               }));
            }

            // 4. 组装丰满的用户档案返回给前端 (绝对不要包含密码等极度敏感数据)
            var claims = new Dictionary<string, object>
            {
                [OpenIddictConstants.Claims.Subject] = subject,
                // 这里我们模拟一下从数据库查出来的详细业务数据
                ["name"] = User.Identity?.Name ?? "铁柱",
                ["email"] = "developer@eshop.com",
                ["phone"] = "138-8888-8888",
                ["location"] = "新加坡", // 模拟真实业务里的常驻地等扩展信息
                // 👇 1. 极其核心：使用 OIDC 标准的 role 声明！
                // 传一个数组，因为在真实的微服务里，一个用户通常拥有多个角色
                ["vip_level"] = "Gold",
                ["preferences"] = new[] { "Electronics", "Books" } // 甚至可以传数组或嵌套对象
            };

            // 1. 极其冷酷地验证旧票据是否合法
            var authenticateResult = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            var oldPrincipal = authenticateResult.Principal;
            var userId = oldPrincipal.GetClaim(OpenIddictConstants.Claims.Subject);
            //var user = await _userManager.FindByIdAsync(userId);
            // 重新查库，赋予最新鲜的角色权限！
            var currentRoles = await _userManager.GetRolesAsync(user);
            claims.Add(OpenIddictConstants.Claims.Role, currentRoles);
            return Ok(claims);
        }

        [HttpGet(".well-known/openid-configuration")]
        public FakeOidcConfig GetConfig()
        {
            return new FakeOidcConfig();
        }

        public class FakeOidcConfig
        {
            public string issuer { get; set; } = "http://localhost:5001";

            public string jwks_uri { get; set; } = "http://localhost:5001/.well-known/jwks.json";
        }
    }
}