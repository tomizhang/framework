using AutoMapper;
using EShop.Application.Common.Interfaces;
using EShop.Application.Orders.Dtos;
using EShop.Application.Products.Dtos;
using EShop.Domain.Entities;
using EShop.Infrastructure.Data;
using Microsoft.EntityFrameworkCore; // 为了使用 AddAsync, SaveChangesAsync

namespace EShop.Application.Products
{
    public class ProductAppService : IProductAppService
    {
        private readonly EShopDbContext _dbContext;
        private readonly IMapper _mapper;

        // 构造函数注入：系统会自动把 DbContext 和 Mapper 给我们要
        private readonly ICacheService _cacheService; // 新增字段
        private readonly IRedisLockService _lockService;
        private readonly IMessageProducer _messageProducer;
        // 修改构造函数，注入 cacheService
        public ProductAppService(
            EShopDbContext dbContext,
            IMapper mapper,
            ICacheService cacheService,
            IRedisLockService lockService,
            IMessageProducer messageProducer
            ) // 新增参数
        {
            _dbContext = dbContext;
            _mapper = mapper;
            _cacheService = cacheService;
            _lockService = lockService;
            _messageProducer = messageProducer;
        }

        public async Task<ProductDto> CreateAsync(CreateProductDto input)
        {
            // 1. 检查商品名是否重复 (业务逻辑)
            // AnyAsync 是 EF Core 的异步方法，检查是否存在
            var exists = await _dbContext.Products.AnyAsync(p => p.Name == input.Name);
            if (exists)
            {
                throw new Exception($"商品名称 '{input.Name}' 已存在");
            }

            // 2. 将 DTO 转换为 实体 (Entity)
            // 注意：因为我们在 Product 实体里用了 private set 和有参构造函数，
            // AutoMapper 默认可能搞不定，或者我们需要手动调用构造函数。
            // 为了演示"充血模型"的威力，我们这里手动实例化：

            var product = new Product(input.Name, input.Price, input.Stock, input.Description);

            // 3. 存入数据库
            await _dbContext.Products.AddAsync(product);
            await _dbContext.SaveChangesAsync(); // 真正执行 SQL 插入

            // 4. 将保存后的实体（此时已有 ID）转换回 DTO 返回给前端
            return _mapper.Map<ProductDto>(product);
        }

        public async Task<ProductDto> GetAsync(long id)
        {
            // 1. 定义缓存 Key (例如: product:10)
            string cacheKey = $"product:{id}";

            // 2. 尝试从缓存获取
            var cachedProduct = await _cacheService.GetAsync<ProductDto>(cacheKey);
            if (cachedProduct != null)
            {
                // 🎯 命中缓存！直接返回，不查数据库
                return cachedProduct;
            }

            // 3. 缓存没命中，去查数据库
            var product = await _dbContext.Products.FindAsync(id);
            if (product == null)
            {
                throw new Exception("商品不存在"); // 这里以后可以用自定义异常
            }

            // 4. 转换对象
            var productDto = _mapper.Map<ProductDto>(product);

            // 5. 写入缓存 (设置过期时间 30 分钟)
            // 这样下一个人来查，就直接走第 2 步了
            await _cacheService.SetAsync(cacheKey, productDto, TimeSpan.FromMinutes(30));

            return productDto;
        }

        // 2. 实现扣减库存（带锁版）
        public async Task<bool> ReduceStockAsync(long id, int quantity)
        {
            string lockKey = $"lock:product:{id}";

            // A. 尝试抢锁 (等待 0 秒，抢不到立刻返回 false，也可以做成自旋等待)
            // 这里设置锁过期 5 秒，防止程序崩了死锁
            bool isLocked = await _lockService.AcquireLockAsync(lockKey, TimeSpan.FromSeconds(5));

            if (!isLocked)
            {
                // 抢锁失败，说明有人正在操作这个商品
                throw new Exception("系统繁忙，请稍后再试");
            }

            try
            {
                // B. 抢到锁了！开始核心业务
                // 1. 查数据库 (或者查 Redis 缓存)
                var product = await _dbContext.Products.FindAsync(id);
                if (product == null) throw new Exception("商品不存在");

                // 2. 执行领域内的扣减逻辑 (充血模型发挥作用！)
                product.ReduceStock(quantity); // 如果库存不足，这里会抛异常

                // 3. 保存到数据库
                await _dbContext.SaveChangesAsync();

                // 4. (可选) 更新缓存 / 删除缓存让下次读取重建
                await _cacheService.RemoveAsync($"product:{id}");

                return true;
            }
            finally
            {
                // C. 无论成功失败，一定要释放锁！！！
                await _lockService.ReleaseLockAsync(lockKey);
            }
        }

        public async Task PlaceOrderAsync(long productId, int quantity, long userId)
        {
            // 1. 这里只做最简单的预检查 (比如检查 Redis 里的库存缓存)
            // 真正的扣库存逻辑后移到消费者里去，或者这里只做"预扣减"

            // 2. 组装消息
            var orderEvent = new OrderCreatedEvent
            {
                ProductId = productId,
                Quantity = quantity,
                UserId = userId,
                OrderTime = DateTime.UtcNow
            };

            // 3. 发送消息到 MQ (极快，几毫秒就完成)
            _messageProducer.SendMessage(orderEvent);

            // 4. 立刻返回！用户不需要等待数据库写入完成
            await Task.CompletedTask;
        }
    }
}