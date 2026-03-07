using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using System.Security.Claims;
using static OpenIddict.Abstractions.OpenIddictConstants;
using Microsoft.AspNetCore.Identity;

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
                    "eshop.api"
                }.Intersect(request.GetScopes()));

                var principal = new ClaimsPrincipal(identity);

                // 4. 返回 Token
                return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            throw new NotImplementedException("这个示例只实现了密码模式");
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