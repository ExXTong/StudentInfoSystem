using Microsoft.Playwright;
using StudentInfoSystem.Common.Models;
using StudentInfoSystem.Common.Services;
using System;
using System.Threading.Tasks;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using System.Collections.Generic;

namespace StudentInfoSystem.AuthService.Services
{
    public class LoginService
    {
        private readonly IBrowserManager _browserManager;
        private readonly string _jwtSecret;
        private readonly string _issuer;
        private readonly string _audience;

        public LoginService(IBrowserManager browserManager, string jwtSecret, string issuer, string audience)
        {
            _browserManager = browserManager;
            _jwtSecret = jwtSecret;
            _issuer = issuer;
            _audience = audience;
        }

        public async Task<LoginResponse> LoginAsync(LoginRequest request)
        {
            IPage page = null;
            try
            {
                // 从池中获取页面
                page = await _browserManager.GetPageAsync();
                Console.WriteLine($"为用户 {request.Username} 获取到新页面实例");
                
                // 使用页面进行登录
                bool loginSuccess = await _browserManager.LoginAsync(request.Username, request.Password, page);
                
                if (loginSuccess)
                {
                    // 创建用户信息
                    var userInfo = new UserInfo
                    {
                        UserId = request.Username,
                        Username = request.Username,
                        Role = "Student", // 根据实际情况设置角色
                        Permissions = new List<string> { "view_grades", "view_schedule", "view_info" }
                    };

                    // 生成JWT令牌
                    var token = GenerateJwtToken(userInfo);
                    var refreshToken = GenerateRefreshToken();

                    // 已获取所需信息，立即执行注销操作
                    Console.WriteLine($"用户 {request.Username} 已成功获取登录信息，执行自动注销...");
                    try
                    {
                        await _browserManager.LogoutAsync(page);
                        Console.WriteLine($"用户 {request.Username} 自动注销成功");
                    }
                    catch (Exception ex)
                    {
                        // 注销失败只记录日志，不影响登录成功的返回
                        Console.WriteLine($"自动注销过程中出现异常: {ex.Message}");
                    }

                    return new LoginResponse
                    {
                        Success = true,
                        Token = token,
                        RefreshToken = refreshToken,
                        Expiration = DateTime.UtcNow.AddHours(1),
                        User = userInfo
                    };
                }
                
                return new LoginResponse
                {
                    Success = false,
                    ErrorMessage = "登录失败"
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"登录过程中出现异常: {ex.Message}");
                return new LoginResponse
                {
                    Success = false,
                    ErrorMessage = $"登录过程中出现错误: {ex.Message}"
                };
            }
            finally
            {
                // 释放页面实例回池中
                if (page != null)
                {
                    await _browserManager.ReleaseBrowserAsync(page);
                    Console.WriteLine($"已释放用户 {request.Username} 的页面实例");
                }
            }
        }

        private string GenerateJwtToken(UserInfo user)
        {
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.UserId),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSecret));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _issuer,
                audience: _audience,
                claims: claims,
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private string GenerateRefreshToken()
        {
            return Guid.NewGuid().ToString();
        }

        public async Task<TokenValidationResponse> ValidateTokenAsync(string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSecret));

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = key,
                    ValidateIssuer = true,
                    ValidIssuer = _issuer,
                    ValidateAudience = true,
                    ValidAudience = _audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                var principal = tokenHandler.ValidateToken(token, validationParameters, out SecurityToken validatedToken);

                var userInfo = new UserInfo
                {
                    UserId = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value,
                    Username = principal.FindFirst(JwtRegisteredClaimNames.UniqueName)?.Value,
                    Role = principal.FindFirst(ClaimTypes.Role)?.Value,
                    Permissions = new List<string>() // 从claims中获取权限
                };

                return new TokenValidationResponse
                {
                    IsValid = true,
                    User = userInfo
                };
            }
            catch (Exception ex)
            {
                return new TokenValidationResponse
                {
                    IsValid = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// 执行注销操作
        /// </summary>
        /// <returns>是否成功注销</returns>
        public async Task<bool> LogoutAsync()
        {
            IPage page = null;
            try
            {
                // 从池中获取页面
                page = await _browserManager.GetPageAsync();
                Console.WriteLine("获取到新页面实例用于注销操作");
                
                // 使用页面进行注销
                bool logoutSuccess = await _browserManager.LogoutAsync(page);
                
                Console.WriteLine($"注销操作结果: {(logoutSuccess ? "成功" : "失败")}");
                return logoutSuccess;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"注销过程中出现异常: {ex.Message}");
                return false;
            }
            finally
            {
                // 释放页面实例回池中
                if (page != null)
                {
                    await _browserManager.ReleaseBrowserAsync(page);
                    Console.WriteLine("已释放用于注销的页面实例");
                }
            }
        }
    }
}