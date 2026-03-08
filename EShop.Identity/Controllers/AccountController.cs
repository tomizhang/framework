using EShop.Identity.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace EShop.Identity.Controllers
{
    [AllowAnonymous]
    public class AccountController : Controller
    {
        private readonly SignInManager<IdentityUser> _signInManager;

        public AccountController(SignInManager<IdentityUser> signInManager)
        {
            _signInManager = signInManager;
        }

        // 1. 展示登录页面 (当未登录时，系统会自动跳到这里 /Account/Login)
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
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
    }
}