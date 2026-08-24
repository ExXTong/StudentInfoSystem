using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using StudentInfoSystem.Portal.Models;

namespace StudentInfoSystem.Portal.Services;

public static class GradeTableParser
{
    private static readonly char[] SemesterSeparators = { ' ', '-' };
    private static readonly Regex YearRegex = new(
        @"学年：\s*([\d-]+)|学年学期：\s*([\d-]+)\s*学期|学年:\s*([\d-]+)",
        RegexOptions.Compiled);
    private static readonly Regex TermRegex = new(
        @"学期：\s*(\d+)|学年学期：\s*[\d-]+\s*学期(\d+)|学期:\s*(\d+)",
        RegexOptions.Compiled);

        public static List<GradeInfo> ParseGradesFromHtml(string htmlContent)
        {
            var grades = new List<GradeInfo>();
            var doc = new HtmlDocument();
            doc.LoadHtml(htmlContent);
        
            try {
                Console.WriteLine("开始解析成绩HTML内容...");
                
                // 1. 首先尝试解析成绩详细列表表格
                /*var gradeTable = doc.DocumentNode.SelectSingleNode("//table[@id and contains(@class, 'gridtable') and .//th[contains(text(), '课程名称')]]");
                if (gradeTable != null)
                {
                    Console.WriteLine("找到课程成绩详细表格");
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
                            Console.WriteLine("找到成绩表格（通用方法）");
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
                    Console.WriteLine($"找到ID为 {gridTable.GetAttributeValue("id", "unknown")} 的表格");
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
                    Console.WriteLine("找到grid表格数据");
                    var rows = tbody.SelectNodes(".//tr");
                    if (rows != null && rows.Count > 0)
                    {
                        return ParseGridTableRows(rows, htmlContent);
                    }
                }
                
                Console.WriteLine("未能找到任何包含成绩的表格");
            }
            catch (Exception ex) {
                Console.WriteLine($"解析HTML内容时发生异常: {ex.Message}");
            }
        
            return grades;
        }
        
        /// <summary>
        /// 解析成绩表格行
        /// </summary>
        public static List<GradeInfo> ParseGradeTableRows(HtmlNodeCollection rows)
        {
            var grades = new List<GradeInfo>();
            Console.WriteLine($"开始解析表格，共 {rows.Count} 行数据");
            
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
                            Console.WriteLine("跳过汇总统计行");
                            continue;
                        }
                        
                        Console.WriteLine($"跳过无效行: 单元格数量不足({cells?.Count ?? 0})");
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
                        Console.WriteLine("跳过没有课程名称的条目");
                        continue;
                    }
                    
                    // 添加到结果列表
                    grades.Add(grade);
                    Console.WriteLine($"解析到课程: {grade.CourseName}, 成绩: {grade.GradeValue}, 学分: {grade.Credits}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"解析单行成绩数据时出错: {ex.Message}");
                }
            }
            
            Console.WriteLine($"表格解析完成，共解析出 {grades.Count} 条成绩记录");
            return grades;
        }
        
        /// <summary>
        /// 专门解析grid结构的表格行
        /// </summary>
        public static List<GradeInfo> ParseGridTableRows(HtmlNodeCollection rows, string htmlContent)
        {
            var grades = new List<GradeInfo>();
            Console.WriteLine($"开始解析Grid表格，共 {rows.Count} 行数据");
            
            // Grid表格通常每行都是数据，不需要跳过表头
            foreach (var row in rows)
            {
                try 
                {
                    // 获取所有单元格
                    var cells = row.SelectNodes(".//td");
                    if (cells == null || cells.Count < 6)
                    {
                        Console.WriteLine($"跳过无效行: 单元格数量不足({(cells?.Count ?? 0)})");
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
                        Console.WriteLine("跳过没有课程名称的条目");
                        continue;
                    }
                    
                    // 添加到结果列表
                    grades.Add(grade);
                    Console.WriteLine($"解析到课程: {grade.CourseName}, 成绩: {grade.GradeValue}, 学分: {grade.Credits}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"解析单行成绩数据时出错: {ex.Message}");
                }
            }
            
            Console.WriteLine($"Grid表格解析完成，共解析出 {grades.Count} 条成绩记录");
            return grades;
        }
        
        /// <summary>
        /// 从学期文本中提取学年信息
        /// </summary>
        public static string ExtractAcademicYear(string semesterText)
        {
            var match = YearRegex.Match(semesterText);
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
        public static string ExtractAcademicTerm(string semesterText)
        {
            var match = TermRegex.Match(semesterText);
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
}
