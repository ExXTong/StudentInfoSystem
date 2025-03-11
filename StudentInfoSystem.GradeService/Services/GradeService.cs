using Microsoft.Playwright;
using StudentInfoSystem.Common.Models;
using StudentInfoSystem.Common.Services;
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

        private readonly IBrowserManager _browserManager;
        private readonly ILogger<GradeService>? _logger;
        private IPage? _currentPage;
        private readonly string _debugFolder;
        private bool _isDebugEnabled;

        public GradeService(IBrowserManager browserManager, ILogger<GradeService>? logger)
        {
            _browserManager = browserManager ?? throw new ArgumentNullException(nameof(browserManager));
            _logger = logger;
            _debugFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug_screenshots");
            _isDebugEnabled = true; // 默认启用调试
            _currentPage = null; // 初始化为 null
            
            // 确保截图保存目录存在
            if (!Directory.Exists(_debugFolder))
                Directory.CreateDirectory(_debugFolder);
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
        public async Task<GradeSummary> GetGradesAsync(string username, string password, string year = null, string term = null, bool allTerms = false)
        {
            IPage? page = null;
            try
            {
                LogInfo($"开始为用户 {username} 获取成绩信息...");
                
                // 从池中获取页面
                page = await _browserManager.GetPageAsync();
                LogInfo($"为用户 {username} 获取到新页面实例");
                
                // 使用浏览器管理器进行登录
                LogInfo($"尝试使用账号 {username} 登录系统...");
                bool loginSuccess = await _browserManager.LoginAsync(username, password, page);
                
                if (!loginSuccess)
                {
                    LogError($"用户 {username} 登录失败，无法获取成绩信息");
                    throw new Exception("登录失败，请检查用户名和密码");
                }
                
                LogInfo($"用户 {username} 登录成功，准备获取成绩...");
                _currentPage = page; // 保存当前页面引用用于调试截图

                // 刷新页面以确保获取最新状态
                LogInfo("刷新页面...");
                await page.ReloadAsync();
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                
                LogInfo($"用户 {username} 登录成功，准备获取成绩...");
                _currentPage = page; // 保存当前页面引用用于调试截图

                // 直接导航到成绩页面URL
                LogInfo("直接导航到成绩历史页面...");
                await page.GotoAsync("https://tam.nwupl.edu.cn/eams/teach/grade/course/person!historyCourseGrade.action?projectType=MAJOR");
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                
                // 捕获可能出现的异常情况
                if (await page.GetByText("无权访问").IsVisibleAsync())
                {
                    LogWarning("当前页面显示'无权访问'，尝试回退到原有导航方式");
                    // 回退到原有导航方式
                    await page.ReloadAsync();
                    await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                    
                    // 等待"我的"链接出现并确保可点击
                    LogInfo("等待'我的'链接可用...");
                    var myLink = page.GetByRole(AriaRole.Link, new() { Name = "我的", Exact = true });
                    await myLink.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
                    
                    // 点击"我的"链接
                    LogInfo("点击'我的'链接...");
                    await myLink.ClickAsync();
                    
                    // 等待"我的成绩"链接出现并确保可点击
                    LogInfo("等待'我的成绩'链接可用...");
                    var gradesLink = page.GetByRole(AriaRole.Link, new() { Name = "我的成绩" });
                    await gradesLink.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
                    
                    // 点击"我的成绩"链接
                    LogInfo("点击'我的成绩'链接...");
                    await gradesLink.ClickAsync();
                    await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                }
                
                // 检查页面是否包含成绩信息
                var hasGradeContent = await page.GetByText("成绩").IsVisibleAsync() || 
                                    await page.GetByText("学年").IsVisibleAsync() || 
                                    await page.GetByText("课程").IsVisibleAsync();
                if (!hasGradeContent)
                {
                    LogInfo("直接访问成绩页面未找到成绩内容，检查iframe...");
                }
                
                // 检查是否有iframe
                IFrame? frame = null;
                var iframeExists = await page.Locator("iframe[name=\"iframeMain\"]").CountAsync() > 0;
                
                if (iframeExists) 
                {
                    // 等待iframe加载完成，使用更健壮的等待方式
                    LogInfo("等待iframe加载完成...");
                    await page.WaitForSelectorAsync("iframe[name=\"iframeMain\"]", new() { State = WaitForSelectorState.Attached, Timeout = 10000 });
                    
                    frame = page.Frame("iframeMain");
                    if (frame == null)
                    {
                        LogWarning("无法通过名称直接获取iframe，尝试备用方法...");
                        var iframeElement = await page.QuerySelectorAsync("iframe[name=\"iframeMain\"]");
                        if (iframeElement != null)
                        {
                            frame = await iframeElement.ContentFrameAsync();
                        }
                    }
                    
                    if (frame == null)
                    {
                        LogWarning("无法找到成绩iframe，将在主页面中查找成绩表格");
                    }
                    else
                    {
                        LogInfo("成功获取到iframe");
                    }
                }
                else
                {
                    LogInfo("页面中没有发现iframe，直接在主页面解析成绩...");
                }
                
                // 获取页面内容
                LogInfo("获取成绩页面内容...");
                string pageContent;
                
                if (frame != null)
                {
                    pageContent = await frame.EvaluateAsync<string>("() => document.documentElement.outerHTML");
                }
                else
                {
                    pageContent = await page.ContentAsync();
                }
                
                // 解析成绩数据
                LogInfo("解析成绩数据...");
                var grades = ParseGradesFromHtml(pageContent);
                
                // 计算成绩统计信息
                var summary = CalculateGradeSummary(grades);
                
                LogInfo($"成功获取到 {grades.Count} 门课程的成绩信息");
                return summary;
            }
            catch (Exception ex)
            {
                LogError($"获取成绩信息时发生错误: {ex.Message}");
                throw;
            }
            finally
            {
                // 释放页面实例
                if (page != null)
                {
                    try {
                        // 添加注销操作
                        LogInfo("正在注销用户...");
                        bool logoutSuccess = await _browserManager.LogoutAsync(page);
                        if (logoutSuccess)
                        {
                            LogInfo("用户已成功注销");
                        }
                        else
                        {
                            LogWarning("注销操作可能未成功完成");
                        }
                        
                        // 释放浏览器资源
                        await _browserManager.ReleaseBrowserAsync(page);
                        LogInfo("已释放用于获取成绩的页面实例");
                    }
                    catch (Exception ex) {
                        LogError($"释放页面或注销时发生错误: {ex.Message}");
                    }
                }
                
                // 解除当前页面引用
                _currentPage = null;
            }
        }

        private List<GradeInfo> ParseGradesFromHtml(string htmlContent)
        {
            var grades = new List<GradeInfo>();
            var doc = new HtmlDocument();
            doc.LoadHtml(htmlContent);
        
            try {
                LogInfo("开始解析成绩HTML内容...");
                
                // 1. 首先尝试解析成绩详细列表表格
                /*var gradeTable = doc.DocumentNode.SelectSingleNode("//table[@id and contains(@class, 'gridtable') and .//th[contains(text(), '课程名称')]]");
                if (gradeTable != null)
                {
                    LogInfo("找到课程成绩详细表格");
                    var rows = gradeTable.SelectNodes(".//tbody//tr");
                    if (rows != null && rows.Count > 0)
                    {
                        return ParseGradeTableRows(rows);
                    }
                }*/
                
                // 2. 尝试更通用的方法查找成绩表格
                /*var tables = doc.DocumentNode.SelectNodes("//table[contains(@class, 'gridtable')]");
                if (tables != null)
                {
                    foreach (var table in tables)
                    {
                        // 检查表头，确认是否为成绩表格
                        var headers = table.SelectNodes(".//thead//th");
                        if (headers != null && 
                            headers.Any(h => h.InnerText.Contains("课程名称") || 
                                          h.InnerText.Contains("学分") || 
                                          h.InnerText.Contains("成绩") ||
                                          h.InnerText.Contains("最终")))
                        {
                            LogInfo("找到成绩表格（通用方法）");
                            var rows = table.SelectNodes(".//tbody//tr");
                            if (rows != null && rows.Count > 0)
                            {
                                return ParseGradeTableRows(rows);
                            }
                        }
                    }
                }*/
                
                // 3. 尝试按ID查找特定表格
                var gridTable = doc.DocumentNode.SelectSingleNode("//table[starts-with(@id, 'grid')]");
                if (gridTable != null)
                {
                    LogInfo($"找到ID为 {gridTable.GetAttributeValue("id", "unknown")} 的表格");
                    var rows = gridTable.SelectNodes(".//tbody//tr");
                    if (rows != null && rows.Count > 0)
                    {
                        return ParseGradeTableRows(rows);
                    }
                }
                
                // 4. 最后尝试旧的方法
                // 查找成绩表格，尝试多种可能的选择器
                var tbody = doc.DocumentNode.SelectNodes("//tbody[starts-with(@id, 'grid') and contains(@id, '_data')]")?.FirstOrDefault();
                if (tbody != null)
                {
                    LogInfo("找到grid表格数据");
                    var rows = tbody.SelectNodes(".//tr");
                    if (rows != null && rows.Count > 0)
                    {
                        return ParseGridTableRows(rows, htmlContent);
                    }
                }
                
                LogWarning("未能找到任何包含成绩的表格");
            }
            catch (Exception ex) {
                LogError($"解析HTML内容时发生异常: {ex.Message}");
            }
        
            return grades;
        }
        
        /// <summary>
        /// 解析成绩表格行
        /// </summary>
        private List<GradeInfo> ParseGradeTableRows(HtmlNodeCollection rows)
        {
            var grades = new List<GradeInfo>();
            LogInfo($"开始解析表格，共 {rows.Count} 行数据");
            
            foreach (var row in rows)
            {
                try 
                {
                    // 直接选择单元格，不使用相对路径选择器提高效率
                    var cells = row.SelectNodes("td");
                    if (cells == null || cells.Count < 4)
                    {
                        // 检查是否为汇总行或表头
                        bool isSummaryRow = row.InnerText.Contains("在校汇总") || 
                                           row.InnerText.Contains("统计时间") ||
                                           row.SelectSingleNode(".//th") != null;
                        if (isSummaryRow)
                        {
                            LogInfo("跳过汇总统计行");
                            continue;
                        }
                        
                        LogWarning($"跳过无效行: 单元格数量不足({cells?.Count ?? 0})");
                        continue;
                    }
                
                    // 初始化成绩对象
                    var grade = new GradeInfo();
                    
                    // 解析学年学期
                    if (cells.Count > 0)
                    {
                        string semesterText = cells[0].InnerText.Trim();
                        if (!string.IsNullOrEmpty(semesterText))
                        {
                            // 尝试从类似"2023-2024 1"的格式中提取学年和学期
                            var parts = semesterText.Split(SemesterSeparators, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 2)
                            {
                                grade.Year = parts[0].Trim(); // 例如"2023-2024"
                                grade.Term = parts[1].Trim(); // 例如"1"或"2"
                            }
                            else
                            {
                                // 使用正则表达式提取方法
                                grade.Year = ExtractAcademicYear(semesterText);
                                grade.Term = ExtractAcademicTerm(semesterText);
                            }
                        }
                    }
                    
                    // 解析课程代码
                    if (cells.Count > 1)
                    {
                        grade.CourseId = cells[1].InnerText.Trim();
                    }
                    
                    // 解析课程名称
                    if (cells.Count > 3)
                    {
                        grade.CourseName = cells[3].InnerText.Trim();
                    }
                    
                    // 解析课程类型
                    if (cells.Count > 4)
                    {
                        grade.CourseType = cells[4].InnerText.Trim();
                    }
                    
                    // 解析学分
                    if (cells.Count > 5)
                    {
                        string creditText = cells[5].InnerText.Trim();
                        if (double.TryParse(creditText, out double credit))
                        {
                            grade.Credits = credit;
                        }
                    }
                    
                    // 解析成绩
                    if (cells.Count > 6)
                    {
                        grade.GradeValue = cells[6].InnerText.Trim();
                        
                        // 处理非数字成绩，如"优秀"、"良好"等
                        if (!double.TryParse(grade.GradeValue, out _))
                        {
                            switch (grade.GradeValue.ToLowerInvariant())
                            {
                                case "优秀":
                                case "优":
                                    grade.GradePoint = "5.0";
                                    break;
                                case "良好":
                                case "良":
                                    grade.GradePoint = "4.0";
                                    break;
                                case "中等":
                                case "中":
                                    grade.GradePoint = "3.0";
                                    break;
                                case "合格":
                                case "及格":
                                case "通过":
                                    grade.GradePoint = "1.0";
                                    break;
                                case "不合格":
                                case "不及格":
                                case "不通过":
                                    grade.GradePoint = "0.0";
                                    break;
                            }
                        }
                    }
                    
                    // 解析绩点
                    if (cells.Count > 7 && string.IsNullOrEmpty(grade.GradePoint))
                    {
                        grade.GradePoint = cells[7].InnerText.Trim();
                    }
                    
                    // 检查必要字段
                    if (string.IsNullOrEmpty(grade.CourseName))
                    {
                        LogWarning("跳过没有课程名称的条目");
                        continue;
                    }
                    
                    // 添加到结果列表
                    grades.Add(grade);
                    LogInfo($"解析到课程: {grade.CourseName}, 成绩: {grade.GradeValue}, 学分: {grade.Credits}");
                }
                catch (Exception ex)
                {
                    LogError($"解析单行成绩数据时出错: {ex.Message}");
                }
            }
            
            LogInfo($"表格解析完成，共解析出 {grades.Count} 条成绩记录");
            return grades;
        }
        
        /// <summary>
        /// 专门解析grid结构的表格行
        /// </summary>
        private List<GradeInfo> ParseGridTableRows(HtmlNodeCollection rows, string htmlContent)
        {
            var grades = new List<GradeInfo>();
            LogInfo($"开始解析Grid表格，共 {rows.Count} 行数据");
            
            // Grid表格通常每行都是数据，不需要跳过表头
            foreach (var row in rows)
            {
                try 
                {
                    // 获取所有单元格
                    var cells = row.SelectNodes(".//td");
                    if (cells == null || cells.Count < 6)
                    {
                        LogWarning($"跳过无效行: 单元格数量不足({(cells?.Count ?? 0)})");
                        continue;
                    }
        
                    // 初始化成绩对象
                    var grade = new GradeInfo();
                    
                    // 解析课程ID和名称
                    // 通常格式为课程ID-课程名称
                    string courseText = cells[0].InnerText.Trim();
                    if (!string.IsNullOrEmpty(courseText))
                    {
                        // 尝试分离课程ID和课程名称
                        var parts = courseText.Split(new[] { '-', '：', ':' }, 2);
                        if (parts.Length > 1)
                        {
                            grade.CourseId = parts[0].Trim();
                            grade.CourseName = parts[1].Trim();
                        }
                        else
                        {
                            // 如果无法分离，则整体作为课程名称
                            grade.CourseName = courseText;
                            // 尝试在其他单元格中寻找课程ID
                            for (int i = 1; i < Math.Min(3, cells.Count); i++)
                            {
                                string cellText = cells[i].InnerText.Trim();
                                if (Regex.IsMatch(cellText, @"^\w+\d+$")) // 简单判断是否可能是课程ID
                                {
                                    grade.CourseId = cellText;
                                    break;
                                }
                            }
                        }
                    }

                    // 解析学分
                    int creditIndex = 1; // 学分通常在第二列，但根据具体表格可能需要调整
                    if (cells.Count > creditIndex)
                    {
                        string creditText = cells[creditIndex].InnerText.Trim();
                        if (double.TryParse(creditText, out double credit))
                        {
                            grade.Credits = credit;
                        }
                    }

                    // 解析成绩
                    int scoreIndex = 3; // 成绩通常在第四列，但根据具体表格可能需要调整
                    if (cells.Count > scoreIndex)
                    {
                        string scoreText = cells[scoreIndex].InnerText.Trim();
                        
                        if (!string.IsNullOrEmpty(scoreText))
                        {
                            // 处理非数字成绩，如"优秀"、"良好"等
                            grade.GradeValue = scoreText;
                            
                            // 将评级转换为绩点和分数值
                            switch (scoreText)
                            {
                                case "优秀":
                                case "优":
                                    grade.GradePoint = "5.0";
                                    break;
                                case "良好":
                                case "良":
                                    grade.GradePoint = "4.0";
                                    break;
                                case "中等":
                                case "中":
                                    grade.GradePoint = "3.0";
                                    break;
                                case "合格":
                                case "及格":
                                case "通过":
                                    grade.GradePoint = "1.0";
                                    break;
                                case "不合格":
                                case "不及格":
                                case "不通过":
                                case "未通过":
                                    grade.GradePoint = "0.0";
                                    break;
                                default:
                                    grade.GradePoint = null; // 无法转换为绩点
                                    break;
                            }
                        }
                    }
                    
                   
                    // 解析学年学期信息
                    if (cells.Count > 4)
                    {
                        string semesterText = cells[4].InnerText.Trim();
                        if (!string.IsNullOrEmpty(semesterText))
                        {
                            // 尝试从学年学期文本中提取学年和学期
                            grade.Year = ExtractAcademicYear(semesterText);
                            grade.Term = ExtractAcademicTerm(semesterText);
                        }
                    }
                    
                    // 解析课程类型
                    if (cells.Count > 2)
                    {
                        grade.CourseType = cells[2].InnerText.Trim();
                    }
                    
                    // 解析备注（如有）
                    if (cells.Count > 5)
                    {
                        grade.Remark = cells[5].InnerText.Trim();
                    }
                    
                    // 检查必要字段
                    if (string.IsNullOrEmpty(grade.CourseName))
                    {
                        LogWarning("跳过没有课程名称的条目");
                        continue;
                    }
                    
                    // 添加到结果列表
                    grades.Add(grade);
                    LogInfo($"解析到课程: {grade.CourseName}, 成绩: {grade.GradeValue}, 学分: {grade.Credits}");
                }
                catch (Exception ex)
                {
                    LogError($"解析单行成绩数据时出错: {ex.Message}");
                }
            }
            
            LogInfo($"Grid表格解析完成，共解析出 {grades.Count} 条成绩记录");
            return grades;
        }
        
        /// <summary>
        /// 从学期文本中提取学年信息
        /// </summary>
        private string ExtractAcademicYear(string semesterText)
        {
            var match = YearRegex().Match(semesterText);
            if (match.Success)
            {
                // 检查各个捕获组，返回第一个非空的捕获值
                for (int i = 1; i < match.Groups.Count; i++)
                {
                    if (!string.IsNullOrEmpty(match.Groups[i].Value))
                    {
                        return match.Groups[i].Value.Trim();
                    }
                }
            }
            
            // 尝试根据常见格式直接拆分
            string[] parts = semesterText.Split(SemesterSeparators, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0)
            {
                // 寻找类似于"2023-2024"格式的部分
                foreach (var part in parts)
                {
                    if (Regex.IsMatch(part, @"\d{4}-\d{4}"))
                    {
                        return part.Trim();
                    }
                }
            }
            
            return string.Empty;
        }
        
        /// <summary>
        /// 从学期文本中提取学期信息
        /// </summary>
        private string ExtractAcademicTerm(string semesterText)
        {
            var match = TermRegex().Match(semesterText);
            if (match.Success)
            {
                // 检查各个捕获组，返回第一个非空的捕获值
                for (int i = 1; i < match.Groups.Count; i++)
                {
                    if (!string.IsNullOrEmpty(match.Groups[i].Value))
                    {
                        return match.Groups[i].Value.Trim();
                    }
                }
            }
            
            // 尝试根据常见关键词判断
            if (semesterText.Contains("第一学期") || semesterText.Contains("第1学期") || semesterText.Contains("学期1"))
            {
                return "1";
            }
            else if (semesterText.Contains("第二学期") || semesterText.Contains("第2学期") || semesterText.Contains("学期2"))
            {
                return "2";
            }
            
            return string.Empty;
        }
        
        /// <summary>
        /// 计算成绩汇总信息
        /// </summary>
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
