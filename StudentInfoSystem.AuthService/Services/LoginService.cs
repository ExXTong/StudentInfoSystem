using StudentInfoSystem.Common.Models;
using StudentInfoSystem.Portal;
using System;
using System.IO;
using System.Text.Json;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;

namespace StudentInfoSystem.AuthService.Services
{
    public class LoginService
    {
        private static readonly ConcurrentDictionary<string, StoredCredential> SavedCredentials = new(StringComparer.Ordinal);
        private static readonly string CredentialFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StudentInfoSystem",
            "credentials.json");

        static LoginService()
        {
            try
            {
                if (File.Exists(CredentialFile))
                {
                    var json = File.ReadAllText(CredentialFile);
                    var data = JsonSerializer.Deserialize<Dictionary<string, StoredCredential>>(json);
                    if (data != null)
                    {
                        foreach (var kv in data) SavedCredentials[kv.Key] = kv.Value;
                    }
                }
            }
            catch
            {
                // 忽略损坏的凭据文件
            }
        }
        private readonly IStudentPortalClient _portal;
        private readonly string _jwtSecret;
        private readonly string _issuer;
        private readonly string _audience;
        private readonly string? _adminPassword;

        public LoginService(IStudentPortalClient portal, string jwtSecret, string issuer, string audience, string? adminPassword = null)
        {
            _portal = portal;
            _jwtSecret = jwtSecret;
            _issuer = issuer;
            _audience = audience;
            _adminPassword = adminPassword;
        }

        public async Task<LoginResponse> LoginAsync(LoginRequest request)
        {
            try
            {
                if (string.Equals(request.Username, "root", StringComparison.OrdinalIgnoreCase))
                {
                    return LoginAsRoot(request.Password);
                }

                if (SavedCredentials.TryGetValue(request.Username, out var saved) &&
                    VerifyCredential(saved, request.Password))
                {
                    return CreateStudentLoginResponse(request.Username);
                }

                bool loginSuccess = await _portal.LoginAsync(request.Username, request.Password);

                if (!loginSuccess)
                {
                    return new LoginResponse
                    {
                        Success = false,
                        ErrorMessage = "登录失败"
                    };
                }

                SaveCredential(request.Username, request.Password);

                var userInfo = new UserInfo
                {
                    UserId = request.Username,
                    Username = request.Username,
                    Role = "Student",
                    Permissions = new List<string> { "view_grades", "view_schedule", "view_info" }
                };

                var token = GenerateJwtToken(userInfo);
                var refreshToken = GenerateRefreshToken();

                return new LoginResponse
                {
                    Success = true,
                    Token = token,
                    RefreshToken = refreshToken,
                    Expiration = DateTime.UtcNow.AddHours(1),
                    User = userInfo
                };
            }
            catch (Exception ex)
            {
                return new LoginResponse
                {
                    Success = false,
                    ErrorMessage = $"登录过程中出现错误: {ex.Message}"
                };
            }
            finally
            {
                await _portal.LogoutAsync();
            }
        }


        private LoginResponse LoginAsRoot(string password)
        {
            if (string.IsNullOrEmpty(_adminPassword) || !string.Equals(password, _adminPassword, StringComparison.Ordinal))
            {
                return new LoginResponse
                {
                    Success = false,
                    ErrorMessage = "管理员密码错误"
                };
            }

            var userInfo = new UserInfo
            {
                UserId = "root",
                Username = "root",
                Name = "Root Admin",
                Role = "Admin",
                Permissions = new List<string> { "admin" }
            };

            return new LoginResponse
            {
                Success = true,
                Token = GenerateJwtToken(userInfo),
                RefreshToken = GenerateRefreshToken(),
                Expiration = DateTime.UtcNow.AddHours(1),
                User = userInfo
            };
        }



        private static void SaveCredential(string username, string password)
        {
            var salt = RandomNumberGenerator.GetBytes(16);
            var hash = HashPassword(password, salt);
            SavedCredentials[username] = new StoredCredential
            {
                Username = username,
                PasswordHash = Convert.ToBase64String(hash),
                Salt = Convert.ToBase64String(salt)
            };

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(CredentialFile)!);
                var json = JsonSerializer.Serialize(SavedCredentials);
                File.WriteAllText(CredentialFile, json);
            }
            catch
            {
                // 写入失败时至少保留内存缓存
            }
        }

        private static bool VerifyCredential(StoredCredential credential, string password)
        {
            if (credential == null || string.IsNullOrEmpty(credential.PasswordHash) || string.IsNullOrEmpty(credential.Salt))
            {
                return false;
            }

            var salt = Convert.FromBase64String(credential.Salt);
            var hash = HashPassword(password, salt);
            return CryptographicOperations.FixedTimeEquals(hash, Convert.FromBase64String(credential.PasswordHash));
        }

        private static byte[] HashPassword(string password, byte[] salt)
        {
            return Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);
        }

        private class StoredCredential
        {
            public string Username { get; set; } = "";
            public string PasswordHash { get; set; } = "";
            public string Salt { get; set; } = "";
        }

        private LoginResponse CreateStudentLoginResponse(string username)
        {
            var userInfo = new UserInfo
            {
                UserId = username,
                Username = username,
                Role = "Student",
                Permissions = new List<string> { "view_grades", "view_schedule", "view_info" }
            };

            return new LoginResponse
            {
                Success = true,
                Token = GenerateJwtToken(userInfo),
                RefreshToken = GenerateRefreshToken(),
                Expiration = DateTime.UtcNow.AddHours(1),
                User = userInfo
            };
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

        public Task<TokenValidationResponse> ValidateTokenAsync(string token)
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
                    Permissions = new List<string>()
                };

                return Task.FromResult(new TokenValidationResponse
                {
                    IsValid = true,
                    User = userInfo
                });
            }
            catch (Exception ex)
            {
                return Task.FromResult(new TokenValidationResponse
                {
                    IsValid = false,
                    ErrorMessage = ex.Message
                });
            }
        }
    }
}
