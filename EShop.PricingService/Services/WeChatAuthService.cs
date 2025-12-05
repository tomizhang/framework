using Microsoft.Extensions.Configuration;

namespace EShop.Infrastructure.Services
{
    public class WeChatAuthService
    {
        private readonly IConfiguration _config;
        private readonly HttpClient _httpClient; // 用来发 HTTP 请求

        public WeChatAuthService(IConfiguration config, HttpClient httpClient)
        {
            _config = config;
            _httpClient = httpClient;
        }

        // 核心方法：拿 Code 换 OpenId
        public async Task<string> GetOpenIdByCodeAsync(string code)
        {
            // 1. 读取配置
            var appId = _config["WeChat:AppId"];
            var appSecret = _config["WeChat:AppSecret"];

            // 2. 模拟向微信发送请求
            // 真实地址是: https://api.weixin.qq.com/sns/jscode2session?appid={appId}&secret={appSecret}&js_code={code}...

            // 这里我们做个假的模拟：
            // 假设微信规定：如果 Code 是 "valid_code"，就返回 "open_id_888"
            if (code == "valid_code")
            {
                // 模拟网络延迟
                await Task.Delay(100);
                return "open_id_888"; // 假设这是微信告诉我们的 OpenID
            }

            throw new Exception("微信登录失败：无效的 Code");
        }
    }
}