using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using StudentInfoSystem.ScheduleService.Models;
using StudentInfoSystem.ScheduleService.Services;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System;

namespace StudentInfoSystem.ScheduleService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ScheduleController : ControllerBase
    {
        private readonly CourseScheduleService _scheduleService;
        private readonly ILogger<ScheduleController> _logger;

        public ScheduleController(CourseScheduleService scheduleService, ILogger<ScheduleController> logger)
        {
            _scheduleService = scheduleService;
            _logger = logger;
        }

        /// <summary>
        /// 获取学生课表
        /// </summary>
        /// <param name="request">课表请求参数</param>
        /// <returns>课表响应</returns>
        [HttpPost("get")]
        public async Task<IActionResult> GetSchedule([FromBody] ScheduleRequest request)
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

                // 检查请求用户名与网关传递的用户名是否一致
                /*if (!Request.Headers.TryGetValue("X-User-Name", out var userNameValue))
                {
                    _logger.LogWarning("请求缺少用户名头");
                    return StatusCode(403, new { message = "未授权的请求" });
                }

                string gatewayUsername = userNameValue.ToString();
                if (gatewayUsername != request.Username)
                {
                    _logger.LogWarning($"用户名不匹配: JWT中为{gatewayUsername}，请求中为{request.Username}");
                    return StatusCode(403, new { message = "请求的用户名与授权的用户名不一致" });
                }*/

                // 年份限制检查
                if (!string.IsNullOrEmpty(request.Year) && !string.IsNullOrEmpty(request.Username) && request.Username.Length >= 4)
                {
                    if (int.TryParse(request.Username.Substring(0, 4), out int enrollmentYear) &&
                        int.TryParse(request.Year.Split('-')[0], out int requestYear))
                    {
                        if (requestYear < enrollmentYear)
                        {
                            _logger.LogWarning($"用户 {request.Username} 尝试访问入学前({enrollmentYear})的数据：{requestYear}");
                            return StatusCode(403, new
                            {
                                success = false,
                                message = $"您不能查询入学年份({enrollmentYear})之前的数据"
                            });
                        }
                    }
                }
                
                _logger.LogInformation($"收到获取课表请求，用户: {request.Username}，学年: {request.Year}，学期: {request.Term}");
                
                var result = await _scheduleService.GetScheduleAsync(request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"获取用户 {request.Username} 课表时发生错误");
                return StatusCode(500, new { message = "服务器处理请求时发生错误" });
            }
        }

        /// <summary>
        /// 健康检查接口
        /// </summary>
        [HttpGet("health")]
        public IActionResult Health()
        {
            return Ok(new { status = "healthy", service = "ScheduleService" });
        }
    }
}