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

       
    }
}