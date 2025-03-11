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
    public static class GradeParser
    {
        /// <summary>
        /// 从HTML内容中解析成绩数据
        /// </summary>
        public static List<GradeInfo> ParseGrades(string htmlContent)
        {
            var grades = new List<GradeInfo>();
            var doc = new HtmlDocument();
            doc.LoadHtml(htmlContent);

            // 查找成绩表格中的所有行
            var tbody = doc.DocumentNode.SelectNodes("//tbody[starts-with(@id, 'grid') and contains(@id, '_data')]")
                                      .FirstOrDefault();
            if (tbody == null)
            {
                Console.WriteLine("未找到成绩表格。");
                return grades;
            }

            var rows = tbody.SelectNodes(".//tr");
            if (rows == null || rows.Count == 0)
            {
                Console.WriteLine("未找到成绩数据。");
                return grades;
            }

            foreach (var row in rows)
            {
                var cells = row.SelectNodes("td");
                if (cells != null && cells.Count >= 8)
                {
                    var grade = new GradeInfo
                    {
                        Year = cells[0].InnerText.Split('-')[0].Trim(),
                        Term = cells[0].InnerText.Split('-')[1].Trim(),
                        CourseCode = cells[1].InnerText.Trim(),
                        CourseNumber = cells[2].InnerText.Trim(),
                        CourseName = cells[3].InnerText.Trim(),
                        CourseType = cells[4].InnerText.Trim(),
                        Credits = ParseDouble(cells[5].InnerText),
                        GradeValue = cells[6].InnerText.Trim(),
                        GradePoint = cells[7].InnerText.Trim()
                    };
                    grades.Add(grade);
                }
            }

            return grades;
        }

        /// <summary>
        /// 生成成绩统计信息
        /// </summary>
        public static GradeSummary GenerateGradeSummary(List<GradeInfo> grades)
        {
            var summary = new GradeSummary();
            summary.Grades = grades;

            if (grades == null || !grades.Any())
            {
                return summary;
            }

            // 计算总学分
            summary.TotalCredits = grades.Sum(g => g.Credits);

            // 计算平均绩点
            var validGrades = grades.Where(g => !string.IsNullOrEmpty(g.GradePoint) && 
                                               double.TryParse(g.GradePoint, out _));
            if (validGrades.Any())
            {
                double totalWeightedPoints = validGrades.Sum(g => 
                    g.Credits * double.Parse(g.GradePoint));
                double totalCredits = validGrades.Sum(g => g.Credits);
                summary.AverageGradePoint = totalWeightedPoints / totalCredits;
            }

            return summary;
        }

        private static double ParseDouble(string text)
        {
            if (double.TryParse(text.Trim(), out double result))
            {
                return result;
            }
            return 0;
        }
    }
}