using System;
using Microsoft.Extensions.Configuration;

namespace StudentInfoSystem.Common.Security
{
    /// <summary>
    /// JWT 配置辅助类：优先从环境变量读取密钥，避免把生产密钥提交到仓库。
    /// </summary>
    public static class JwtConfiguration
    {
        // 仅供本地开发使用的密钥，绝不能用于生产环境。
        public const string DevelopmentOnlyKey = "DevOnly_DoNotUseInProduction_0123456789ABCDEF";

        public static string GetSigningKey(IConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            // 支持常见的环境变量命名：JWT__KEY、JWT_KEY、Jwt__Key。
            var envKey = Environment.GetEnvironmentVariable("JWT__KEY")
                         ?? Environment.GetEnvironmentVariable("JWT_KEY")
                         ?? Environment.GetEnvironmentVariable("Jwt__Key");

            if (!string.IsNullOrWhiteSpace(envKey))
            {
                return envKey;
            }

            var configKey = configuration["Jwt:Key"];
            if (!string.IsNullOrWhiteSpace(configKey) &&
                !configKey.StartsWith("YourSuperSecretKey", StringComparison.Ordinal))
            {
                return configKey;
            }

            if (IsDevelopment(configuration))
            {
                return DevelopmentOnlyKey;
            }

            throw new InvalidOperationException(
                "JWT signing key is not configured. Set JWT__KEY or Jwt:Key in production.");
        }

        public static bool IsDevelopment(IConfiguration configuration)
        {
            var env = configuration["ASPNETCORE_ENVIRONMENT"] ?? "Production";
            return string.Equals(env, "Development", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(env, "Staging", StringComparison.OrdinalIgnoreCase);
        }
    }
}
