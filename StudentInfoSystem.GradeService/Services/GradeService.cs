using StudentInfoSystem.Portal.Models;
using StudentInfoSystem.Portal.Services;
using StudentInfoSystem.Portal;
using HtmlAgilityPack;
using System.Text.RegularExpressions;

using System.Threading.Tasks;

namespace StudentInfoSystem.GradeService.Services
{
    public partial class GradeService
    {
        // 将分隔符数组定义为静态只读字段
        private static readonly char[] SemesterSeparators = { ' ', '-' };
        
        // 为正则表达式定义静态只读字段，使用 GeneratedRegex 特性
        [GeneratedRegex(@"学年：\s*([\d-]+)|学年学期：\s*([\d-]+)\s*学期|学年:\s*([\d-]+)")]
        private static partial Regex YearRegex();
        
        [GeneratedRegex(@"学期：\s*(\d+)|学年学期：\s*[\d-]+\s*学期(\d+)|学期:\s*(\d+)")]
        private static partial Regex TermRegex();

        // 定义标准化的日志消息模板
        private const string LogMsgGeneric = "{Message}";
        private const string LogMsgError = "错误: {ErrorMessage}";
        private const string LogMsgWarning = "警告: {WarningMessage}";

        private readonly IStudentPortalClient _portal;
        private readonly ILogger<GradeService>? _logger;
        private bool _isDebugEnabled;

        public GradeService(IStudentPortalClient portal, ILogger<GradeService>? logger)
        {
            _portal = portal ?? throw new ArgumentNullException(nameof(portal));
            _logger = logger;
        }

        /// <summary>
        /// 设置是否启用调试功能
        /// </summary>
        public void SetDebugEnabled(bool enabled)
        {
            _isDebugEnabled = enabled;
            LogInfo($"调试模式已{(enabled ? "启用" : "禁用")}");
        }

        /// <summary>
        /// 获取成绩信息
        /// </summary>
        /// <param name="username">学号/工号</param>
        /// <param name="password">密码</param>
        /// <param name="year">学年，如"2024-2025"，如果要查看所有学期成绩，设置为null</param>
        /// <param name="term">学期，"1"=第一学期，"2"=第二学期，如果要查看所有学期成绩，设置为null</param>
        /// <param name="allTerms">是否获取所有学期的成绩，当为true时忽略year和term参数</param>
        /// <returns>成绩汇总信息</returns>
        public async Task<GradeSummary> GetGradesAsync(string username, string password, string? year = null, string? term = null, bool allTerms = false)
        {
            try
            {
                LogInfo($"开始为用户 {username} 获取成绩信息...");

                LogInfo($"尝试使用账号 {username} 登录系统...");
                bool loginSuccess = await _portal.LoginAsync(username, password);
                if (!loginSuccess)
                {
                    LogError($"用户 {username} 登录失败，无法获取成绩信息");
                    throw new Exception("登录失败，请检查用户名和密码");
                }

                LogInfo($"用户 {username} 登录成功，准备获取成绩...");

                // 获取成绩页面并解析当前学期
                var gradePage = await _portal.GetGradePageAsync();
                var semesterMatch = Regex.Match(gradePage, @"person!search\.action\?semesterId=(\d+)");
                var semesterId = semesterMatch.Success ? semesterMatch.Groups[1].Value : "";
                var projectType = "";

                LogInfo($"获取成绩列表，semesterId={semesterId}");
                var gradeHtml = allTerms
                    ? await _portal.GetHistoryGradeAsync()
                    : string.IsNullOrEmpty(semesterId)
                        ? gradePage
                        : await _portal.GetGradeSearchAsync(semesterId, projectType);

                var grades = GradeTableParser.ParseGradesFromHtml(gradeHtml);

                // 根据请求参数过滤学年/学期
                if (!allTerms)
                {
                    if (!string.IsNullOrWhiteSpace(year))
                    {
                        grades = grades.Where(g => string.Equals(g.Year?.Trim(), year.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
                    }

                    if (!string.IsNullOrWhiteSpace(term))
                    {
                        grades = grades.Where(g => string.Equals(g.Term?.Trim(), term.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
                    }
                }

                LogInfo($"成功获取到 {grades.Count} 门课程的成绩信息");
                return CalculateGradeSummary(grades);
            }
            catch (Exception ex)
            {
                LogError($"获取成绩信息时发生错误: {ex.Message}");
                throw;
            }
            finally
            {
                await _portal.LogoutAsync();
            }
        }

        private GradeSummary CalculateGradeSummary(List<GradeInfo> grades)
        {
            LogInfo("开始计算成绩汇总统计...");
            
            var summary = new GradeSummary
            {
                Grades = grades
            };
            
            // 计算有效课程（学分大于0）
            var validGrades = grades.Where(g => g.Credits > 0).ToList();
            
            // 统计总课程数和学分
            double totalCredits = validGrades.Sum(g => g.Credits);
            double totalWeightedPoints = 0;
            double totalWeightedScore = 0;
            
            foreach (var grade in validGrades)
            {
                // 计算加权绩点
                if (!string.IsNullOrEmpty(grade.GradePoint) && 
                    double.TryParse(grade.GradePoint, out double point))
                {
                    totalWeightedPoints += point * grade.Credits;
                }
                
                // 计算加权成绩（如果成绩是数字）
                if (!string.IsNullOrEmpty(grade.GradeValue) && 
                    double.TryParse(grade.GradeValue, out double score))
                {
                    totalWeightedScore += score * grade.Credits;
                }
            }
            
            // 设置总学分和课程数
            summary.TotalCredits = Math.Round(totalCredits, 1);
            summary.TotalCourses = grades.Count;
            
            // 计算加权平均绩点和分数
            if (totalCredits > 0)
            {
                summary.AverageGradePoint = Math.Round(totalWeightedPoints / totalCredits, 2);
                summary.AverageScore = Math.Round(totalWeightedScore / totalCredits, 2);
            }
            
            // 修复 CalculateGradeSummary 方法中的 CourseTypeStat 引用
            
            // 添加按课程类型统计
            summary.CourseTypeStats = grades
                .GroupBy(g => g.CourseType)
                .Select(g => new GradeSummary.CourseTypeStat // 使用完全限定名称
                {
                    Type = g.Key,
                    CourseCount = g.Count(),
                    TotalCredits = g.Sum(c => c.Credits),
                    AverageScore = g.Sum(c => c.Credits) > 0 ? 
                        g.Sum(c => ParseScoreToDouble(c.GradeValue) * c.Credits) / g.Sum(c => c.Credits) : 0
                })
                .ToList();
            
            // 统计各类成绩分布
            summary.ScoreDistribution = new Dictionary<string, int> {
                { "优秀(90-100)", 0 },
                { "良好(80-89)", 0 },
                { "中等(70-79)", 0 },
                { "及格(60-69)", 0 },
                { "不及格(<60)", 0 },
                { "其他评级", 0 }
            };
            
            foreach (var grade in grades)
            {
                // 计算成绩分布
                if (double.TryParse(grade.GradeValue, out double score))
                {
                    if (score >= 90) summary.ScoreDistribution["优秀(90-100)"]++;
                    else if (score >= 80) summary.ScoreDistribution["良好(80-89)"]++;
                    else if (score >= 70) summary.ScoreDistribution["中等(70-79)"]++;
                    else if (score >= 60) summary.ScoreDistribution["及格(60-69)"]++;
                    else summary.ScoreDistribution["不及格(<60)"]++;
                }
                else if (!string.IsNullOrEmpty(grade.GradeValue))
                {
                    summary.ScoreDistribution["其他评级"]++;
                }
            }
            
            LogInfo($"成绩统计完成 - 总课程数: {grades.Count}, 总学分: {summary.TotalCredits:F1}");
            LogInfo($"平均绩点: {summary.AverageGradePoint:F2}, 平均分: {summary.AverageScore:F2}");
            
            return summary;
        }
        
        // 辅助方法：将成绩文本转换为数字
        private double ParseScoreToDouble(string scoreText)
        {
            if (string.IsNullOrEmpty(scoreText))
                return 0;
                
            if (double.TryParse(scoreText, out double score))
                return score;
                
            // 处理非数字成绩
            switch (scoreText.ToLowerInvariant())
            {
                case "优秀":
                case "优": return 95;
                case "良好":
                case "良": return 85;
                case "中等":
                case "中": return 75;
                case "合格":
                case "及格":
                case "通过": return 65;
                default: return 0;
            }
        }
            
            // 添加成绩统计数据到扩展属性中
            // 注意：由于GradeSummary类中没有这些属性，需要通过扩展数据字典存储
            // 或者在返回前转换为具有这些属性的视图模型
            
        
        /// <summary>
        /// 记录一般信息日志
        /// </summary>
        private void LogInfo(string message)
        {
            _logger?.LogInformation(LogMsgGeneric, message);
        }
        
        /// <summary>
        /// 记录警告日志
        /// </summary>
        private void LogWarning(string message)
        {
            _logger?.LogWarning(LogMsgWarning, message);
        }
        
        /// <summary>
        /// 记录错误日志
        /// </summary>
        private void LogError(string message)
        {
            _logger?.LogError(LogMsgError, message);
        }
    }
}
