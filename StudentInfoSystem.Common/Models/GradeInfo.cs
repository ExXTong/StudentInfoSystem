using System;
using System.Collections.Generic;

namespace StudentInfoSystem.Common.Models
{
    public class GradeInfo
    {
        public string CourseId { get; set; }        // 课程ID
        public string CourseCode { get; set; }      // 课程代码
        public string CourseNumber { get; set; }    // 课程序号
        public string CourseName { get; set; }      // 课程名称
        public double Credits { get; set; }         // 学分
        public string GradeValue { get; set; }      // 成绩值
        public string GradePoint { get; set; }      // 绩点
        public string ExamType { get; set; }        // 考试类型
        public string CourseType { get; set; }      // 课程类型
        public string CourseNature { get; set; }    // 课程性质
        public string Year { get; set; }            // 学年
        public string Term { get; set; }            // 学期
        public string Remark { get; set; }          // 备注
    }

    public class GradeSummary
    {
        public double TotalCredits { get; set; }     // 总学分
        public double AverageGradePoint { get; set; } // 平均绩点
        public List<GradeInfo> Grades { get; set; } // 成绩列表
        public int TotalCourses { get; set; }
        public double AverageScore { get; set; }
        public List<CourseTypeStat> CourseTypeStats { get; set; } = new List<CourseTypeStat>();
        public Dictionary<string, int> ScoreDistribution { get; set; } = new Dictionary<string, int>();

        public class CourseTypeStat
        {
            public string Type { get; set; }
            public int CourseCount { get; set; }
            public double TotalCredits { get; set; }
            public double AverageScore { get; set; }
        }

        public GradeSummary()
        {
            Grades = new List<GradeInfo>();
        }
    }
}