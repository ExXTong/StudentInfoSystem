using StudentInfoSystem.Common.Models;
using System;

namespace StudentInfoSystem.AuthService.Services
{
    public class LoginResponse
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public string Token { get; set; }
        public string RefreshToken { get; set; }
        public DateTime Expiration { get; set; }
        public UserInfo User { get; set; }
    }

    public class TokenValidationResponse
    {
        public bool IsValid { get; set; }
        public UserInfo User { get; set; }
        public string ErrorMessage { get; set; }
    }
}