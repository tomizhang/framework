using EShop.API.Filters;
using EShop.Application.Auth;
using EShop.Application.Auth.Dtos;
using EShop.Infrastructure.Data;
using EShop.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EShop.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly EShopDbContext _context;
        private readonly WeChatAuthService _weChatAuthService;

        public AuthController(EShopDbContext context, IAuthService authService, WeChatAuthService weChatAuthService)
        {
            _authService = authService;
            _context = context;
            _weChatAuthService = weChatAuthService;
        }

        [HttpPost("register")]
        [TypeFilter(typeof(LogTimeFilter))]
        public async Task<IActionResult> Register([FromBody] RegisterDto input)
        {
            try
            {
                Thread.Sleep(1000);
                var result = await _authService.RegisterAsync(input);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto input)
        {
            try
            {
                var result = await _authService.LoginAsync(input);
                return Ok(result);
            }
            catch (Exception ex)
            {
                // 登录失败通常返回 401 Unauthorized 或 400 BadRequest
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("external-login")]
        [TypeFilter(typeof(LogTimeFilter))]
        public async Task<IActionResult> ExternalLogin([FromBody] ExternalLoginDto input)
        {
            try
            {
                // 这里模拟：假设前端已经找微信拿到 OpenId 了
                var result = await _authService.LoginByOpenIdAsync(input);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        public async Task<AuthResponseDto> LoginByCodeAsync(ExternalLoginDto input)
        {
            string openId;

            // 1. 根据 Provider 判断找谁换 OpenID
            if (input.Provider == "WeChat")
            {
                // 调用刚才写的服务，把 Code 换成 OpenID
                // 这就是 AppID 和 AppSecret 发挥作用的地方！
                openId = await _weChatAuthService.GetOpenIdByCodeAsync(input.Code);
            }
            else
            {
                throw new Exception("不支持的第三方登录");
            }

            // 2. 拿到 OpenID 后，剩下的逻辑和之前一模一样（查库、注册、发 Token）
            var user = await _context.Users.FirstOrDefaultAsync(u => u.OpenId == openId);
            var result = await _authService.RegisterAsync(new RegisterDto()
            {
                Username = user.Username,
            });
            return result;
        }
    }
}