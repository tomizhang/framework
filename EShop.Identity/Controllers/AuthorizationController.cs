using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using System.Security.Claims;
using static OpenIddict.Abstractions.OpenIddictConstants;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication;

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
            // 1. 解析请求 (OpenIddict 帮我们解析好了)
            var request = HttpContext.GetOpenIddictServerRequest();

            // 2. 处理 "密码模式" (Password Flow)
            if (request.IsPasswordGrantType())
            {
                var user = await _userManager.FindByNameAsync(request.Username);
                if (user == null) return Forbid(
                    authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                    properties: new Microsoft.AspNetCore.Authentication.AuthenticationProperties(new Dictionary<string, string>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "用户名或密码错误"
                    }));

                var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
                if (!result.Succeeded) return Forbid(
                     authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                     properties: new Microsoft.AspNetCore.Authentication.AuthenticationProperties(new Dictionary<string, string>
                     {
                         [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                         [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "用户名或密码错误"
                     }));

                // 3. 登录成功，创建票据 (Principal)
                var identity = new ClaimsIdentity(
                    authenticationType: TokenValidationParameters.DefaultAuthenticationType,
                    nameType: Claims.Name,
                    roleType: Claims.Role);

                identity.AddClaim(Claims.Subject, await _userManager.GetUserIdAsync(user));
                identity.AddClaim(Claims.Name, await _userManager.GetUserNameAsync(user));

                // 必须加上 Scope 权限
                identity.SetScopes(new[]
                {
                    Scopes.OpenId,
                    Scopes.Profile,
                    "eshop.api",
                    // 👇👇👇 把 OfflineAccess 加到允许发放的名单里 👇👇👇
                    OpenIddictConstants.Scopes.OfflineAccess
                }.Intersect(request.GetScopes()));

                var principal = new ClaimsPrincipal(identity);

                // 4. 返回 Token
                return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }
            else if (request.IsAuthorizationCodeGrantType())
            {
                // 1. 获取在 Authorize() 网页登录成功时，封印在 Code 里的用户凭证
                var authenticateResult = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

                // 2. 提取出完整的用户信息
                var principal = authenticateResult.Principal;

                // 3. 核心动作：同意用 Code 换取 Access Token！
                return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }
            // 👇👇👇 新增：处理 "刷新令牌模式" (Refresh Token Flow) 👇👇👇
            else if (request.IsRefreshTokenGrantType())
            {
                // 1. 获取封印在旧 Refresh Token 里的用户凭证 (OpenIddict 已经帮我们验证了它的合法性和有效期)
                var authenticateResult = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

                // 2. 把用户信息原封不动地提出来
                var principal = authenticateResult.Principal;

                // 3. 核心动作：直接签发一对全新的 Access Token 和 Refresh Token！
                return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }
            // 如果有人拿其他乱七八糟的模式来请求，直接打回票
            throw new InvalidOperationException("不支持的授权模式。");
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