using EShop.Application.Common.Interfaces;
using Microsoft.Extensions.Caching.Distributed; // 引用 IDistributedCache
using System.Text.Json; // 引用 System.Text.Json

namespace EShop.Infrastructure.Caching
{
    public class RedisCacheService : ICacheService
    {
        private readonly IDistributedCache _cache;

        public RedisCacheService(IDistributedCache cache)
        {
            _cache = cache;
        }

        public async Task<T?> GetAsync<T>(string key)
        {
            var jsonStr = await _cache.GetStringAsync(key);
            if (string.IsNullOrEmpty(jsonStr))
            {
                return default;
            }

            // 反序列化：字符串 -> 对象
            return JsonSerializer.Deserialize<T>(jsonStr);
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null)
        {
            var jsonStr = JsonSerializer.Serialize(value);

            var options = new DistributedCacheEntryOptions();
            // 如果没传过期时间，默认缓存 10 分钟
            options.AbsoluteExpirationRelativeToNow = expiration ?? TimeSpan.FromMinutes(10);

            // 序列化：对象 -> 字符串
            await _cache.SetStringAsync(key, jsonStr, options);
        }

        public async Task RemoveAsync(string key)
        {
            await _cache.RemoveAsync(key);
        }
    }
}