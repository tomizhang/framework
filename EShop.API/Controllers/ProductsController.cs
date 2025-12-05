using EShop.Application.Orders.Dtos;
using EShop.Application.Products;
using EShop.Application.Products.Dtos;
using EShop.PricingService.Protos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static EShop.PricingService.Protos.Pricing;

namespace EShop.API.Controllers
{
    [ApiController] // 启用 API 行为（如自动 400 错误验证）
    [Route("api/[controller]")] // 路由地址：api/products
    public class ProductsController : ControllerBase
    {
        private readonly IProductAppService _productAppService;
        private readonly PricingClient _pricingClient;

        // 构造函数注入：我们要用 Application 层的服务
        public ProductsController(IProductAppService productAppService, PricingClient pricingClient)
        {
            _productAppService = productAppService;
            _pricingClient = pricingClient;
        }

        // 1. 创建商品接口
        // POST: api/products
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CreateAsync([FromBody] CreateProductDto input)
        {
            // 直接调用 Service 层逻辑
            var result = await _productAppService.CreateAsync(input);

            // 返回 201 Created 状态码，并附带新创建的数据
            // ✅ 改成这一行 (直接返回 200 OK 和数据)
            return Ok(result);
        }

        // 以后我们要在这里加 [HttpGet] 用来查商品
        [HttpGet("{id}")]
        [Authorize]
        public async Task<IActionResult> GetAsync(long id)
        {
            var result = await _productAppService.GetAsync(id);
            return Ok(result);
        }

        // POST: api/products/1/buy
        [HttpPost("{id}/buy")]
        public async Task<IActionResult> BuyAsync(long id, [FromBody] int quantity)
        {
            try
            {
                // 调用我们刚才写的带分布式锁的方法
                bool success = await _productAppService.ReduceStockAsync(id, quantity);

                if (success)
                {
                    return Ok(new { message = "购买成功！" });
                }
                else
                {
                    // 理论上 ReduceStockAsync 里抛异常了不会走到这里，
                    // 但为了代码健壮性，我们可以返回 400
                    return BadRequest(new { message = "购买失败" });
                }
            }
            catch (Exception ex)
            {
                // 捕获 "系统繁忙" 或 "库存不足" 等异常，返回 400 给前端
                return BadRequest(new { message = ex.Message });
            }
        }

        // POST: api/products/1/buy
        [HttpPost("{userId}/place-order")] // 路由名字稍微改优雅一点
        public async Task<IActionResult> PlaceOrderAsync(long userId, [FromBody] CreateOrderDto input)
        {
            // input 里面包含了 ProductId 和 Quantity
            await _productAppService.PlaceOrderAsync(input.ProductId, input.Quantity, userId);

            // 返回 202 Accepted，表示"请求已接收，正在排队处理中"
            // 这是异步接口最标准的返回码
            return Accepted(new { message = "订单已提交，正在排队处理中..." });
        }

        [HttpPost("calculate-price")]
        public async Task<IActionResult> CalculatePrice([FromBody] CreateOrderDto input)
        {
            // 构造 gRPC 请求
            var grpcRequest = new PriceRequest
            {
                ProductId = input.ProductId,
                Quantity = input.Quantity,
                OriginalPrice = 100, // 假设从数据库查出来的单价是100
                DiscountCode = "VIP888" // 假设用户填了优惠码
            };

            // 像调本地方法一样发起远程调用！
            var reply = await _pricingClient.CalculatePriceAsync(grpcRequest);

            return Ok(new
            {
                finalPrice = reply.FinalPrice,
                note = reply.Message
            });
        }
    }
}