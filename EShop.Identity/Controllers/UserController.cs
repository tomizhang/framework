using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json; // 引入 JSON 解析

namespace EShop.Identity.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        // 👇 极其规范地注入 HttpClientFactory 和 Configuration
        public UserController(
            UserManager<IdentityUser> userManager,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration)
        {
            _userManager = userManager;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        public class RegisterRequest
        {
            public string Username { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
            public string Role { get; set; } = "User";
            // 👇 极其核心：接收前端传来的 Cloudflare 令牌
            public string TurnstileToken { get; set; } = string.Empty;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            // ==========================================
            // 🚨 极其冷酷的第一道防线：Turnstile 令牌校验
            // ==========================================
            if (string.IsNullOrEmpty(request.TurnstileToken))
                return BadRequest("极其危险：缺少人机验证令牌！");

            var isHuman = await VerifyTurnstileAsync(request.TurnstileToken);
            if (!isHuman)
                return StatusCode(403, "极其抱歉：Cloudflare 判定您为机器人或验证已失效！");

            // ==========================================
            // 🛡️ 验证通过，放行进入核心注册逻辑 (保持你原来的代码)
            // ==========================================
            var user = new IdentityUser { UserName = request.Username, Email = request.Username + "@eshop.com" };
            var result = await _userManager.CreateAsync(user, request.Password);

            if (!result.Succeeded) return BadRequest(result.Errors);
            await _userManager.AddToRoleAsync(user, request.Role);

            return Ok(new { Message = "极其完美：真人验证通过，用户创建成功！", UserId = user.Id });
        }

        // 👇 架构师的极其优雅的私有方法：向 Cloudflare 查验 Token 的真伪
        private async Task<bool> VerifyTurnstileAsync(string token)
        {
            // 从 appsettings.json 读取你的 Secret Key
            var secretKey = _configuration["Cloudflare:TurnstileSecret"];

            var client = _httpClientFactory.CreateClient();
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("secret", secretKey!),
                new KeyValuePair<string, string>("response", token)
            });

            // 极其果断地向 Cloudflare 发起查验请求
            var response = await client.PostAsync("https://challenges.cloudflare.com/turnstile/v0/siteverify", content);
            if (!response.IsSuccessStatusCode) return false;

            var jsonString = await response.Content.ReadAsStringAsync();
            using var jsonDoc = JsonDocument.Parse(jsonString);

            // 极其精准地提取 Cloudflare 返回的 success 字段
            return jsonDoc.RootElement.GetProperty("success").GetBoolean();
        }
    }
}