using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using StudentInfoSystem.Portal;
using CourseInfo = StudentInfoSystem.Portal.Models.CourseInfo;
using StudentInfoSystem.Portal.Services;
using StudentInfoSystem.ScheduleService.Models;
using HtmlAgilityPack;

namespace StudentInfoSystem.ScheduleService.Services
{
    // 将类名从ScheduleService改为CourseScheduleService
    public class CourseScheduleService
    {
        private readonly IStudentPortalClient _portal;
        private readonly ILogger<CourseScheduleService> _logger;

        public CourseScheduleService(IStudentPortalClient portal, ILogger<CourseScheduleService> logger)
        {
            _portal = portal;
            _logger = logger;
        }

        /// <summary>
        /// 获取学生课表
        /// </summary>
        /// <param name="request">课表查询请求</param>
        /// <returns>课表查询响应</returns>
        public async Task<ScheduleResponse> GetScheduleAsync(ScheduleRequest request)
        {
            try
            {
                if (request == null)
                {
                    return new ScheduleResponse { Success = false, ErrorMessage = "请求参数不能为空" };
                }

                if (string.IsNullOrWhiteSpace(request.TableType))
                {
                    request.TableType = "std";
                }

                if (request.TableType != "std" && request.TableType != "class")
                {
                    return new ScheduleResponse { Success = false, ErrorMessage = "课表类型仅支持 std 或 class" };
                }

                if (string.IsNullOrWhiteSpace(request.Username) ||
                    string.IsNullOrWhiteSpace(request.Password) ||
                    string.IsNullOrWhiteSpace(request.Year) ||
                    string.IsNullOrWhiteSpace(request.Term))
                {
                    return new ScheduleResponse { Success = false, ErrorMessage = "用户名、密码、学年和学期不能为空" };
                }

                _logger.LogInformation($"开始获取用户 {request.Username} 的课表信息");

                bool loginSuccess = await _portal.LoginAsync(request.Username, request.Password);
                if (!loginSuccess)
                {
                    _logger.LogWarning($"用户 {request.Username} 登录失败");
                    return new ScheduleResponse
                    {
                        Success = false,
                        ErrorMessage = "登录失败，请检查用户名和密码"
                    };
                }

                _logger.LogInformation($"用户 {request.Username} 登录成功，开始获取课表");

                var courseTablePage = await _portal.GetCourseTablePageAsync();
                var semesterMatch = Regex.Match(courseTablePage, @"name=""semester\.id""\s+value=""([^""]*)""");
                var semesterId = semesterMatch.Success ? semesterMatch.Groups[1].Value : "";

                var idsMatch = Regex.Match(courseTablePage, @"bg\.form\.addInput\(form,""ids"",""(\d+)""\)");
                var ids = idsMatch.Success ? idsMatch.Groups[1].Value : (request.TableType == "std" ? "676535" : "2183");

                var projectId = "1";
                var projectMatch = Regex.Match(courseTablePage, @"name=""project\.id""[^>]*value=""([^""]*)""|value=""(\d+)""[^>]*name=""project\.id""");
                if (projectMatch.Success)
                {
                    projectId = projectMatch.Groups[1].Success ? projectMatch.Groups[1].Value : projectMatch.Groups[2].Value;
                }

                if (string.IsNullOrEmpty(semesterId))
                {
                    // 初始页面 semester.id 由前端 JS 动态填充，这里先回退到常用学期 ID
                    semesterId = "194";
                    _logger.LogWarning("课表页面未解析到 semester.id，使用默认 194");
                }

                var scheduleHtml = await _portal.GetCourseTableDataAsync(semesterId, projectId, ids, request.TableType);
                if (string.IsNullOrEmpty(scheduleHtml))
                {
                    _logger.LogWarning($"获取课表HTML内容失败");
                    return new ScheduleResponse { Success = false, ErrorMessage = "获取课表数据失败" };
                }

                var courses = await CourseScheduleParser.ParseScheduleHtmlAsync(scheduleHtml);
                _logger.LogInformation($"成功解析 {courses.Count} 门课程的信息");

                return new ScheduleResponse
                {
                    Success = true,
                    Courses = courses
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"获取课表时发生错误: {ex.Message}");
                return new ScheduleResponse
                {
                    Success = false,
                    ErrorMessage = $"获取课表时发生错误: {ex.Message}"
                };
            }
            finally
            {
                await _portal.LogoutAsync();
            }
        }

        /// <summary>
        /// 解析课表HTML
        /// </summary>
    }
}
