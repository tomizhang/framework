using EShop.Application.Common.Interfaces;
using BCrypt.Net;

namespace EShop.Infrastructure.Security
{
    public class PasswordHasher : IPasswordHasher
    {
        public string HashPassword(string password)
        {
            // BCrypt 会自动生成 Salt（盐），防止彩虹表攻击
            return BCrypt.Net.BCrypt.HashPassword(password);
        }

        public bool VerifyPassword(string password, string passwordHash)
        {
            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
        }
    }
}