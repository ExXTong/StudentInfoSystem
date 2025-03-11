using Microsoft.AspNetCore.Builder;

namespace StudentInfoSystem.Common.Middleware
{
    public static class MiddlewareExtensions
    {
        public static IApplicationBuilder UseApiSecurity(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ApiSecurityMiddleware>();
        }
    }
}