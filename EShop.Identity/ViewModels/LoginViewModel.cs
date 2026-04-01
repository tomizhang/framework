using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace EShop.Identity.ViewModels
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "请输入用户名")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "请输入密码")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
        // 👇 极其聪明的映射：接住前端自动提交的 cf-turnstile-response
        [BindProperty(Name = "cf-turnstile-response")]
        public string? TurnstileToken { get; set; }

        // 极其重要：记录用户是从哪个业务系统跳过来的，登录完还要跳回去！
        public string? ReturnUrl { get; set; } // = "/connect/authorize";
    }
}