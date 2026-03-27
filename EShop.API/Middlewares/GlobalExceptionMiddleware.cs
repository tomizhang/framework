using System.Diagnostics;
using System.Net;
using System.Text.Json;

namespace EShop.API.Middlewares
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;

        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                // 🟢 1. 放行：让请求继续往下走 (去 Controller, Service...)
                await _next(context);
            }
            catch (Exception ex)
            {
                // 🔴 2. 捕获：如果下面任何地方报错了，这里都能抓到
                _logger.LogError(ex, "全局异常捕获: {Message}", ex.Message);

                // 3. 处理：返回一个漂亮的 JSON 给前端，而不是 500 网页
                await HandleExceptionAsync(context, ex);
            }
        }

        private static Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";

            // 根据异常类型决定状态码 (这里简单统一为 500)
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            var traceId = Activity.Current?.Id ?? context.TraceIdentifier;

            // 👇 极其核心：强行对齐大厂的统一返回契约 (约定格式)
            var response = new
            {
                code = context.Response.StatusCode, // 之前是 StatusCode -> 统一为 code
                message = "服务器开小差了，请联系管理员", // 之前是 Message -> 统一为 message
                data = (object)null, // 之前没有 data -> 极其严谨地补上 data: null，保持格式绝对一致！
                traceId = traceId, // 之前是大写 -> 统一为小写 traceId
                detailedError = exception.Message // 保留开发调试用的详细错误
            };

            // 注意：因为这里的序列化没有走 ASP.NET Core MVC 的默认配置，所以我们直接用小写属性名来生成 JSON
            var json = JsonSerializer.Serialize(response);
            return context.Response.WriteAsync(json);
        }
    }
}