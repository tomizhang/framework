using Castle.Core.Logging;
using EShop.Application.Auth;
using EShop.Application.Auth.Dtos;
using EShop.Application.Common.Interfaces;
using EShop.Domain.Entities;
using EShop.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq; // 👈 引入 Mock 神器
using Xunit;

namespace EShop.UnitTests.Application
{
    public class AuthServiceTests
    {
        // 定义我们要用到的 Mock 对象
        private readonly Mock<IPasswordHasher> _mockHasher;
        private readonly Mock<ITokenService> _mockTokenService;
        private readonly Mock<ILogger<AuthService>> _logger;
        private readonly EShopDbContext _dbContext; // 这里用内存数据库，不用 Mock
        private readonly AuthService _authService; // 被测试的主角

        public AuthServiceTests()
        {
            // 1. 初始化 Mock 对象
            _mockHasher = new Mock<IPasswordHasher>();
            _mockTokenService = new Mock<ITokenService>();
            _logger = new Mock<ILogger<AuthService>>();

            // 2. 初始化内存数据库 (InMemory DB)
            // 给每个测试用唯一的数据库名，防止数据冲突
            var options = new DbContextOptionsBuilder<EShopDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _dbContext = new EShopDbContext(options);

            // 3. 组装 AuthService
            // 把假的 Hasher、假的 TokenService、内存 DB 塞进去
            _authService = new AuthService(_logger.Object,_dbContext, _mockHasher.Object, _mockTokenService.Object);
        }

        [Fact]
        public async Task RegisterAsync_Should_Success_When_User_Not_Exists()
        {
            // 1. Arrange (准备)
            var input = new RegisterDto
            {
                Username = "newuser",
                Password = "password123",
                Email = "test@test.com"
            };

            // 设置 Mock 行为：
            // 当有人调用 HashPassword("password123") 时，请返回 "hashed_secret_123"
            _mockHasher.Setup(x => x.HashPassword(input.Password))
                       .Returns("hashed_secret_123");

            // 当有人调用 GenerateToken 时，请返回 "fake_jwt_token"
            _mockTokenService.Setup(x => x.GenerateToken(It.IsAny<long>(), input.Username))
                             .Returns("fake_jwt_token");

            // 2. Act (执行)
            var result = await _authService.RegisterAsync(input);

            // 3. Assert (断言)
            // 检查返回值
            Assert.NotNull(result);
            Assert.Equal("fake_jwt_token", result.Token);
            Assert.Equal("newuser", result.Username);

            // 检查数据库里是不是真的有一条数据
            var dbUser = await _dbContext.Users.FirstAsync();
            Assert.Equal("newuser", dbUser.Username);
            Assert.Equal("hashed_secret_123", dbUser.PasswordHash); // 检查是否用了我们Mock的加密结果
        }

        [Fact]
        public async Task RegisterAsync_Should_Throw_Exception_When_Username_Exists()
        {
            // 1. Arrange
            // 先往内存库里塞一个老用户
            _dbContext.Users.Add(new User("olduser", "hash", "email"));
            await _dbContext.SaveChangesAsync();

            var input = new RegisterDto { Username = "olduser", Password = "123" };

            // 2. Act & Assert
            // 期望抛出 Exception，且消息包含 "用户名已被使用"
            var ex = await Assert.ThrowsAsync<Exception>(() => _authService.RegisterAsync(input));
            Assert.Equal("用户名已被使用", ex.Message);
        }
    }
}