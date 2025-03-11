using StudentInfoSystem.Common.Models;
using System.Threading.Tasks;

namespace StudentInfoSystem.StudentService.Services
{
    public interface IStudentInfoService
    {
        /// <summary>
        /// 通过学生凭据获取学生信息（爬虫模式）
        /// </summary>
        Task<StudentInfo> GetStudentInfoByCredentialsAsync(string username, string password);
    }
}