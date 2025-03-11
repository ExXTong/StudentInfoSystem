using System;
using System.Collections.Generic;

namespace StudentInfoSystem.Common.Models
{
    public class Teacher
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public bool IsLab { get; set; }
    }

    public class CourseInfo
    {
        public string CourseId { get; set; }             // 课程ID
        public string CourseCode { get; set; }           // 课程代码，如tb1130014
        public string CourseNumber { get; set; }         // 课程序号，如tb1130014.18
        public string CourseName { get; set; }           // 课程名称
        public double Credits { get; set; }              // 学分
        public List<Teacher> Teachers { get; set; }      // 教师列表
        public string Classroom { get; set; }            // 教室
        public string ClassroomId { get; set; }          // 教室ID
        public string WeekPattern { get; set; }          // 上课周次模式，如"01000000000001010100000000000000000000000000000000000"
        public string ProcessedWeekPattern { get; set; } // 处理后的周次模式（反转并去除前导0）
        public List<int> Weeks { get; set; }             // 上课周次列表
        public int DayOfWeek { get; set; }               // 星期几，0表示周一
        public List<int> Periods { get; set; }           // 课程节次列表
        public string Remark { get; set; }               // 备注信息
        public string RawHtmlInfo { get; set; }          // 原始HTML信息，用于调试

        public CourseInfo()
        {
            Teachers = new List<Teacher>();
            Weeks = new List<int>();
            Periods = new List<int>();
        }
    }
}