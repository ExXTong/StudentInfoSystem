using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StudentInfoSystem.Common.Models;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace StudentInfoSystem.StudentService.Services
{
    public class StudentInfoService : IStudentInfoService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<StudentInfoService> _logger;
        private readonly IStudentInfoCrawlerService _crawlerService;

        public StudentInfoService(
            HttpClient httpClient, 
            IConfiguration configuration, 
            ILogger<StudentInfoService> logger, 
            IStudentInfoCrawlerService crawlerService)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
            _crawlerService = crawlerService;
        }

        public async Task<StudentInfo> GetStudentInfoByCredentialsAsync(string username, string password)
        {
            try
            {
                _logger.LogInformation($"使用学生凭据获取 {username} 的信息");
                
                // 直接使用爬虫抓取学生本人信息
                var studentInfo = await _crawlerService.GetStudentInfoByScrapingAsync(username, password);
                
                if (studentInfo != null)
                {
                    _logger.LogInformation($"成功获取学生 {username} 的信息");
                    return studentInfo;
                }
                
                _logger.LogWarning($"无法使用凭据获取学生 {username} 的信息");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"使用凭据获取学生 {username} 信息时发生错误");
                throw;
            }
        }
    }
}