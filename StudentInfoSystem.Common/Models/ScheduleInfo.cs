using System;
using System.Collections.Generic;

namespace StudentInfoSystem.Common.Models
{
    public class ScheduleInfo
    {
        public string ScheduleId { get; set; }
        public string Year { get; set; }            // 学年
        public string Term { get; set; }            // 学期
        public string StudentId { get; set; }       // 学生ID
        public string ClassId { get; set; }         // 班级ID
        public List<CourseInfo> Courses { get; set; } // 课程列表
        
        public ScheduleInfo()
        {
            Courses = new List<CourseInfo>();
        }
    }
    
    public class ScheduleWeek
    {
        public int WeekNumber { get; set; }         // 周次
        public DateTime StartDate { get; set; }     // 开始日期
        public DateTime EndDate { get; set; }       // 结束日期
        public List<ScheduleDay> Days { get; set; } // 每天的课程
        
        public ScheduleWeek()
        {
            Days = new List<ScheduleDay>();
        }
    }
    
    public class ScheduleDay
    {
        public int DayOfWeek { get; set; }          // 星期几，0表示周一
        public DateTime Date { get; set; }          // 日期
        public List<CourseInfo> Courses { get; set; } // 当天的课程
        
        public ScheduleDay()
        {
            Courses = new List<CourseInfo>();
        }
    }
}