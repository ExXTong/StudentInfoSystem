using System;
using System.Collections.Generic;
using System.Linq;
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
                if (cells == null || cells.Count < 8)
                {
                    continue;
                }

                var semesterParts = cells[0].InnerText.Split(new[] { ' ', '-' }, StringSplitOptions.RemoveEmptyEntries);

                var grade = new GradeInfo
                {
                    Year = semesterParts.Length > 0 ? semesterParts[0].Trim() : string.Empty,
                    Term = semesterParts.Length > 1 ? semesterParts[1].Trim() : string.Empty,
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

            return grades;
        }

        /// <summary>
        /// 生成成绩统计信息
        /// </summary>
        public static GradeSummary GenerateGradeSummary(List<GradeInfo> grades)
        {
            var summary = new GradeSummary();
            summary.Grades = grades ?? new List<GradeInfo>();

            if (summary.Grades.Count == 0)
            {
                return summary;
            }

            var validGrades = summary.Grades.Where(g => g.Credits > 0).ToList();
            double totalCredits = validGrades.Sum(g => g.Credits);
            double totalWeightedPoints = 0;
            double totalWeightedScore = 0;

            foreach (var grade in validGrades)
            {
                if (!string.IsNullOrEmpty(grade.GradePoint) &&
                    double.TryParse(grade.GradePoint, out double point))
                {
                    totalWeightedPoints += point * grade.Credits;
                }

                if (!string.IsNullOrEmpty(grade.GradeValue) &&
                    double.TryParse(grade.GradeValue, out double score))
                {
                    totalWeightedScore += score * grade.Credits;
                }
            }

            summary.TotalCredits = Math.Round(totalCredits, 1);
            summary.TotalCourses = summary.Grades.Count;

            if (totalCredits > 0)
            {
                summary.AverageGradePoint = Math.Round(totalWeightedPoints / totalCredits, 2);
                summary.AverageScore = Math.Round(totalWeightedScore / totalCredits, 2);
            }

            summary.CourseTypeStats = summary.Grades
                .GroupBy(g => g.CourseType)
                .Select(g => new GradeSummary.CourseTypeStat
                {
                    Type = g.Key,
                    CourseCount = g.Count(),
                    TotalCredits = g.Sum(c => c.Credits),
                    AverageScore = g.Sum(c => c.Credits) > 0
                        ? Math.Round(g.Sum(c => ParseScoreToDouble(c.GradeValue) * c.Credits) / g.Sum(c => c.Credits), 2)
                        : 0
                })
                .ToList();

            summary.ScoreDistribution = new Dictionary<string, int>
            {
                { "优秀(90-100)", 0 },
                { "良好(80-89)", 0 },
                { "中等(70-79)", 0 },
                { "及格(60-69)", 0 },
                { "不及格(<60)", 0 },
                { "其他评级", 0 }
            };

            foreach (var grade in summary.Grades)
            {
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

            return summary;
        }

        private static double ParseScoreToDouble(string scoreText)
        {
            if (string.IsNullOrEmpty(scoreText))
                return 0;

            if (double.TryParse(scoreText, out double score))
                return score;

            return scoreText.ToLowerInvariant() switch
            {
                "优秀" or "优" => 95,
                "良好" or "良" => 85,
                "中等" or "中" => 75,
                "合格" or "及格" or "通过" => 65,
                "不合格" or "不及格" or "不通过" => 0,
                _ => 0
            };
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
