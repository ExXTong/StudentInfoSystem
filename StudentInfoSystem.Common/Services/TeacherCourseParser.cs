using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using System.Threading.Tasks;
using HtmlAgilityPack;
using StudentInfoSystem.Common.Models;

namespace StudentInfoSystem.Common.Services
{
    /// <summary>
    /// 教师课程信息类，存储解析后的课程教师数据
    /// </summary>
    public class TeacherCourseInfo
    {
        public int Index { get; set; } // 序号
        public string CourseCode { get; set; } // 课程代码
        public string CourseName { get; set; } // 课程名称
        public decimal Credits { get; set; } // 学分
        public string CourseNumber { get; set; } // 课程序号
        public string TeacherName { get; set; } // 教师名称
        public int? TeacherId { get; set; } // 教师ID
        public string Remarks { get; set; } // 备注
        public string LessonId { get; set; } // 课程ID (从URL中提取)
    }

    /// <summary>
    /// 教师信息类
    /// </summary>
    public class TeacherInfo
    {
        public int Id { get; set; } // 教师ID
        public string Name { get; set; } // 教师姓名
        public bool IsLab { get; set; } // 是否为实验室教师
    }

    /// <summary>
    /// 教师课程信息的简化版本，仅包含必要字段
    /// </summary>
    public class TeacherLiteInfo
    {
        public string TeacherName { get; set; }
        public string CourseNumber { get; set; }
        public int? TeacherId { get; set; }
    }

    /// <summary>
    /// 教师课程解析器，用于从HTML中提取课程表格数据并匹配教师ID
    /// </summary>
    public class TeacherCourseParser
    {
        /// <summary>
        /// 从HTML内容中解析课程表数据
        /// </summary>
        /// <param name="htmlContent">HTML内容</param>
        /// <returns>解析出的课程信息列表</returns>
        public async Task<List<TeacherCourseInfo>> ParseAsync(string htmlContent)
        {
            // 使用HtmlAgilityPack解析HTML
            var doc = new HtmlDocument();
            doc.LoadHtml(htmlContent);

            // 解析课程表格数据
            var courses = ParseCourseTable(doc);
            
            // 提取教师ID映射
            var teacherIdMap = ExtractTeacherIds(htmlContent);
            
            // 匹配教师ID到课程
            MatchTeacherIds(courses, teacherIdMap);
            
            return courses;
        }

        /// <summary>
        /// 解析课程表格
        /// </summary>
        private List<TeacherCourseInfo> ParseCourseTable(HtmlDocument doc)
        {
            var courses = new List<TeacherCourseInfo>();
            
            // 查找课程表格，表格ID可能变化，所以使用更通用的选择器
            var tbody = doc.DocumentNode.SelectSingleNode("//tbody[contains(@id, 'grid') and contains(@id, 'data')]");
            if (tbody == null)
            {
                // 尝试其他可能的选择器
                tbody = doc.DocumentNode.SelectSingleNode("//div[@class='grid']//tbody");
            }
            
            if (tbody != null)
            {
                var rows = tbody.SelectNodes("tr");
                if (rows != null)
                {
                    foreach (var row in rows)
                    {
                        var cells = row.SelectNodes("td");
                        if (cells != null && cells.Count >= 8)
                        {
                            var course = new TeacherCourseInfo
                            {
                                Index = ParseInt(cells[0].InnerText),
                                CourseCode = cells[1].InnerText.Trim(),
                                CourseName = cells[2].InnerText.Trim(),
                                Credits = ParseDecimal(cells[3].InnerText),
                                CourseNumber = cells[4].InnerText.Trim(),
                                TeacherName = cells[5].InnerText.Trim(),
                                Remarks = cells.Count > 7 ? cells[7].InnerText.Trim() : ""
                            };
                            
                            // 提取课程ID
                            var lessonIdMatch = Regex.Match(cells[2].InnerHtml, @"lesson=([\d]+)");
                            if (lessonIdMatch.Success)
                            {
                                course.LessonId = lessonIdMatch.Groups[1].Value;
                            }
                            
                            courses.Add(course);
                        }
                    }
                }
            }
            
            return courses;
        }

        /// <summary>
        /// 从HTML中提取教师ID映射
        /// </summary>
        private Dictionary<string, int> ExtractTeacherIds(string htmlContent)
        {
            var teacherIdMap = new Dictionary<string, int>();
            
            // 使用正则表达式查找教师ID和名称的映射
            var matches = Regex.Matches(htmlContent, @"teacher=(\d+)[^>]*>([^<]+)<");
            foreach (Match match in matches)
            {
                if (match.Groups.Count >= 3)
                {
                    int teacherId = int.Parse(match.Groups[1].Value);
                    string teacherName = match.Groups[2].Value.Trim();
                    
                    if (!teacherIdMap.ContainsKey(teacherName))
                    {
                        teacherIdMap[teacherName] = teacherId;
                    }
                }
            }
            
            return teacherIdMap;
        }

        /// <summary>
        /// 将教师ID匹配到课程信息中
        /// </summary>
        private void MatchTeacherIds(List<TeacherCourseInfo> courses, Dictionary<string, int> teacherIdMap)
        {
            foreach (var course in courses)
            {
                if (!string.IsNullOrEmpty(course.TeacherName) && teacherIdMap.ContainsKey(course.TeacherName))
                {
                    course.TeacherId = teacherIdMap[course.TeacherName];
                }
            }
        }

        /// <summary>
        /// 解析整数
        /// </summary>
        private int ParseInt(string text)
        {
            if (int.TryParse(text.Trim(), out int result))
            {
                return result;
            }
            return 0;
        }

        /// <summary>
        /// 解析小数
        /// </summary>
        private decimal ParseDecimal(string text)
        {
            if (decimal.TryParse(text.Trim(), out decimal result))
            {
                return result;
            }
            return 0;
        }
    }
}