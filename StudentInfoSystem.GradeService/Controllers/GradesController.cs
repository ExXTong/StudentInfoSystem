using Microsoft.AspNetCore.Mvc;
using StudentInfoSystem.Common.Models;
using StudentInfoSystem.GradeService.Services;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

using GradeServiceImpl = StudentInfoSystem.GradeService.Services.GradeService;

namespace StudentInfoSystem.GradeService.Controllers
{
    [ApiController]
    [Route("api/grade")]
    public class GradesController : ControllerBase
    {
        private readonly GradeServiceImpl _gradeService;
        private readonly ILogger<GradesController> _logger;

        public GradesController(GradeServiceImpl gradeService, ILogger<GradesController> logger)
        {
            _gradeService = gradeService ?? throw new ArgumentNullException(nameof(gradeService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// 获取学生成绩信息
        /// </summary>
        /// <param name="request">成绩查询请求</param>
        /// <returns>成绩汇总信息</returns>
        [HttpPost]
        [Route("query")]
        [ProducesResponseType(typeof(GradeQueryResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetGrades([FromBody] GradeQueryRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "InvalidRequest",
                    ErrorMessage = "请求参数无效",
                    Details = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList()
                });
            }

            try
            {
                // 验证请求是否来自网关
                if (!Request.Headers.TryGetValue("X-Gateway-Source", out var sourceValue) || 
                    sourceValue != "StudentInfoGateway")
                {
                    _logger.LogWarning("拒绝非网关来源的请求");
                    return StatusCode(403, new ErrorResponse
                    {
                        ErrorCode = "Forbidden",
                        ErrorMessage = "只接受来自授权网关的请求"
                    });
                }

                // 检查请求用户名与网关传递的用户名是否一致
                /*if (!Request.Headers.TryGetValue("X-User-Name", out var userNameValue))
                {
                    _logger.LogWarning("请求缺少用户名头");
                    return StatusCode(403, new ErrorResponse
                    {
                        ErrorCode = "Forbidden",
                        ErrorMessage = "未授权的请求"
                    });
                }

                string gatewayUsername = userNameValue.ToString();
                if (gatewayUsername != request.Username)
                {
                    _logger.LogWarning($"用户名不匹配: JWT中为{gatewayUsername}，请求中为{request.Username}");
                    return StatusCode(403, new ErrorResponse
                    {
                        ErrorCode = "Forbidden",
                        ErrorMessage = "请求的用户名与授权的用户名不一致"
                    });
                }*/

                // 年份限制检查 - 如果提供了年份参数
                if (!request.AllTerms && !string.IsNullOrEmpty(request.Year) && !string.IsNullOrEmpty(request.Username) && request.Username.Length >= 4)
                {
                    if (int.TryParse(request.Username.Substring(0, 4), out int enrollmentYear) &&
                        int.TryParse(request.Year.Split('-')[0], out int requestYear))
                    {
                        if (requestYear < enrollmentYear)
                        {
                            _logger.LogWarning($"用户 {request.Username} 尝试访问入学前({enrollmentYear})的数据：{requestYear}");
                            return StatusCode(403, new ErrorResponse
                            {
                                ErrorCode = "Forbidden",
                                ErrorMessage = $"您不能查询入学年份({enrollmentYear})之前的数据"
                            });
                        }
                    }
                }

                _logger.LogInformation("接收到成绩查询请求，用户名：{Username}", request.Username);
                
                var gradeSummary = await _gradeService.GetGradesAsync(
                    request.Username,
                    request.Password,
                    request.Year,
                    request.Term,
                    request.AllTerms);
                
                _logger.LogInformation("成功获取用户 {Username} 的成绩信息，共 {CourseCount} 门课程",
                    request.Username, gradeSummary.Grades.Count);
                
                var response = new GradeQueryResponse
                {
                    Success = true,
                    GradeSummary = gradeSummary
                };
                
                return Ok(response);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning("用户 {Username} 登录失败: {Message}", request.Username, ex.Message);
                return Unauthorized(new ErrorResponse
                {
                    ErrorCode = "Unauthorized",
                    ErrorMessage = "用户名或密码错误",
                    Details = new List<string> { ex.Message }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取用户 {Username} 成绩时发生错误", request.Username);
                return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse
                {
                    ErrorCode = "ServerError",
                    ErrorMessage = "服务器处理请求时发生错误",
                    Details = new List<string> { "请稍后再试或联系管理员" }
                });
            }
        }

        [HttpPost]
        [Route("debug")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        public IActionResult SetDebugMode([FromBody] DebugModeRequest request)
        {
            // 验证请求是否来自网关
            if (!Request.Headers.TryGetValue("X-Gateway-Source", out var sourceValue) || 
                sourceValue != "StudentInfoGateway")
            {
                _logger.LogWarning("拒绝非网关来源的请求");
                return StatusCode(403, new ErrorResponse
                {
                    ErrorCode = "Forbidden",
                    ErrorMessage = "只接受来自授权网关的请求"
                });
            }

            try
            {
                _gradeService.SetDebugEnabled(request.Enabled);
                _logger.LogInformation("调试模式已{Status}", request.Enabled ? "启用" : "禁用");
                return Ok(new { success = true, message = $"调试模式已{(request.Enabled ? "启用" : "禁用")}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "设置调试模式时发生错误");
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "BadRequest",
                    ErrorMessage = "设置调试模式失败",
                    Details = new List<string> { ex.Message }
                });
            }
        }
    }

    /// <summary>
    /// 成绩查询请求
    /// </summary>
    public class GradeQueryRequest
    {
        /// <summary>
        /// 学号/工号
        /// </summary>
        [Required]
        public string Username { get; set; }

        /// <summary>
        /// 密码
        /// </summary>
        [Required]
        public string Password { get; set; }

        /// <summary>
        /// 学年，如"2024-2025"
        /// </summary>
        public string? Year { get; set; }

        /// <summary>
        /// 学期，"1"=第一学期，"2"=第二学期
        /// </summary>
        public string? Term { get; set; }

        /// <summary>
        /// 是否获取所有学期的成绩，当为true时忽略year和term参数
        /// </summary>
        public bool AllTerms { get; set; } = false;
    }

    /// <summary>
    /// 成绩查询响应
    /// </summary>
    public class GradeQueryResponse
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 成绩汇总信息
        /// </summary>
        public GradeSummary GradeSummary { get; set; }
    }

    /// <summary>
    /// 错误响应
    /// </summary>
    public class ErrorResponse
    {
        /// <summary>
        /// 错误代码
        /// </summary>
        public string ErrorCode { get; set; }

        /// <summary>
        /// 错误消息
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// 错误详情
        /// </summary>
        public List<string> Details { get; set; } = new List<string>();
    }

    /// <summary>
    /// 调试模式请求
    /// </summary>
    public class DebugModeRequest
    {
        /// <summary>
        /// 是否启用调试模式
        /// </summary>
        public bool Enabled { get; set; }
    }
}