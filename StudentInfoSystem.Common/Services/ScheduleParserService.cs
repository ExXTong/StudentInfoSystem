using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using StudentInfoSystem.Common.Models;

namespace StudentInfoSystem.Common.Services
{
    public static class ScheduleParserService
    {
        /// <summary>
        /// 从HTML内容中解析课表数据
        /// </summary>
        /// <param name="htmlContent">HTML内容</param>
        /// <returns>解析后的课表信息</returns>
        public static ScheduleInfo ParseSchedule(string htmlContent)
        {
            var scheduleInfo = new ScheduleInfo();
            var doc = new HtmlDocument();
            doc.LoadHtml(htmlContent);
            
            // 解析学年学期信息
            var semesterInfo = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'semesterInfo')]");
            if (semesterInfo != null)
            {
                var match = Regex.Match(semesterInfo.InnerText, @"(\d{4}-\d{4})学年第(\d)学期");
                if (match.Success)
                {
                    scheduleInfo.Year = match.Groups[1].Value;
                    scheduleInfo.Term = match.Groups[2].Value;
                }
            }
            
            // 解析课表表格
            var table = doc.DocumentNode.SelectSingleNode("//table[contains(@class, 'courseTable')]");
            if (table == null)
            {
                Console.WriteLine("未找到课表表格。");
                return scheduleInfo;
            }
            
            // 解析课程信息
            var courseNodes = doc.DocumentNode.SelectNodes("//div[contains(@class, 'kbcontent')]");
            if (courseNodes != null)
            {
                foreach (var courseNode in courseNodes)
                {
                    // 获取课程所在的单元格，确定星期几和节次
                    var cell = courseNode.ParentNode;
                    var row = cell.ParentNode;
                    
                    // 获取行和列索引
                    var rowIndex = row.SelectNodes("td").ToList().IndexOf(cell);
                    var colIndex = table.SelectNodes(".//tr").ToList().IndexOf(row);
                    
                    // 解析课程内容
                    var courseContent = courseNode.InnerHtml;
                    if (!string.IsNullOrWhiteSpace(courseContent) && courseContent != "&nbsp;")
                    {
                        var courses = ParseCourseContent(courseContent, rowIndex, colIndex);
                        scheduleInfo.Courses.AddRange(courses);
                    }
                }
            }
            
            return scheduleInfo;
        }
        
        /// <summary>
        /// 解析课程内容
        /// </summary>
        private static List<CourseInfo> ParseCourseContent(string content, int dayOfWeek, int startPeriod)
        {
            var courses = new List<CourseInfo>();
            
            // 分割多个课程
            var courseBlocks = Regex.Split(content, "<br><br>");
            foreach (var block in courseBlocks)
            {
                if (string.IsNullOrWhiteSpace(block)) continue;
                
                var course = new CourseInfo
                {
                    DayOfWeek = dayOfWeek - 1, // 调整为0表示周一
                    Periods = new List<int>()
                };
                
                // 添加课程节次
                for (int i = 0; i < 2; i++) // 假设每个课程占2节课
                {
                    course.Periods.Add(startPeriod + i);
                }
                
                // 解析课程名称
                var nameMatch = Regex.Match(block, @"<a[^>]*>([^<]+)</a>");
                if (nameMatch.Success)
                {
                    course.CourseName = nameMatch.Groups[1].Value.Trim();
                }
                
                // 解析课程代码
                var codeMatch = Regex.Match(block, @"\[([^\]]+)\]");
                if (codeMatch.Success)
                {
                    course.CourseCode = codeMatch.Groups[1].Value.Trim();
                }
                
                // 解析教师信息
                var teacherMatch = Regex.Match(block, @"教师:([^<]+)");
                if (teacherMatch.Success)
                {
                    var teacherName = teacherMatch.Groups[1].Value.Trim();
                    course.Teachers.Add(new Teacher { Name = teacherName });
                }
                
                // 解析教室信息
                var roomMatch = Regex.Match(block, @"教室:([^<]+)");
                if (roomMatch.Success)
                {
                    course.Classroom = roomMatch.Groups[1].Value.Trim();
                }
                
                // 解析周次信息
                var weekMatch = Regex.Match(block, @"周次:([^<]+)");
                if (weekMatch.Success)
                {
                    var weekInfo = weekMatch.Groups[1].Value.Trim();
                    course.WeekPattern = ConvertWeekInfoToPattern(weekInfo);
                    course.Weeks = ParseWeekInfo(weekInfo);
                }
                
                courses.Add(course);
            }
            
            return courses;
        }
        
        private const int MaxWeekNumber = 54;

        /// <summary>
        /// 将周次信息转换为模式字符串
        /// </summary>
        private static string ConvertWeekInfoToPattern(string weekInfo)
        {
            var weeks = ParseWeekInfo(weekInfo);
            var pattern = new char[MaxWeekNumber + 1];
            Array.Fill(pattern, '0');

            foreach (var week in weeks)
            {
                if (week >= 1 && week <= MaxWeekNumber)
                {
                    pattern[week] = '1';
                }
            }

            return new string(pattern);
        }

        /// <summary>
        /// 解析周次信息为周次列表
        /// </summary>
        private static List<int> ParseWeekInfo(string weekInfo)
        {
            var weeks = new List<int>();

            if (string.IsNullOrWhiteSpace(weekInfo))
            {
                return weeks;
            }

            var parts = weekInfo.Replace("周", "").Split(new[] { ',', '，' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (trimmed.Length == 0)
                {
                    continue;
                }

                if (trimmed.Contains("-"))
                {
                    var range = trimmed.Split('-');
                    if (range.Length == 2 &&
                        int.TryParse(range[0].Trim(), out int start) &&
                        int.TryParse(range[1].Trim().Replace("双", "").Replace("单", ""), out int end))
                    {
                        bool isEven = trimmed.Contains("双");
                        bool isOdd = trimmed.Contains("单");

                        for (int i = start; i <= end; i++)
                        {
                            if (i < 1 || i > MaxWeekNumber)
                            {
                                continue;
                            }

                            if ((isEven && i % 2 == 0) || (isOdd && i % 2 == 1) || (!isEven && !isOdd))
                            {
                                weeks.Add(i);
                            }
                        }
                    }
                }
                else if (int.TryParse(trimmed, out int week) && week >= 1 && week <= MaxWeekNumber)
                {
                    weeks.Add(week);
                }
            }

            return weeks.Distinct().OrderBy(w => w).ToList();
        }

        /// <summary>
        /// 生成周视图课表
        /// </summary>
        public static List<ScheduleWeek> GenerateWeekView(ScheduleInfo scheduleInfo, DateTime semesterStartDate)
        {
            var weekViews = new List<ScheduleWeek>();
            
            // 获取所有课程的周次
            var allWeeks = scheduleInfo.Courses
                .SelectMany(c => c.Weeks)
                .Distinct()
                .OrderBy(w => w)
                .ToList();
            
            foreach (var weekNum in allWeeks)
            {
                var weekView = new ScheduleWeek
                {
                    WeekNumber = weekNum,
                    StartDate = semesterStartDate.AddDays((weekNum - 1) * 7),
                    EndDate = semesterStartDate.AddDays((weekNum - 1) * 7 + 6),
                    Days = new List<ScheduleDay>()
                };
                
                // 生成每天的课表
                for (int day = 0; day < 7; day++)
                {
                    var scheduleDay = new ScheduleDay
                    {
                        DayOfWeek = day,
                        Date = weekView.StartDate.AddDays(day),
                        Courses = scheduleInfo.Courses
                            .Where(c => c.DayOfWeek == day && c.Weeks.Contains(weekNum))
                            .ToList()
                    };
                    
                    weekView.Days.Add(scheduleDay);
                }
                
                weekViews.Add(weekView);
            }
            
            return weekViews;
        }
    }
}