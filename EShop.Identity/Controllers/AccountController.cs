using EShop.Identity.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EShop.Identity.Controllers
{
    [AllowAnonymous]
    public class AccountController : Controller
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UserManager<IdentityUser> _userManager;
        public AccountController(SignInManager<IdentityUser> signInManager, UserManager<IdentityUser> userManager)
        {
            _signInManager = signInManager;
            _userManager = userManager;
        }

        // 1. 展示登录页面 (当未登录时，系统会自动跳到这里 /Account/Login)
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            //ViewData["ReturnUrl"] = returnUrl;
            //return View();
            // 👇 2. 极其核心：把它塞进你的 ViewModel 里！
            var model = new LoginViewModel
            {
                ReturnUrl = returnUrl
            };

            return View(model);
        }

        // 2. 接收用户提交的账号密码
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            ViewData["ReturnUrl"] = model.ReturnUrl;

            if (ModelState.IsValid)
            {
                // 调用系统自带的密码验证机制 (注意我们在 TestDataWorker 里建的密码是 Password123!)
                var result = await _signInManager.PasswordSignInAsync(model.Username, model.Password, isPersistent: false, lockoutOnFailure: false);

                if (result.Succeeded)
                {
                    // 登录成功！如果有 ReturnUrl，就带着凭证跳回原系统
                    if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                    {
                        return Redirect(model.ReturnUrl);
                    }
                    // 如果没有，就留在默认首页
                    return Content("登录成功！但没有 ReturnUrl。");
                }

                ModelState.AddModelError(string.Empty, "账号或密码错误。");
            }

            // 登录失败，重新显示页面并带上错误提示
            return View(model);
        }

        // 👇 1. 用户点击 GitHub 登录按钮时，触发这个动作，把人踢到 GitHub 去
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public IActionResult ExternalLogin(string provider, string? returnUrl = null)
        {
            // 配置 GitHub 授权成功后跳回来的地址 (跳回我们下面写的 ExternalLoginCallback)
            var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Account", new { returnUrl });

            // 呼叫底层的认证中间件，帮我们构造跳往 GitHub 的链接
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            return Challenge(properties, provider);
        }

        // 👇 2. 用户在 GitHub 点了同意后，会带着凭证跳回这个动作
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null, string? remoteError = null)
        {
            if (remoteError != null)
            {
                ModelState.AddModelError(string.Empty, $"来自外部提供商的错误: {remoteError}");
                return View("Login", new LoginViewModel { ReturnUrl = returnUrl });
            }

            // 1. 从中间件的临时 Cookie 里，把 GitHub 传回来的用户信息捞出来
            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null) return RedirectToAction(nameof(Login));

            // 2. 检查我们本地数据库的 AspNetUserLogins 表，看看这个 GitHub 账号以前绑定过没有？
            // 如果绑定过，直接丝滑登录！
            var result = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);

            if (result.Succeeded)
            {
                return RedirectToLocal(returnUrl); // 登录成功，跳回前面的商城页面
            }
            else
            {
                // 3. 核心逻辑：如果没绑定过 (新用户第一次用 GitHub 登录)，我们就暗中帮他建个本地账号！

                // 从 GitHub 传回来的数据里翻找邮箱和名字
                var email = info.Principal.FindFirstValue(ClaimTypes.Email) ?? $"{info.ProviderKey}@github.local";
                var userName = info.Principal.FindFirstValue(ClaimTypes.Name) ?? email;

                var user = new IdentityUser { UserName = userName, Email = email };
                var createResult = await _userManager.CreateAsync(user); // 在本地 AspNetUsers 表新建一条记录

                if (createResult.Succeeded)
                {
                    // 把本地新建的账号，和刚才这个 GitHub 身份绑定起来写入数据库
                    createResult = await _userManager.AddLoginAsync(user, info);
                    if (createResult.Succeeded)
                    {
                        // 绑定成功，签发本地登录的 Cookie！
                        await _signInManager.SignInAsync(user, isPersistent: false);
                        return RedirectToLocal(returnUrl);
                    }
                }
                return Content("注册第三方账号失败！");
            }
        }

        // 辅助方法：处理安全跳转
        private IActionResult RedirectToLocal(string? returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);
            return Content("登录成功！但没有 ReturnUrl。");
        }

        // 接收前端发来的登出请求
        [HttpGet("~/connect/logout")]
        [HttpPost("~/connect/logout")]
        public IActionResult Logout()
        {
            // 核心魔法：返回 SignOut 结果。
            // 1. 清除 Identity 的本地应用 Cookie (让你在 5001 端口真正退出)
            // 2. 触发 OpenIddict 的登出机制，它会自动读取网址里的 post_logout_redirect_uri 并帮你 302 跳回前端！
            return SignOut(
                authenticationSchemes: new[]
                {
                    Microsoft.AspNetCore.Identity.IdentityConstants.ApplicationScheme,
                    OpenIddict.Server.AspNetCore.OpenIddictServerAspNetCoreDefaults.AuthenticationScheme
                });
        }
    }
}