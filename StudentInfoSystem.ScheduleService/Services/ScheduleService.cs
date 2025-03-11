using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using StudentInfoSystem.Common.Services;
using StudentInfoSystem.ScheduleService.Models;
using HtmlAgilityPack;

namespace StudentInfoSystem.ScheduleService.Services
{
    // 将类名从ScheduleService改为CourseScheduleService
    public class CourseScheduleService
    {
        private readonly IBrowserManager _browserManager;
        private readonly ILogger<CourseScheduleService> _logger;

        // 构造函数也需要更新
        public CourseScheduleService(IBrowserManager browserManager, ILogger<CourseScheduleService> logger)
        {
            _browserManager = browserManager;
            _logger = logger;
        }

        /// <summary>
        /// 获取学生课表
        /// </summary>
        /// <param name="request">课表查询请求</param>
        /// <returns>课表查询响应</returns>
        public async Task<ScheduleResponse> GetScheduleAsync(ScheduleRequest request)
        {
            IPage page = null;
            
            try
            {
                _logger.LogInformation($"开始获取用户 {request.Username} 的课表信息");
                
                // 从池中获取页面
                page = await _browserManager.GetPageAsync();
                _logger.LogInformation($"为用户 {request.Username} 获取到新页面实例");
                
                // 使用页面进行登录
                bool loginSuccess = await _browserManager.LoginAsync(request.Username, request.Password, page);
                
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
                
                // 获取课表HTML内容
                var scheduleHtml = await GetScheduleHtmlAsync(
                    page, 
                    request.TableType, 
                    request.Year, 
                    request.Term);
                
                if (string.IsNullOrEmpty(scheduleHtml))
                {
                    _logger.LogWarning($"获取课表HTML内容失败");
                    return new ScheduleResponse 
                    { 
                        Success = false, 
                        ErrorMessage = "获取课表数据失败" 
                    };
                }
                
                // 解析课表数据
                var courses = await ParseScheduleHtmlAsync(scheduleHtml);
                
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
                // 尝试注销登录
                if (page != null)
                {
                    try
                    {
                        _logger.LogInformation($"用户 {request.Username} 操作完成，执行自动注销");
                        await _browserManager.LogoutAsync(page);
                        _logger.LogInformation($"用户 {request.Username} 已成功注销");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"注销过程中发生错误: {ex.Message}");
                    }
                    
                    // 释放页面实例回池中
                    await _browserManager.ReleaseBrowserAsync(page);
                    _logger.LogInformation($"已释放用户 {request.Username} 的页面实例");
                }
            }
        }


        /// <summary>
        /// 获取课表HTML内容
        /// </summary>
        private async Task<string> GetScheduleHtmlAsync(IPage page, string tableType, string year, string term)
        {
            try
            {
                // 等待"我的"链接出现并确保可点击
                _logger.LogInformation("等待'我的'链接可用...");
                var myLink = page.GetByRole(AriaRole.Link, new() { Name = "我的", Exact = true });
                await myLink.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
                
                // 点击"我的"链接
                _logger.LogInformation("点击'我的'链接...");
                await myLink.ClickAsync();
                
                // 等待"我的课表"链接出现并确保可点击
                _logger.LogInformation("等待'我的课表'链接可用...");
                var scheduleLink = page.GetByRole(AriaRole.Link, new() { Name = "我的课表" });
                await scheduleLink.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
                
                // 点击"我的课表"链接
                _logger.LogInformation("点击'我的课表'链接...");
                await scheduleLink.ClickAsync();
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                
                // 主动关闭下拉菜单
                _logger.LogInformation("关闭下拉菜单...");
                var mainTop = page.Locator("#main-top");
                await mainTop.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 3000 });
                await mainTop.ClickAsync();
                
                // 等待iframe加载完成
                _logger.LogInformation("等待iframe加载完成...");
                await page.WaitForSelectorAsync("iframe[name=\"iframeMain\"]", new() { 
                    State = WaitForSelectorState.Attached, 
                    Timeout = 10000 
                });
                
                var frame = page.Frame("iframeMain");
                if (frame == null)
                {
                    _logger.LogWarning("无法找到课表iframe，请检查网页结构是否有变化");
                    return null;
                }
                
                // 等待课表类型选择器可用
                _logger.LogInformation("等待课表类型选择器可用...");
                var tableTypeSelector = frame.GetByLabel("课表类型:");
                await tableTypeSelector.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
                
                // 选择课表类型（学生课表或班级课表）
                _logger.LogInformation($"选择{(tableType == "std" ? "学生" : "班级")}课表...");
                await tableTypeSelector.SelectOptionAsync(new[] { tableType });
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                
                // 等待学年学期选择器可用
                _logger.LogInformation("等待学年学期选择器可用...");
                var yearTermSelector = frame.GetByText("学年学期");
                await yearTermSelector.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
                
                // 点击学年学期选择器
                _logger.LogInformation("点击学年学期选择器...");
                await yearTermSelector.ClickAsync();
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                
                // 选择学年
                string yearSelector = year.Split('-').Length > 1 ? 
                                     $"-{year.Split('-')[1]}" : year;
                _logger.LogInformation($"等待并选择学年: {yearSelector}...");
                var yearCell = frame.GetByRole(AriaRole.Cell, new() { Name = yearSelector });
                await yearCell.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
                await yearCell.ClickAsync();
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                
                // 选择学期
                string termName = term == "1" ? "学期1" : "学期2";
                _logger.LogInformation($"等待并选择学期: {termName}...");
                var termCell = frame.GetByRole(AriaRole.Cell, new() { Name = termName });
                await termCell.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
                await termCell.ClickAsync();
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                
                // 等待并点击"切换学期"按钮
                _logger.LogInformation("等待并点击切换学期按钮...");
                var switchTermButton = frame.GetByRole(AriaRole.Button, new() { Name = "切换学期" });
                await switchTermButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
                await switchTermButton.ClickAsync();
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

                // 确保课表内容已完全加载
                _logger.LogInformation("等待课表内容加载完成...");
                await Task.Delay(2000); // 额外等待一小段时间
                
                // 等待课表表格元素加载完成
                await frame.WaitForSelectorAsync(".gridtable, #kbtable", new() { 
                    State = WaitForSelectorState.Visible, 
                    Timeout = 10000 
                });
                
                // 获取完整的HTML内容
                var scheduleHtml = await frame.EvaluateAsync<string>("() => document.documentElement.outerHTML");
                _logger.LogInformation("成功获取课表HTML内容");
                
                return scheduleHtml;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"获取课表HTML内容时出错: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 解析课表HTML
        /// </summary>
        private async Task<List<CourseInfo>> ParseScheduleHtmlAsync(string htmlContent)
        {
            List<CourseInfo> courses = new List<CourseInfo>();
            
            try
            {
                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(htmlContent);
                
                // 设置单元格数（每天的课程节数）
                int unitCount = 10; // 默认一天10节课
                
                // 尝试提取unitCount的值
                var unitCountMatch = Regex.Match(htmlContent, @"var\s+unitCount\s*=\s*(\d+)\s*;");
                if (unitCountMatch.Success)
                {
                    unitCount = int.Parse(unitCountMatch.Groups[1].Value);
                }
                
                // 解析课程学分信息
                Dictionary<string, double> courseCredits = ParseCourseCredits(doc);
                
                // 修改：改进正则表达式，匹配整个课程活动块
                var activityPattern = @"activity\s*=\s*new\s*TaskActivity\(([^;]+)\);\s*index\s*=(\d+)\*unitCount\+(\d+);(?:\s*table0\.activities\[index\]\[table0\.activities\[index\]\.length\]=activity;)+";
                var scriptMatches = Regex.Matches(htmlContent, activityPattern);
                
                // 步骤1: 从JavaScript代码中提取课程基本信息
                foreach (Match match in scriptMatches)
                {
                    try
                    {
                        string activityParams = match.Groups[1].Value;
                        int dayIndex = int.Parse(match.Groups[2].Value);
                        int periodIndex = int.Parse(match.Groups[3].Value);
                        
                        // 解析课程活动参数
                        var paramParts = SplitParameters(activityParams);
                        if (paramParts.Count < 7) continue;
                        
                        // 获取教师名称
                        var teacherName = paramParts[1].Trim('\'', '"');
                        
                        // 解析课程代码和名称
                        string courseIdWithCode = paramParts[2].Trim('\'', '"');
                        string courseName = paramParts[3].Trim('\'', '"');
                        
                        // 提取课程代码和序号
                        var courseCodeMatch = Regex.Match(courseIdWithCode, @"(\d+)\(([^)]+)\)");
                        string courseId = courseCodeMatch.Success ? courseCodeMatch.Groups[1].Value : "0";
                        string courseNumber = courseCodeMatch.Success ? courseCodeMatch.Groups[2].Value : courseIdWithCode;
                        
                        // 提取课程代码（如tb1130014）
                        string courseCode = "";
                        if (courseNumber.Contains("."))
                        {
                            courseCode = courseNumber.Split('.')[0];
                        }
                        
                        // 处理教室信息
                        string classroomId = paramParts[4].Trim('\'', '"');
                        string classroom = paramParts[5].Trim('\'', '"');
                        
                        // 处理周次模式
                        string weekPattern = paramParts[6].Trim('\'', '"');
                        string processedWeekPattern = ProcessWeekPattern(weekPattern);
                        List<int> weeks = GetWeeksFromPattern(weekPattern);
                        
                        // 处理备注
                        string remark = "";
                        if (paramParts.Count > 9)
                        {
                            remark = paramParts[9].Trim('\'', '"');
                        }
                        
                        // 创建课程信息对象
                        var course = new CourseInfo
                        {
                            CourseId = courseId,
                            CourseCode = courseCode,
                            CourseNumber = courseNumber,
                            CourseName = ExtractCourseName(courseName),
                            TeacherName = FormatTeacherName(teacherName), // 处理教师名称
                            Classroom = classroom,
                            ClassroomId = classroomId,
                            WeekInfo = processedWeekPattern,
                            Weeks = weeks,
                            DayOfWeek = dayIndex + 1, // 转换为1-7表示
                            Remark = remark
                        };
                        
                        // 添加课程节次
                        course.Periods.Add(periodIndex);
                        
                        // 添加课程学分
                        if (courseCredits.ContainsKey(courseNumber))
                        {
                            course.Credits = courseCredits[courseNumber];
                        }
                        
                        // 添加到课程列表
                        MergeOrAddCourse(courses, course);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"解析课程活动时出错: {ex.Message}");
                    }
                }
                
                // 步骤2: 从表格中提取跨行课程信息，补充节次数据
                ProcessRowspanCourses(courses, doc, unitCount);
                
                // 步骤3: 如果没有找到任何课程，尝试备用解析方法
                if (courses.Count == 0)
                {
                    _logger.LogInformation("未从脚本中找到课程，使用备用解析方法");
                    courses = ParseCourseInfoAlternative(htmlContent, doc, unitCount, courseCredits);
                }
                
                // 步骤4: 解析教师ID
                await ParseTeacherIdsAsync(courses, htmlContent);
                
                // 步骤5: 处理连续节次并格式化
                ProcessContinuousPeriods(courses);

                // 步骤6: 最终合并与清理冗余课程
                courses = MergeDuplicateCourses(courses);
                
                _logger.LogInformation($"成功解析 {courses.Count} 门课程信息");
                return courses;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"解析课表HTML时出错: {ex.Message}");
                return courses;
            }
        }
        /// <summary>
        /// 处理连续节次并格式化
        /// </summary>
        private void ProcessContinuousPeriods(List<CourseInfo> courses)
        {
            _logger.LogInformation("开始处理连续节次...");
            
            foreach (var course in courses)
            {
                try
                {
                    // 确保节次排序
                    if (course.Periods.Count > 0)
                    {
                        course.Periods.Sort();
                        course.StartPeriod = course.Periods.First() + 1; // +1转换为1-based
                        course.EndPeriod = course.Periods.Last() + 1;   // +1转换为1-based
                        
                        // 识别连续节次分组
                        List<List<int>> periodGroups = GroupConsecutivePeriods(course.Periods);
                        
                        // 生成格式化的节次信息
                        course.FormattedPeriods = FormatPeriodGroups(periodGroups);
                        
                        _logger.LogDebug($"课程 '{course.CourseName}' ({course.DayOfWeek}): 节次={string.Join(",", course.Periods)}, 格式化为 '{course.FormattedPeriods}'");
                    }
                    else
                    {
                        course.StartPeriod = 0;
                        course.EndPeriod = 0;
                        course.FormattedPeriods = "";
                        
                        _logger.LogWarning($"课程 '{course.CourseName}' 没有节次信息");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"处理课程 '{course.CourseName}' 的连续节次时出错: {ex.Message}");
                }
            }
            
            _logger.LogInformation($"处理连续节次完成，共处理 {courses.Count} 门课程");
        }
        
        /// <summary>
        /// 将节次列表分组为连续的子序列
        /// </summary>
        /// <param name="periods">节次列表</param>
        /// <returns>连续节次分组列表</returns>
        private List<List<int>> GroupConsecutivePeriods(List<int> periods)
        {
            List<List<int>> result = new List<List<int>>();
            if (periods.Count == 0)
            {
                return result;
            }
            
            List<int> currentGroup = new List<int> { periods[0] };
            
            for (int i = 1; i < periods.Count; i++)
            {
                if (periods[i] == periods[i - 1] + 1)
                {
                    // 当前节次与前一节次连续
                    currentGroup.Add(periods[i]);
                }
                else
                {
                    // 连续性中断，创建新分组
                    result.Add(currentGroup);
                    currentGroup = new List<int> { periods[i] };
                }
            }
            
            // 添加最后一个组
            result.Add(currentGroup);
            return result;
        }
        
        /// <summary>
        /// 格式化节次分组信息
        /// </summary>
        /// <param name="periodGroups">节次分组列表</param>
        /// <returns>格式化的节次字符串</returns>
        private string FormatPeriodGroups(List<List<int>> periodGroups)
        {
            if (periodGroups.Count == 0)
                return "";
                
            List<string> formattedGroups = new List<string>();
            
            foreach (var group in periodGroups)
            {
                if (group.Count == 1)
                {
                    // 单独一节
                    formattedGroups.Add($"{group[0] + 1}");
                }
                else
                {
                    // 连续多节
                    formattedGroups.Add($"{group[0] + 1}-{group.Last() + 1}");
                }
            }
            
            // 组合所有分组，用逗号分隔
            return "第" + string.Join("、", formattedGroups) + "节";
        }
        /// <summary>
        /// 检查列表是否连续
        /// </summary>
        private bool IsSequentialList(List<int> list)
        {
            // 如果只有一个元素，不算连续
            if (list.Count <= 1) return false;
            
            for (int i = 1; i < list.Count; i++)
            {
                // 如果相邻两个元素的差值不是1，则不连续
                if (list[i] - list[i-1] != 1)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 格式化节次信息
        /// </summary>
        private string FormatPeriods(List<int> periods, bool isContinuous)
        {
            if (periods.Count == 0) return "";
            if (periods.Count == 1) return $"第{periods[0]+1}节"; // +1是因为内部存储从0开始
            
            if (isContinuous)
            {
                // 连续节次表示为范围
                return $"第{periods.First()+1}-{periods.Last()+1}节";
            }
            else
            {
                // 不连续节次列出所有节次
                return "第" + string.Join(", ", periods.Select(p => (p+1).ToString())) + "节";
            }
        }

        /// <summary>
        /// 格式化教师名称，处理特殊情况
        /// </summary>
        private string FormatTeacherName(string rawName)
        {
            if (string.IsNullOrEmpty(rawName))
                return string.Empty;
                
            // 处理JavaScript变量引用
            if (rawName.Contains("actTeacherName") || rawName.Contains("join") || 
                rawName.Contains("teachers") || rawName.Contains("TeacherName"))
                return string.Empty;
            
            // 移除HTML标签    
            rawName = Regex.Replace(rawName, @"<[^>]+>", "");
            
            // 处理HTML实体
            rawName = rawName.Replace("&nbsp;", " ")
                             .Replace("&amp;", "&")
                             .Replace("&lt;", "<")
                             .Replace("&gt;", ">");
                
            // 处理多教师情况，只保留第一个
            if (rawName.Contains(",") || rawName.Contains("、") || rawName.Contains(";"))
            {
                string[] names = rawName.Split(new[] { ',', '、', ';' }, StringSplitOptions.RemoveEmptyEntries);
                if (names.Length > 0)
                    return names[0].Trim();
            }
            
            // 移除特殊字符和括号内容
            rawName = Regex.Replace(rawName, @"\([^\)]*\)", "");
            rawName = Regex.Replace(rawName, @"[\[\]\(\)\{\}\<\>\*]", "");
            
            return rawName.Trim();
        }


        /// <summary>
        /// 解析教师ID
        /// </summary>
        private async Task ParseTeacherIdsAsync(List<CourseInfo> courses, string htmlContent)
        {
            try
            {
                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(htmlContent);
                
                // 1. 从课程表表格提取教师与课程对应关系
                var courseToCourseInfoMap = ExtractTeacherCourseFromTable(doc);
                
                // 2. 从JS代码中提取教师ID映射
                var teacherIdMap = ExtractTeacherIds(htmlContent);
                
                // 3. 合并两处信息提高匹配率
                int matchCount = 0;
                
                foreach (var course in courses)
                {
                    bool matched = false;
                    
                    // 首先尝试通过课程序号匹配表格中的信息
                    if (!string.IsNullOrEmpty(course.CourseNumber) && 
                        courseToCourseInfoMap.TryGetValue(course.CourseNumber, out var tableInfo))
                    {
                        // 使用表格中提取的教师名称(通常更可靠)
                        if (!string.IsNullOrEmpty(tableInfo.TeacherName))
                        {
                            course.TeacherName = tableInfo.TeacherName;
                            
                            // 尝试查找此教师的ID
                            if (teacherIdMap.TryGetValue(tableInfo.TeacherName, out int id))
                            {
                                course.TeacherId = id;
                                matched = true;
                                _logger.LogDebug($"通过课程序号+表格教师名匹配: {course.CourseName}, 教师={course.TeacherName}, ID={id}");
                            }
                        }
                    }
                    
                    // 如果不成功，直接尝试用课程中的教师名匹配
                    if (!matched && !string.IsNullOrEmpty(course.TeacherName))
                    {
                        if (teacherIdMap.TryGetValue(course.TeacherName, out int id))
                        {
                            course.TeacherId = id;
                            matched = true;
                            _logger.LogDebug($"直接匹配教师名: {course.TeacherName}, ID={id}");
                        }
                        else
                        {
                            // 尝试按逗号分割的第一个教师名
                            var firstTeacherName = course.TeacherName.Split(new[] {',', '、', ';'}, StringSplitOptions.RemoveEmptyEntries)
                                                         .FirstOrDefault()?.Trim();
                            
                            if (!string.IsNullOrEmpty(firstTeacherName) && 
                                teacherIdMap.TryGetValue(firstTeacherName, out int firstId))
                            {
                                course.TeacherId = firstId;
                                matched = true;
                                _logger.LogDebug($"匹配第一个教师名: {firstTeacherName}, ID={firstId}");
                            }
                            else
                            {
                                // 最后尝试提取纯中文名
                                var chineseName = new string(course.TeacherName.Where(c => c >= 0x4e00 && c <= 0x9fa5).ToArray());
                                if (!string.IsNullOrEmpty(chineseName) && teacherIdMap.TryGetValue(chineseName, out int chineseId))
                                {
                                    course.TeacherId = chineseId;
                                    matched = true;
                                    _logger.LogDebug($"匹配纯中文名: {chineseName}, ID={chineseId}");
                                }
                            }
                        }
                    }
                    
                    if (matched) matchCount++;
                }
                
                _logger.LogInformation($"教师ID匹配完成: 共{courses.Count}门课程，成功匹配{matchCount}门");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"解析教师ID时出错: {ex.Message}");
            }
        }
        /// <summary>
        /// 从HTML/JS中提取教师ID和名称映射
        /// </summary>
        private Dictionary<string, int> ExtractTeacherIds(string html)
        {
            var teacherMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            
            try
            {
                // 1. 直接匹配 var teachers = [{id:2903,name:"录睿琪",lab:false}]; 格式
                var directPattern = @"var\s+teachers\s*=\s*\[\s*\{id:(\d+),name:""([^""]+)"",lab:(false|true)\}\s*\];";
                var directMatches = Regex.Matches(html, directPattern);
                
                foreach (Match match in directMatches)
                {
                    if (match.Success && match.Groups.Count >= 3)
                    {
                        int id = int.Parse(match.Groups[1].Value);
                        string name = match.Groups[2].Value;
                        
                        if (!teacherMap.ContainsKey(name))
                        {
                            teacherMap[name] = id;
                            _logger.LogDebug($"直接从teachers变量匹配到教师: {name} (ID: {id})");
                        }
                    }
                }
                
                // 2. 匹配任何包含id和中文名的对象定义
                if (teacherMap.Count == 0)
                {
                    var generalPattern = @"id:(\d+),name:""([^""]+)""";
                    var generalMatches = Regex.Matches(html, generalPattern);
                    
                    foreach (Match match in generalMatches)
                    {
                        if (match.Success && match.Groups.Count >= 3)
                        {
                            int id = int.Parse(match.Groups[1].Value);
                            string name = match.Groups[2].Value;
                            
                            // 只接受包含中文字符的名称
                            if (Regex.IsMatch(name, @"[\u4e00-\u9fa5]") && !teacherMap.ContainsKey(name))
                            {
                                teacherMap[name] = id;
                                _logger.LogDebug($"正则匹配到教师: {name} (ID: {id})");
                            }
                        }
                    }
                }
                
                // 3. 查找所有教师数组定义
                if (teacherMap.Count < 5)
                {
                    var arrayPatterns = new[]
                    {
                        @"var\s+actTeachers\s*=\s*\[(.*?)\];",
                        @"var\s+allTeachers\s*=\s*\[(.*?)\];",
                        @"var\s+teachers\s*=\s*\[(.*?)\];"
                    };
                    
                    foreach (var pattern in arrayPatterns)
                    {
                        var matches = Regex.Matches(html, pattern, RegexOptions.Singleline);
                        foreach (Match match in matches)
                        {
                            if (match.Success && match.Groups.Count > 1)
                            {
                                string content = match.Groups[1].Value;
                                var teacherItems = Regex.Matches(content, @"\{id:(\d+),name:""([^""]+)"",lab:(false|true)\}");
                                
                                foreach (Match item in teacherItems)
                                {
                                    if (item.Success && item.Groups.Count >= 3)
                                    {
                                        int id = int.Parse(item.Groups[1].Value);
                                        string name = item.Groups[2].Value;
                                        
                                        if (Regex.IsMatch(name, @"[\u4e00-\u9fa5]") && !teacherMap.ContainsKey(name))
                                        {
                                            teacherMap[name] = id;
                                            _logger.LogDebug($"从数组提取教师: {name} (ID: {id})");
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"提取教师ID时出错: {ex.Message}");
            }
            
            _logger.LogInformation($"共找到 {teacherMap.Count} 个教师ID映射");
            return teacherMap;
        }
        /// <summary>
        /// 解析课程学分信息
        /// </summary>
        private Dictionary<string, double> ParseCourseCredits(HtmlDocument doc)
        {
            var result = new Dictionary<string, double>();
            
            // 直接查找所有gridtable类的表格
            var tables = doc.DocumentNode.SelectNodes("//table[contains(@class, 'gridtable')]");
            
            if (tables != null)
            {
                foreach (var table in tables)
                {
                    // 检查是否为课程列表表格
                    var headers = table.SelectNodes(".//th");
                    if (headers == null)
                    {
                        continue;
                    }
                    
                    // 检查表头是否包含所需列
                    bool hasCourseCode = false;
                    bool hasCredits = false;
                    bool hasCourseNumber = false;
                    
                    int courseCodeColIndex = -1;
                    int creditsColIndex = -1;
                    int courseNumberColIndex = -1;
                    int courseNameColIndex = -1;
                    
                    for (int i = 0; i < headers.Count; i++)
                    {
                        string headerText = headers[i].InnerText.Trim();
                        
                        if (headerText.Contains("课程代码"))
                        {
                            courseCodeColIndex = i;
                            hasCourseCode = true;
                        }
                        if (headerText.Contains("学分"))
                        {
                            creditsColIndex = i;
                            hasCredits = true;
                        }
                        if (headerText.Contains("课程序号"))
                        {
                            courseNumberColIndex = i;
                            hasCourseNumber = true;
                        }
                        if (headerText.Contains("课程名称"))
                        {
                            courseNameColIndex = i;
                        }
                    }
                    
                    // 如果表格包含课程代码、学分和课程序号列，则认为是课程表格
                    if (hasCourseCode && hasCredits && (hasCourseNumber || courseNameColIndex >= 0))
                    {
                        // 处理表格行
                        var rows = table.SelectNodes(".//tr[td]");
                        if (rows != null)
                        {
                            foreach (var row in rows)
                            {
                                var cells = row.SelectNodes("./td");
                                if (cells != null && cells.Count > Math.Max(courseNumberColIndex, Math.Max(creditsColIndex, courseCodeColIndex)))
                                {
                                    try
                                    {
                                        // 提取课程序号
                                        string courseNumber = "";
                                        if (courseNumberColIndex >= 0)
                                        {
                                            var courseNumberCell = cells[courseNumberColIndex];
                                            var courseNumberMatch = Regex.Match(courseNumberCell.InnerText.Trim(), @"([a-z]+\d+\.\d+)");
                                            if (courseNumberMatch.Success)
                                            {
                                                courseNumber = courseNumberMatch.Groups[1].Value;
                                            }
                                            else
                                            {
                                                var link = courseNumberCell.SelectSingleNode(".//a");
                                                if (link != null)
                                                {
                                                    courseNumber = link.InnerText.Trim();
                                                }
                                                else
                                                {
                                                    courseNumber = courseNumberCell.InnerText.Trim();
                                                }
                                            }
                                        }
                                        
                                        // 提取学分
                                        if (!string.IsNullOrEmpty(courseNumber) && creditsColIndex >= 0)
                                        {
                                            var creditsCell = cells[creditsColIndex];
                                            string creditsText = creditsCell.InnerText.Trim();
                                            
                                            // 尝试解析学分值
                                            if (double.TryParse(creditsText, out double credits))
                                            {
                                                result[courseNumber] = credits;
                                                _logger.LogDebug($"提取到课程 {courseNumber} 的学分: {credits}");
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogWarning($"解析学分时出错: {ex.Message}");
                                    }
                                }
                            }
                        }
                    }
                }
            }
            
            // 如果结果为空，尝试使用正则表达式从JavaScript代码中提取课程信息
            if (result.Count == 0)
            {
                try
                {
                    string htmlContent = doc.DocumentNode.OuterHtml;
                    
                    // 在JavaScript代码中查找课程信息模式
                    var courseInfoMatches = Regex.Matches(htmlContent, 
                        @"<td[^>]*>([^<]*)</td>\s*<td[^>]*>([^<]*)</td>\s*<td[^>]*>([^<]*)</td>\s*<td[^>]*>(\d+(?:\.\d+)?)</td>\s*<td[^>]*>([^<]*)</td>");
                    
                    foreach (Match match in courseInfoMatches)
                    {
                        try
                        {
                            string courseCode = match.Groups[2].Value.Trim();
                            string credits = match.Groups[4].Value.Trim();
                            string courseNumber = match.Groups[5].Value.Trim();
                            
                            // 提取课程序号 (如 tb1130014.18)
                            var courseNumberMatch = Regex.Match(courseNumber, @"([a-z]+\d+\.\d+)");
                            if (courseNumberMatch.Success)
                            {
                                courseNumber = courseNumberMatch.Groups[1].Value;
                                
                                if (double.TryParse(credits, out double creditsValue))
                                {
                                    result[courseNumber] = creditsValue;
                                    _logger.LogDebug($"从JS代码提取到课程 {courseNumber} 的学分: {creditsValue}");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning($"从JS提取学分时出错: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"使用正则提取学分信息时出错: {ex.Message}");
                }
            }
            
            _logger.LogInformation($"共提取到 {result.Count} 条课程学分信息");
            return result;
        }

        /// <summary>
        /// 从课程表表格提取教师和课程关联信息
        /// </summary>
        private Dictionary<string, TeacherCourseInfo> ExtractTeacherCourseFromTable(HtmlDocument doc)
        {
            var result = new Dictionary<string, TeacherCourseInfo>();
            
            try
            {
                // 查找所有课程表格(带有随机ID的gridtable)
                var tables = doc.DocumentNode.SelectNodes("//table[contains(@class, 'gridtable')]");
                if (tables == null) return result;
                
                foreach (var table in tables)
                {
                    // 获取表头确定列索引
                    var headers = table.SelectNodes(".//thead//th");
                    if (headers == null) continue;
                    
                    int teacherColIndex = -1;
                    int courseNumberColIndex = -1;
                    int courseNameColIndex = -1;
                    
                    for (int i = 0; i < headers.Count; i++)
                    {
                        string headerText = headers[i].InnerText.Trim();
                        if (headerText.Contains("教师"))
                            teacherColIndex = i;
                        if (headerText.Contains("课程序号"))
                            courseNumberColIndex = i;
                        if (headerText.Contains("课程名称"))
                            courseNameColIndex = i;
                    }
                    
                    // 确保找到必要的列
                    if (teacherColIndex >= 0 && courseNumberColIndex >= 0)
                    {
                        // 处理表格数据行
                        var rows = table.SelectNodes(".//tbody/tr");
                        if (rows != null)
                        {
                            foreach (var row in rows)
                            {
                                var cells = row.SelectNodes("./td");
                                if (cells == null || cells.Count <= Math.Max(teacherColIndex, courseNumberColIndex)) 
                                    continue;
                                
                                // 提取教师名称
                                string teacherName = cells[teacherColIndex].InnerText.Trim();
                                
                                // 提取课程序号和lessonId
                                var courseCell = cells[courseNumberColIndex];
                                var courseLink = courseCell.SelectSingleNode(".//a");
                                
                                if (courseLink != null)
                                {
                                    string href = courseLink.GetAttributeValue("href", "");
                                    string courseNumber = courseLink.InnerText.Trim();
                                    
                                    // 从链接中提取lessonId
                                    var match = Regex.Match(href, @"lesson\.id=(\d+)");
                                    if (match.Success && !string.IsNullOrEmpty(courseNumber))
                                    {
                                        string lessonId = match.Groups[1].Value;
                                        
                                        // 提取课程名称(如果有)
                                        string courseName = "";
                                        if (courseNameColIndex >= 0 && cells.Count > courseNameColIndex)
                                            courseName = cells[courseNameColIndex].InnerText.Trim();
                                        
                                        // 保存信息
                                        result[courseNumber] = new TeacherCourseInfo
                                        {
                                            CourseNumber = courseNumber,
                                            TeacherName = teacherName,
                                            LessonId = lessonId,
                                            CourseName = courseName
                                        };
                                        
                                        _logger.LogDebug($"从表格提取: 课程={courseNumber}, 教师={teacherName}, LessonId={lessonId}");
                                    }
                                }
                            }
                        }
                    }
                }
                
                _logger.LogInformation($"从表格提取了 {result.Count} 条教师-课程对应关系");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"从表格提取教师信息出错: {ex.Message}");
            }
            
            return result;
        }
        
        // 支持类
        private class TeacherCourseInfo
        {
            public string CourseNumber { get; set; }
            public string TeacherName { get; set; }
            public string LessonId { get; set; }
            public string CourseName { get; set; }
            public int? TeacherId { get; set; }
        }

        /// <summary>
        /// 提取课程名称（去除序号部分）
        /// </summary>
        private string ExtractCourseName(string fullName)
        {
            // 检查输入是否为空
            if (string.IsNullOrEmpty(fullName))
                return string.Empty;
                
            // 提取括号前的课程名称 - 使用贪婪匹配确保获取全部名称
            var match = Regex.Match(fullName, @"(.*)\(");
            if (match.Success && match.Groups.Count > 1)
            {
                return match.Groups[1].Value.Trim();
            }
            
            // 如果没有括号，返回原始名称
            return fullName;
        }

        /// <summary>
        /// 处理周次模式字符串
        /// </summary>
        private string ProcessWeekPattern(string weekPattern)
        {
            // 反转字符串并移除前导零
            string reversed = new string(weekPattern.Reverse().ToArray());
            return reversed.TrimStart('0');
        }

        /// <summary>
        /// 从周次模式中获取上课周次列表
        /// </summary>
        private List<int> GetWeeksFromPattern(string weekPattern)
        {
            List<int> weeks = new List<int>();
            
            for (int i = 1; i < weekPattern.Length; i++) // 从1开始，因为第二个字符代表第一周
            {
                if (weekPattern[i] == '1')
                {
                    weeks.Add(i); // 周次从1开始
                }
            }
            
            return weeks;
        }

        /// <summary>
        /// 分割JavaScript函数参数
        /// </summary>
        private List<string> SplitParameters(string parameters)
        {
            List<string> result = new List<string>();
            StringBuilder currentParam = new StringBuilder();
            bool inString = false;
            char stringDelimiter = '\0';
            int nestedParentheses = 0;
            
            for (int i = 0; i < parameters.Length; i++)
            {
                char c = parameters[i];
                
                // 处理字符串
                if ((c == '\'' || c == '"') && (i == 0 || parameters[i - 1] != '\\'))
                {
                    if (!inString)
                    {
                        inString = true;
                        stringDelimiter = c;
                    }
                    else if (c == stringDelimiter)
                    {
                        inString = false;
                    }
                    currentParam.Append(c);
                }
                // 处理嵌套括号
                else if (c == '(' && !inString)
                {
                    nestedParentheses++;
                    currentParam.Append(c);
                }
                else if (c == ')' && !inString)
                {
                    nestedParentheses--;
                    currentParam.Append(c);
                }
                // 处理参数分隔符(逗号)
                else if (c == ',' && !inString && nestedParentheses == 0)
                {
                    result.Add(currentParam.ToString().Trim());
                    currentParam.Clear();
                }
                else
                {
                    currentParam.Append(c);
                }
            }
            
            if (currentParam.Length > 0)
            {
                result.Add(currentParam.ToString().Trim());
            }
            
            return result;
        }

        /// <summary>
        /// 合并或添加课程，增强跨行处理和数据补充
        /// </summary>
        private void MergeOrAddCourse(List<CourseInfo> courses, CourseInfo newCourse)
        {
            // 改进匹配逻辑
            var existingCourse = courses.FirstOrDefault(c => 
                c.DayOfWeek == newCourse.DayOfWeek && // 相同星期
                (
                    // 条件1: 课程名称完全相同
                    c.CourseName == newCourse.CourseName ||
                    
                    // 条件2: 一方是另一方的前缀(处理简写情况)
                    (c.CourseName.Length > 0 && newCourse.CourseName.Length > 0 &&
                     (c.CourseName.StartsWith(newCourse.CourseName) || 
                      newCourse.CourseName.StartsWith(c.CourseName)))
                ) &&
                (
                    // 条件3: 至少一方教师名为空，或者教师名相同
                    string.IsNullOrEmpty(c.TeacherName) || 
                    string.IsNullOrEmpty(newCourse.TeacherName) || 
                    c.TeacherName == newCourse.TeacherName
                ) &&
                (
                    // 条件4: 节次重叠或至少一方没有节次信息
                    c.Periods.Count == 0 || 
                    newCourse.Periods.Count == 0 ||
                    c.Periods.Any(p => newCourse.Periods.Contains(p))
                ));
            
            if (existingCourse != null)
            {
                // 合并课程节次
                foreach (var period in newCourse.Periods)
                {
                    if (!existingCourse.Periods.Contains(period))
                    {
                        existingCourse.Periods.Add(period);
                    }
                }
                
                // 补充缺失的元数据
                if ((string.IsNullOrEmpty(existingCourse.CourseId) || existingCourse.CourseId == "0") && 
                    !string.IsNullOrEmpty(newCourse.CourseId) && newCourse.CourseId != "0")
                    existingCourse.CourseId = newCourse.CourseId;
                    
                if (string.IsNullOrEmpty(existingCourse.CourseCode) && !string.IsNullOrEmpty(newCourse.CourseCode))
                    existingCourse.CourseCode = newCourse.CourseCode;
                    
                if (string.IsNullOrEmpty(existingCourse.CourseNumber) && !string.IsNullOrEmpty(newCourse.CourseNumber))
                    existingCourse.CourseNumber = newCourse.CourseNumber;
                    
                if (string.IsNullOrEmpty(existingCourse.Classroom) && !string.IsNullOrEmpty(newCourse.Classroom))
                    existingCourse.Classroom = newCourse.Classroom;
                    
                if (existingCourse.Credits == 0 && newCourse.Credits > 0)
                    existingCourse.Credits = newCourse.Credits;
                
                // 课程名称，保留更长的版本
                if (newCourse.CourseName.Length > existingCourse.CourseName.Length)
                    existingCourse.CourseName = newCourse.CourseName;
                    
                // 排序课程节次
                existingCourse.Periods.Sort();
                _logger.LogDebug($"合并课程: {existingCourse.CourseName}, 节次数量: {existingCourse.Periods.Count}");
            }
            else
            {
                // 添加新课程
                courses.Add(newCourse);
                _logger.LogDebug($"添加新课程: {newCourse.CourseName}, 节次数量: {newCourse.Periods.Count}");
            }
        }
        
        /// <summary>
        /// 最终合并清理，识别并合并冗余的课程信息
        /// </summary>
        private List<CourseInfo> MergeDuplicateCourses(List<CourseInfo> courses)
        {
            _logger.LogInformation("开始最终合并处理，删除重复课程...");
            
            var result = new List<CourseInfo>();
            var processedCourses = new HashSet<string>(); // 跟踪已处理的课程唯一标识
            
            // 确保有完整信息的课程优先处理
            var orderedCourses = courses
                .OrderByDescending(c => !string.IsNullOrEmpty(c.CourseId) && c.CourseId != "0") // 有课程ID的优先
                .ThenByDescending(c => !string.IsNullOrEmpty(c.CourseNumber)) // 有课程编号的优先
                .ThenByDescending(c => c.Periods.Count) // 节次信息越完整越优先
                .ThenByDescending(c => c.Credits > 0) // 有学分信息的优先
                .ToList();
            
            foreach (var course in orderedCourses)
            {
                // 生成课程唯一标识（日期+名称+节次范围）
                string courseKey = $"{course.DayOfWeek}_{course.CourseName}_{(course.Periods.Count > 0 ? course.Periods.Min() : 0)}";
                
                // 如果是截断的课程名（只有一个字），则跳过
                if (course.CourseName.Length <= 1)
                {
                    _logger.LogDebug($"跳过截断课程名: {course.CourseName}");
                    continue;
                }
                
                // 如果已处理过这个课程，跳过
                if (processedCourses.Contains(courseKey))
                {
                    _logger.LogDebug($"跳过重复课程: {courseKey}");
                    continue;
                }
                
                // 查找可能是同一课程的其他记录
                var duplicates = orderedCourses.Where(c => 
                    c != course && // 不是自己
                    c.DayOfWeek == course.DayOfWeek && // 相同星期
                    (c.CourseName == course.CourseName || // 完全相同名称
                     (c.CourseName.Length <= 2 && course.CourseName.StartsWith(c.CourseName)) || // 短名称是长名称的前缀
                     (course.CourseName.Length <= 2 && c.CourseName.StartsWith(course.CourseName))) && // 或相反
                    HaveOverlappingPeriods(c.Periods, course.Periods) // 有重叠节次
                ).ToList();
                
                // 如果找到重复记录，合并信息
                foreach (var duplicate in duplicates)
                {
                    MergeCoursesData(course, duplicate);
                }
                
                // 添加到结果集并标记为已处理
                result.Add(course);
                processedCourses.Add(courseKey);
            }
            
            _logger.LogInformation($"最终合并完成，从 {courses.Count} 减少到 {result.Count} 门课程");
            return result;
        }
        
        /// <summary>
        /// 检查两组节次是否有重叠
        /// </summary>
        private bool HaveOverlappingPeriods(List<int> periods1, List<int> periods2)
        {
            // 任一为空则检查另一方是否为空
            if (periods1.Count == 0 || periods2.Count == 0)
                return true; // 允许空节次合并
                
            // 检查是否有任何重叠节次
            return periods1.Any(p => periods2.Contains(p));
        }
        
        /// <summary>
        /// 合并两个课程的信息到主课程
        /// </summary>
        private void MergeCoursesData(CourseInfo main, CourseInfo other)
        {
            // 合并基本属性，用非空值替换空值
            if (string.IsNullOrEmpty(main.CourseId) || main.CourseId == "0")
                main.CourseId = other.CourseId;
                
            if (string.IsNullOrEmpty(main.CourseCode))
                main.CourseCode = other.CourseCode;
                
            if (string.IsNullOrEmpty(main.CourseNumber))
                main.CourseNumber = other.CourseNumber;
                
            if (string.IsNullOrEmpty(main.TeacherName))
                main.TeacherName = other.TeacherName;
                
            if (main.TeacherId == null)
                main.TeacherId = other.TeacherId;
                
            if (string.IsNullOrEmpty(main.Classroom))
                main.Classroom = other.Classroom;
                
            if (string.IsNullOrEmpty(main.ClassroomId))
                main.ClassroomId = other.ClassroomId;
                
            if (main.Credits == 0 && other.Credits > 0)
                main.Credits = other.Credits;
                
            if (string.IsNullOrEmpty(main.Remark) && !string.IsNullOrEmpty(other.Remark))
                main.Remark = other.Remark;
                
            // 合并周次信息
            if (main.Weeks.Count == 0 && other.Weeks.Count > 0)
            {
                main.Weeks = new List<int>(other.Weeks);
                main.WeekInfo = other.WeekInfo;
            }
            
            // 合并节次信息，确保不重复
            foreach (var period in other.Periods)
            {
                if (!main.Periods.Contains(period))
                    main.Periods.Add(period);
            }
            
            // 确保节次排序
            if (main.Periods.Count > 0)
                main.Periods.Sort();
        }
        
        
        /// <summary>
        /// 备用解析方法，直接从HTML表格中提取课程信息
        /// </summary>
        private List<CourseInfo> ParseCourseInfoAlternative(string htmlContent, HtmlDocument doc, int unitCount, Dictionary<string, double> courseCredits)
        {
            List<CourseInfo> courses = new List<CourseInfo>();
            
            try
            {
                // 尝试直接从JavaScript代码中提取课程活动
                var activityBlocks = Regex.Matches(htmlContent, @"var\s+teachers\s*=\s*\[.*\];\s*var\s+actTeachers\s*=\s*\[.*\];.*activity\s*=\s*new\s*TaskActivity\(([^;]+)\);\s*index\s*=(\d+)\*unitCount\+(\d+);", RegexOptions.Singleline);
                
                _logger.LogInformation($"备用解析方法找到 {activityBlocks.Count} 个课程活动");
                
                foreach (Match block in activityBlocks)
                {
                    try
                    {
                        string activityParams = block.Groups[1].Value;
                        int dayIndex = int.Parse(block.Groups[2].Value);
                        int periodIndex = int.Parse(block.Groups[3].Value);
                        
                        // 首先解析课程活动参数
                        var paramParts = SplitParameters(activityParams);
                        if (paramParts.Count < 7) continue;
                        
                        // 处理教师信息
                        string teacherName = paramParts[1].Trim('\'', '"');
                        
                        // 解析课程代码和名称
                        string courseIdWithCode = paramParts[2].Trim('\'', '"');
                        string courseName = paramParts[3].Trim('\'', '"');
                        
                        // 提取课程代码和序号
                        var courseCodeMatch = Regex.Match(courseIdWithCode, @"(\d+)\(([^)]+)\)");
                        string courseId = courseCodeMatch.Success ? courseCodeMatch.Groups[1].Value : "0";
                        string courseNumber = courseCodeMatch.Success ? courseCodeMatch.Groups[2].Value : courseIdWithCode;
                        
                        // 提取课程代码（如tb1130014）
                        string courseCode = "";
                        if (courseNumber.Contains("."))
                        {
                            courseCode = courseNumber.Split('.')[0];
                        }
                        
                        // 处理教室信息
                        string classroomId = paramParts[4].Trim('\'', '"');
                        string classroom = paramParts[5].Trim('\'', '"');
                        
                        // 处理周次模式
                        string weekPattern = paramParts[6].Trim('\'', '"');
                        string processedWeekPattern = ProcessWeekPattern(weekPattern);
                        List<int> weeks = GetWeeksFromPattern(weekPattern);
                        
                        // 处理备注
                        string remark = "";
                        if (paramParts.Count > 9)
                        {
                            remark = paramParts[9].Trim('\'', '"');
                        }
                        
                        // 创建课程信息对象
                        var course = new CourseInfo
                        {
                            CourseId = courseId,
                            CourseCode = courseCode,
                            CourseNumber = courseNumber,
                            CourseName = ExtractCourseName(courseName),
                            TeacherName = teacherName,
                            Classroom = classroom,
                            ClassroomId = classroomId,
                            WeekInfo = processedWeekPattern,
                            Weeks = weeks,
                            DayOfWeek = dayIndex + 1, // 转换为1-7表示
                            Remark = remark
                        };
                        
                        // 添加课程节次
                        course.Periods.Add(periodIndex);
                        
                        // 添加课程学分
                        if (courseCredits.ContainsKey(courseNumber))
                        {
                            course.Credits = courseCredits[courseNumber];
                        }
                        
                        // 尝试合并相同课程的不同节次
                        MergeOrAddCourse(courses, course);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"备用解析方法处理课程活动时出错: {ex.Message}");
                    }
                }
                
                // 如果还是没有找到课程，尝试从表格中直接解析
                if (courses.Count == 0)
                {
                    _logger.LogInformation("尝试从课表表格直接解析课程信息");
                    
                    // 查找课表表格
                    var tables = doc.DocumentNode.SelectNodes("//table[contains(@class, 'gridtable') or contains(@id, 'kbtable')]");
                    if (tables != null)
                    {
                        foreach (var table in tables)
                        {
                            // 处理表格中的单元格信息，提取课程数据
                            var cells = table.SelectNodes(".//td[contains(@style, 'background-color')]");
                            if (cells != null)
                            {
                                foreach (var cell in cells)
                                {
                                    try
                                    {
                                        // 尝试从单元格ID解析位置信息
                                        var id = cell.GetAttributeValue("id", "");
                                        if (!string.IsNullOrEmpty(id) && id.StartsWith("TD"))
                                        {
                                            var idParts = id.Substring(2).Split('_');
                                            if (idParts.Length == 2 && 
                                                int.TryParse(idParts[0], out int index) && 
                                                int.TryParse(idParts[1], out int tableIndex))
                                            {
                                                // 解析位置：节次和星期
                                                int period = index % unitCount;
                                                int dayOfWeek = index / unitCount + 1; // 转为1-7表示
                                                
                                                // 提取课程信息
                                                var courseText = cell.InnerText.Trim();
                                                if (!string.IsNullOrEmpty(courseText) && courseText != "&nbsp;")
                                                {
                                                    // 使用正则表达式提取课程名称、教师、教室等信息
                                                    var courseMatch = Regex.Match(courseText, 
                                                        @"(.+?)\s*(?:\[(.+?)\])?\s*(?:<br\s*/?>\s*(.+?))?");
                                                    
                                                    if (courseMatch.Success)
                                                    {
                                                        string courseName = courseMatch.Groups[1].Value.Trim();
                                                        string teacherName = courseMatch.Groups[2].Success ? 
                                                                           courseMatch.Groups[2].Value.Trim() : "";
                                                        string location = courseMatch.Groups[3].Success ? 
                                                                        courseMatch.Groups[3].Value.Trim() : "";
                                                        
                                                        // 创建课程对象
                                                        var course = new CourseInfo
                                                        {
                                                            CourseId = "0", // 无法从表格直接获取
                                                            CourseCode = "", // 无法从表格直接获取
                                                            CourseNumber = "", // 无法从表格直接获取
                                                            CourseName = courseName,
                                                            TeacherName = teacherName,
                                                            Classroom = location,
                                                            DayOfWeek = dayOfWeek,
                                                            WeekInfo = "1" // 无法从表格直接获取周次信息，默认为1
                                                        };
                                                        
                                                        // 添加课程节次
                                                        course.Periods.Add(period);
                                                        
                                                        // 合并或添加课程
                                                        MergeOrAddCourse(courses, course);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogWarning($"解析表格单元格信息时出错: {ex.Message}");
                                    }
                                }
                            }
                        }
                    }
                }
                
                _logger.LogInformation($"备用解析方法共解析出 {courses.Count} 门课程");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "备用解析方法出现错误");
            }
            
            return courses;
        }

        

        /// <summary>
        /// 解析表格中的跨行课程信息
        /// </summary>
        private void ProcessRowspanCourses(List<CourseInfo> courses, HtmlDocument doc, int unitCount)
        {
            try
            {
                _logger.LogInformation("开始处理跨行课程信息...");
                
                // 查找课表表格
                var tables = doc.DocumentNode.SelectNodes("//table[contains(@class, 'gridtable') or contains(@id, 'kbtable')]");
                if (tables == null)
                {
                    _logger.LogWarning("未找到课表表格");
                    return;
                }
                
                foreach (var table in tables)
                {
                    // 查找所有带有rowspan属性的单元格
                    var rowspanCells = table.SelectNodes(".//td[@rowspan]");
                    if (rowspanCells == null)
                    {
                        _logger.LogInformation("未找到跨行单元格");
                        continue;
                    }
                    
                    _logger.LogInformation($"找到 {rowspanCells.Count} 个跨行单元格");
                    
                    foreach (var cell in rowspanCells)
                    {
                        try
                        {
                            // 获取单元格的位置信息
                            string id = cell.GetAttributeValue("id", "");
                            if (string.IsNullOrEmpty(id) || !id.StartsWith("TD")) continue;
                            
                            // 解析单元格的ID来获取位置信息
                            var idParts = id.Substring(2).Split('_');
                            if (idParts.Length != 2) continue;
                            
                            if (int.TryParse(idParts[0], out int index) && 
                                int.TryParse(idParts[1], out int tableIndex))
                            {
                                // 解析位置：节次和星期
                                int period = index % unitCount;
                                int dayOfWeek = index / unitCount + 1; // 转为1-7表示
                                
                                // 获取rowspan值，表示连续的节数
                                int rowspan = int.Parse(cell.GetAttributeValue("rowspan", "1"));
                                
                                // 提取课程信息
                                var courseText = cell.InnerText.Trim();
                                if (string.IsNullOrEmpty(courseText) || courseText == "&nbsp;") continue;
                                
                                // 使用正则表达式提取课程名称、教师、教室等信息
                                var courseMatch = Regex.Match(courseText, 
                                    @"(.+?)(?:\s*\[(.+?)\])?\s*(?:<br\s*/?>\s*(.+?))?");
                                
                                if (courseMatch.Success)
                                {
                                    string courseName = courseMatch.Groups[1].Value.Trim();
                                    string teacherName = courseMatch.Groups[2].Success ? 
                                                       courseMatch.Groups[2].Value.Trim() : "";
                                    string location = courseMatch.Groups[3].Success ? 
                                                    courseMatch.Groups[3].Value.Trim() : "";
                                    
                                    // 创建课程对象
                                    var course = new CourseInfo
                                    {
                                        CourseId = "0", // 从表格直接解析时无法获取
                                        CourseName = courseName,
                                        TeacherName = teacherName,
                                        Classroom = location,
                                        DayOfWeek = dayOfWeek,
                                        WeekInfo = "1" // 从表格直接解析时无法获取周次信息，默认为1
                                    };
                                    
                                    // 添加连续的节次
                                    for (int i = 0; i < rowspan; i++)
                                    {
                                        course.Periods.Add(period + i);
                                    }
                                    
                                    // 添加或合并到课程列表
                                    MergeOrAddCourse(courses, course);
                                    _logger.LogDebug($"处理跨行课程: {courseName}, 在星期{dayOfWeek}，第{period+1}节开始，共{rowspan}节");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning($"处理跨行单元格出错: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理跨行课程时发生错误");
            }
        }
    }
}