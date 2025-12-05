using EShop.Domain.Common;

namespace EShop.Domain.Entities
{
    public class User : FullAuditedEntity<long>
    {
        public string Username { get; private set; }

        // ⚠️ 存的是加密后的哈希值，不是 "123456"
        public string PasswordHash { get; private set; }

        public string Email { get; private set; }
        // 新增：第三方登录的唯一标识 (可以为空，因为有的用户只用账号密码登录)
        public string? OpenId { get; private set; }

        // 来源 (比如 "WeChat", "Google")
        public string? Provider { get; private set; }

        public void BindOpenId(string openId, string provider)
        {
            OpenId = openId;
            Provider = provider;
        }
        private User() { }

        public User(string username, string passwordHash, string email)
        {
            Username = username;
            PasswordHash = passwordHash;
            Email = email;
        }
    }
}