using Duende.IdentityServer.Models;

namespace EShop.Identity
{
    public static class Config
    {
        // 1. 定义受保护的资源 (Scopes)
        public static IEnumerable<ApiScope> ApiScopes =>
            new List<ApiScope>
            {
                new ApiScope("eshop.api", "EShop API 访问权限")
            };

        // 2. 定义身份资源 (OpenID Connect 标准)
        public static IEnumerable<IdentityResource> IdentityResources =>
            new List<IdentityResource>
            {
                new IdentityResources.OpenId(),
                new IdentityResources.Profile()
            };

        // 3. 定义客户端 (Clients)
        public static IEnumerable<Client> Clients =>
            new List<Client>
            {
                // 定义一个给 Postman 或 Swagger 用的客户端
                new Client
                {
                    ClientId = "postman_client",
                    
                    // 客户端密钥 (需要保密，类似 AppSecret)
                    ClientSecrets = { new Secret("secret".Sha256()) },

                    // 允许的授权类型：ResourceOwnerPassword (密码模式)
                    // 意思就是：用户直接把账号密码给客户端，客户端拿去找我们换 Token
                    // (虽然现在推荐 AuthorizationCode，但为了方便 Swagger 测试，我们先用密码模式)
                    AllowedGrantTypes = GrantTypes.ResourceOwnerPassword,

                    // 允许这个客户端访问哪些 Scope
                    AllowedScopes = { "eshop.api", "openid", "profile" }
                }
            };
    }
}