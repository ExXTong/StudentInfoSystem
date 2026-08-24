using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System;
using StudentInfoSystem.AuthService.Services;
using StudentInfoSystem.Common.Models;  // 添加这个引用

namespace StudentInfoSystem.AuthService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly LoginService _loginService;

        public AuthController(LoginService loginService)
        {
            _loginService = loginService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)  // 使用Common.Models的LoginRequest
        {
            try
            {
                Console.WriteLine("------------------------------------");
                Console.WriteLine($"收到登录请求，用户名: {request.Username}");
                
                if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
                {
                    Console.WriteLine("登录请求验证失败: 用户名或密码为空");
                    return BadRequest(new { success = false, message = "用户名和密码不能为空" });
                }
                
                // 使用注入的LoginService处理登录
                var loginResponse = await _loginService.LoginAsync(request);

                if (loginResponse.Success)
                {
                    Console.WriteLine("登录成功，返回200 OK");
                    return Ok(new { 
                        success = true, 
                        message = "登录成功",
                        token = loginResponse.Token,
                        refreshToken = loginResponse.RefreshToken,
                        expiration = loginResponse.Expiration,
                        user = loginResponse.User
                    });
                }

                Console.WriteLine("登录失败，返回400 Bad Request");
                return BadRequest(new { 
                    success = false, 
                    message = loginResponse.ErrorMessage ?? "登录失败，可能是用户名或密码错误，或者目标网站结构发生变化" 
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"登录处理过程中发生未捕获异常: {ex.Message}");
                Console.WriteLine($"异常堆栈: {ex.StackTrace}");
                return StatusCode(500, new { 
                    success = false, 
                    message = $"服务器错误: {ex.Message}"
                });
            }
            finally
            {
                Console.WriteLine("登录请求处理完成");
                Console.WriteLine("------------------------------------");
            }
        }

        [HttpPost("logout")]
        public IActionResult Logout()
        {
            Console.WriteLine("收到注销请求");
            // JWT 无状态注销由前端丢弃 Token 即可，无需调用教务系统
            return Ok(new { success = true, message = "注销成功" });
        }

        [HttpGet("ping")]
        public IActionResult Ping()
        {
            Console.WriteLine("收到ping请求");
            return Ok("pong");
        }
    }
}