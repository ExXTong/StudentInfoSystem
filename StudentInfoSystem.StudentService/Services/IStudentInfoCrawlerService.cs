using StudentInfoSystem.Common.Models;
using System.Threading.Tasks;

namespace StudentInfoSystem.StudentService.Services
{
    /// <summary>
    /// 学生信息爬虫服务接口
    /// </summary>
    public interface IStudentInfoCrawlerService
    {
        /// <summary>
        /// 通过学生本人凭据爬取学生信息
        /// </summary>
        /// <param name="username">用户名</param>
        /// <param name="password">密码</param>
        /// <returns>学生信息</returns>
        Task<StudentInfo> GetStudentInfoByScrapingAsync(string username, string password);
    }
}