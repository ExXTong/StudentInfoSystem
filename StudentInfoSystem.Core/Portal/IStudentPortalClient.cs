using System.Threading.Tasks;

namespace StudentInfoSystem.Core.Portal
{
    public interface IStudentPortalClient
    {
        Task<bool> LoginAsync(string username, string password);
        Task<string> GetHomePageAsync();
        Task<string> GetStudentDetailAsync();
        Task<string> GetGradePageAsync();
        Task<string> GetHistoryGradeAsync();
        Task<string> GetGradeSearchAsync(string semesterId, string projectType);
        Task<string> GetCourseTablePageAsync();
        Task<string> GetCourseTableDataAsync(string semesterId, string projectId, string ids, string kind);
        Task LogoutAsync();
    }
}
