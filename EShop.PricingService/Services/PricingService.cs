using Grpc.Core;
using EShop.PricingService.Protos; // 引用生成的命名空间

namespace EShop.PricingService.Services
{
    // 继承自生成出来的 Pricing.PricingBase
    public class PricingService : Pricing.PricingBase
    {
        private readonly ILogger<PricingService> _logger;

        public PricingService(ILogger<PricingService> logger)
        {
            _logger = logger;
        }

        public override Task<PriceReply> CalculatePrice(PriceRequest request, ServerCallContext context)
        {
            _logger.LogInformation($"正在计算商品 {request.ProductId} 的价格...");

            // 模拟复杂的计价逻辑
            double finalPrice = request.OriginalPrice * request.Quantity;

            string msg = "原价";

            // 如果有折扣码
            if (request.DiscountCode == "VIP888")
            {
                finalPrice *= 0.8; // 打8折
                msg = "VIP折扣生效";
            }

            // 返回结果
            return Task.FromResult(new PriceReply
            {
                FinalPrice = finalPrice,
                Message = msg
            });
        }
    }
}