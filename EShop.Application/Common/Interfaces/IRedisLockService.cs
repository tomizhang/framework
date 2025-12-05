using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EShop.Application.Common.Interfaces
{
    public interface IRedisLockService
    {
        // 尝试获取锁
        // key: 锁的名字 (比如 lock:product:1)
        // expiry: 锁多久自动过期 (防止死锁)
        // 返回: true=拿到锁了, false=没拿到
        Task<bool> AcquireLockAsync(string key, TimeSpan expiry);

        // 释放锁
        Task ReleaseLockAsync(string key);
    }
}
