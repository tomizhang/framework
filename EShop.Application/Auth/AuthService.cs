using EShop.Application.Auth.Dtos;
using EShop.Application.Common.Interfaces;
using EShop.Domain.Entities;
using EShop.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EShop.Application.Auth
{
    public class AuthService : IAuthService
    {
        private readonly EShopDbContext _context;
        private readonly IPasswordHasher _passwordHasher;
        private readonly ITokenService _tokenService;
        private readonly ILogger<AuthService> _logger;
        public AuthService(ILogger<AuthService> logger, EShopDbContext context, IPasswordHasher passwordHasher, ITokenService tokenService)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _tokenService = tokenService;
            _logger = logger;
        }

        // 注册逻辑
        public async Task<AuthResponseDto> RegisterAsync(RegisterDto input)
        {
            // 1. 检查用户名是否已存在
            if (await _context.Users.AnyAsync(u => u.Username == input.Username))
            {
                throw new Exception("用户名已被使用");
            }

            // 2. 密码加密 (加盐哈希)
            var passwordHash = _passwordHasher.HashPassword(input.Password);

            // 3. 创建用户实体
            var user = new User(input.Username, passwordHash, input.Email);

            // 4. 存库
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // 5. 注册成功后直接生成 Token 返回，用户不用再登一次
            var token = _tokenService.GenerateToken(user.Id, user.Username);

            return new AuthResponseDto
            {
                Token = token,
                UserId = user.Id,
                Username = user.Username
            };
        }

        // 登录逻辑
        public async Task<AuthResponseDto> LoginAsync(LoginDto input)
        {
            // 1. 查用户
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == input.Username);
            if (user == null)
            {
                throw new Exception("用户名或密码错误"); // 为了安全，不要提示"用户名不存在"
            }

            // 2. 校验密码 (用 BCrypt 验证)
            if (!_passwordHasher.VerifyPassword(input.Password, user.PasswordHash))
            {
                throw new Exception("用户名或密码错误");
            }

            // 3. 生成 Token
            var token = _tokenService.GenerateToken(user.Id, user.Username);

            return new AuthResponseDto
            {
                Token = token,
                UserId = user.Id,
                Username = user.Username
            };
        }

        public async Task<AuthResponseDto> LoginByOpenIdAsync(ExternalLoginDto input)
        {
            #region 微信
#if !DEBUG && DEBUG
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
#endif
            #endregion

            // 1. 先查有没有这个 OpenId 的用户
            var user = await _context.Users.FirstOrDefaultAsync(u => u.OpenId == input.OpenId);

            if (user == null)
            {
                // 2. 如果没查到，说明是"新用户"，自动注册 (Auto Register)
                // 生成一个随机用户名
                var randomName = $"{input.Provider}_{Guid.NewGuid().ToString().Substring(0, 8)}";

                // 密码给个随机的因为他以后用 OpenID 登
                var randomPwdHash = _passwordHasher.HashPassword(Guid.NewGuid().ToString());

                user = new User(randomName, randomPwdHash, $"{randomName}@external.com");
                user.BindOpenId(input.OpenId, input.Provider);

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                // 记录日志 (Serilog 会捕捉到)
                _logger.LogInformation($"新用户通过 {input.Provider} 注册成功: {user.Username}");
            }

            // 3. 生成 Token (和普通登录一样)
            var token = _tokenService.GenerateToken(user.Id, user.Username);

            return new AuthResponseDto
            {
                Token = token,
                UserId = user.Id,
                Username = user.Username
            };
        }
    }
}