using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using StudentInfoSystem.Common.Models;
using StudentInfoSystem.StudentService.Services;
using System;
using System.Threading.Tasks;
using StudentInfoSystem.Common.Filters;

namespace StudentInfoSystem.StudentService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StudentController : ControllerBase
    {
        private readonly IStudentInfoService _studentInfoService;
        private readonly ILogger<StudentController> _logger;

        public StudentController(
            IStudentInfoService studentInfoService,
            ILogger<StudentController> logger)
        {
            _studentInfoService = studentInfoService;
            _logger = logger;
        }

        [HttpPost("login")]
        public async Task<IActionResult> GetStudentInfoByCredentials([FromBody] LoginModel login)
        {
            try
            {
                // 验证请求是否来自网关
                if (!Request.Headers.TryGetValue("X-Gateway-Source", out var sourceValue) || 
                    sourceValue != "StudentInfoGateway")
                {
                    _logger.LogWarning("拒绝非网关来源的请求");
                    return StatusCode(403, new { message = "只接受来自授权网关的请求" });
                }

                if (string.IsNullOrEmpty(login.Username) || string.IsNullOrEmpty(login.Password))
                {
                    return BadRequest(new { message = "用户名和密码不能为空" });
                }
                
                // 检查请求用户名与网关传递的用户名是否一致
                /*if (!Request.Headers.TryGetValue("X-User-Name", out var userNameValue))
                {
                    _logger.LogWarning("请求缺少用户名头");
                    return StatusCode(403, new { message = "未授权的请求" });
                }

                string gatewayUsername = userNameValue.ToString();
                if (gatewayUsername != login.Username)
                {
                    _logger.LogWarning($"用户名不匹配: JWT中为{gatewayUsername}，请求中为{login.Username}");
                    return StatusCode(403, new { message = "请求的用户名与授权的用户名不一致" });
                }*/
                
                var studentInfo = await _studentInfoService.GetStudentInfoByCredentialsAsync(login.Username, login.Password);
                if (studentInfo == null)
                {
                    return NotFound(new { message = "登录失败或未找到学生信息" });
                }

                return Ok(studentInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取学生信息时发生错误");
                return StatusCode(500, new { message = ex.Message });
            }
        }

        /// <summary>
        /// 通过爬虫模式获取学生信息 - 明确标注使用爬虫抓取
        /// </summary>
        [HttpPost("crawler")]
        public async Task<IActionResult> GetStudentInfoByCrawler([FromBody] LoginModel credentials)
        {
            try
            {
                // 验证请求是否来自网关
                if (!Request.Headers.TryGetValue("X-Gateway-Source", out var sourceValue) || 
                    sourceValue != "StudentInfoGateway")
                {
                    _logger.LogWarning("拒绝非网关来源的请求");
                    return StatusCode(403, new { message = "只接受来自授权网关的请求" });
                }

                if (string.IsNullOrEmpty(credentials.Username) || string.IsNullOrEmpty(credentials.Password))
                {
                    return BadRequest(new { message = "用户名和密码不能为空" });
                }
                
                // 检查请求用户名与网关传递的用户名是否一致
                if (!Request.Headers.TryGetValue("X-User-Name", out var userNameValue))
                {
                    _logger.LogWarning("请求缺少用户名头");
                    return StatusCode(403, new { message = "未授权的请求" });
                }

                string gatewayUsername = userNameValue.ToString();
                if (gatewayUsername != credentials.Username)
                {
                    _logger.LogWarning($"用户名不匹配: JWT中为{gatewayUsername}，请求中为{credentials.Username}");
                    return StatusCode(403, new { message = "请求的用户名与授权的用户名不一致" });
                }
                
                _logger.LogInformation($"通过爬虫模式获取学生 {credentials.Username} 信息");
                
                var studentInfo = await _studentInfoService.GetStudentInfoByCredentialsAsync(credentials.Username, credentials.Password);
                if (studentInfo == null)
                {
                    return NotFound(new { message = "爬取失败或未找到学生信息" });
                }

                return Ok(new 
                { 
                    message = "通过爬虫模式成功获取学生信息",
                    source = "crawler",
                    data = studentInfo 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"爬取学生信息时发生错误");
                return StatusCode(500, new { message = ex.Message });
            }
        }
    }

    public class LoginModel
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }
}