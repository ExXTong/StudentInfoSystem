using System;
using System.Collections.Generic;

namespace StudentInfoSystem.Common.Models
{
    public class LoginRequest
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }

    public class LoginResponse
    {
        public bool Success { get; set; }
        public string Token { get; set; }
        public string RefreshToken { get; set; }
        public string ErrorMessage { get; set; }
        public DateTime Expiration { get; set; }
        public UserInfo User { get; set; }
    }

    public class UserInfo
    {
        public string UserId { get; set; }
        public string Username { get; set; }
        public string Name { get; set; }
        public string Role { get; set; } // Student, Teacher, Admin
        public List<string> Permissions { get; set; }
    }

    public class TokenValidationRequest
    {
        public string Token { get; set; }
    }

    public class TokenValidationResponse
    {
        public bool IsValid { get; set; }
        public UserInfo User { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class RefreshTokenRequest
    {
        public string RefreshToken { get; set; }
    }
}