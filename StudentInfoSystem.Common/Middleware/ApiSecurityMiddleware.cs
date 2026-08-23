using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace StudentInfoSystem.Common.Middleware
{
    public class ApiSecurityMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ApiSecurityMiddleware> _logger;
        private readonly IConfiguration _configuration;

        public ApiSecurityMiddleware(
            RequestDelegate next,
            ILogger<ApiSecurityMiddleware> logger,
            IConfiguration configuration)
        {
            _next = next;
            _logger = logger;
            _configuration = configuration;
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

            // 2. 如果配置了网关共享密钥，则校验请求头，防止直接访问下游服务时伪造来源
            var gatewaySecret = _configuration["Security:GatewaySecret"];
            if (!string.IsNullOrWhiteSpace(gatewaySecret))
            {
                if (!context.Request.Headers.TryGetValue("X-Gateway-Secret", out var secretValue) ||
                    secretValue != gatewaySecret)
                {
                    _logger.LogWarning("拒绝网关密钥不匹配的请求");
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsJsonAsync(new { message = "网关密钥无效" });
                    return;
                }
            }

            // 3. 获取并存储用户名，供后续使用
            if (context.Request.Headers.TryGetValue("X-User-Name", out var userNameValue))
            {
                context.Items["Username"] = userNameValue.ToString();
                _logger.LogDebug($"请求来自用户: {userNameValue}");
            }

            await _next(context);
        }
    }
}
