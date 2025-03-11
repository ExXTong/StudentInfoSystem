using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace StudentInfoSystem.Common.Middleware
{
    public class ApiSecurityMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ApiSecurityMiddleware> _logger;

        public ApiSecurityMiddleware(RequestDelegate next, ILogger<ApiSecurityMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // 1. 验证请求是否来自网关
            if (!context.Request.Headers.TryGetValue("X-Gateway-Source", out var sourceValue) ||
                sourceValue != "StudentInfoGateway")
            {
                _logger.LogWarning("拒绝非网关来源的请求");
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { message = "只接受来自授权网关的请求" });
                return;
            }

            // 2. 获取并存储用户名，供后续使用
            if (context.Request.Headers.TryGetValue("X-User-Name", out var userNameValue))
            {
                context.Items["Username"] = userNameValue.ToString();
                _logger.LogDebug($"请求来自用户: {userNameValue}");
            }
            
            await _next(context);
        }
    }
}