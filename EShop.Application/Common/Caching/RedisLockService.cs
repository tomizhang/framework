using EShop.Application.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace EShop.Application.Common.Caching
{
    public class RedisLockService : IRedisLockService
    {
        private readonly IConnectionMultiplexer _redis;

        public RedisLockService(IConnectionMultiplexer redis)
        {
            _redis = redis;
        }

        public async Task<bool> AcquireLockAsync(string key, TimeSpan expiry)
        {
            var db = _redis.GetDatabase();
            // StringSetAsync 对应 Redis 的 SET 命令
            // When.NotExists 对应 NX 参数 (只有 key 不存在时才设置成功)
            // 这样就保证了只有一个线程能设置成功
            return await db.StringSetAsync(key, "locked", expiry, When.NotExists);
        }

        public async Task ReleaseLockAsync(string key)
        {
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync(key);
        }
    }
}
