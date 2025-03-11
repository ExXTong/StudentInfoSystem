using System;
using System.Collections.Generic;
using System.Linq;

namespace StudentInfoSystem.Common.Models
{
    /// <summary>
    /// 轻量级课程信息类（用于序列化）
    /// </summary>
    public class CourseInfoLite
    {
        public string CourseId { get; set; }
        public string CourseCode { get; set; }
        public string CourseNumber { get; set; }
        public string CourseName { get; set; }
        public double Credits { get; set; }
        public string TeacherNames { get; set; }
        public string Classroom { get; set; }
        public string ClassroomId { get; set; }
        public List<int> Weeks { get; set; }
        public int DayOfWeek { get; set; }
        public List<int> Periods { get; set; }
        public string Remark { get; set; }

        public CourseInfoLite(CourseInfo course)
        {
            CourseId = course.CourseId;
            CourseCode = course.CourseCode;
            CourseNumber = course.CourseNumber;
            CourseName = course.CourseName;
            Credits = course.Credits;
            TeacherNames = string.Join(", ", course.Teachers.Select(t => t.Name));
            Classroom = course.Classroom;
            ClassroomId = course.ClassroomId;
            Weeks = new List<int>(course.Weeks);
            DayOfWeek = course.DayOfWeek;
            Periods = new List<int>(course.Periods);
            Remark = course.Remark;
        }
    }

    /// <summary>
    /// 轻量级教师信息类
    /// </summary>
    public class TeacherLiteInfo
    {
        public string CourseNumber { get; set; }
        public string TeacherName { get; set; }
        public int TeacherId { get; set; }
    }
}