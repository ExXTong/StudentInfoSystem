using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StudentInfoSystem.Common.Models;
using StudentInfoSystem.Common.Services;
using StudentInfoSystem.Portal;

namespace StudentInfoSystem.StudentService.Services
{
    /// <summary>
    /// 学生信息爬虫服务（HTTP 版），不依赖 Playwright。
    /// 当前通过学生门户登录后获取首页；详细学籍信息接口待补充。
    /// </summary>
    public class StudentInfoCrawlerService : IStudentInfoCrawlerService
    {
        private readonly IStudentPortalClient _portal;
        private readonly IConfiguration _configuration;
        private readonly ILogger<StudentInfoCrawlerService> _logger;
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        public StudentInfoCrawlerService(
            IStudentPortalClient portal,
            IConfiguration configuration,
            ILogger<StudentInfoCrawlerService> logger)
        {
            _portal = portal;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<StudentInfo> GetStudentInfoByScrapingAsync(string username, string password)
        {
            await _semaphore.WaitAsync();
            try
            {
                _logger.LogInformation("开始使用用户 {Username} 凭据获取学生信息", username);

                var loginSuccess = await _portal.LoginAsync(username, password);
                if (!loginSuccess)
                {
                    _logger.LogWarning("爬取页面登录失败，无法获取学生信息");
                    return null;
                }

                var detailHtml = await _portal.GetStudentDetailAsync();
                _logger.LogInformation("已获取学籍信息页面，长度 {Length}", detailHtml?.Length ?? 0);

                var studentInfo = StudentInfoParser.ParseStudentInfoFromHtml(detailHtml ?? "");
                if (studentInfo == null)
                {
                    studentInfo = new StudentInfo();
                }

                if (string.IsNullOrEmpty(studentInfo.StudentId))
                {
                    studentInfo.StudentId = username;
                }

                return studentInfo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "爬取学生信息时发生异常: {Message}", ex.Message);
                return null;
            }
            finally
            {
                await _portal.LogoutAsync();
                _semaphore.Release();
            }
        }
    }
}
