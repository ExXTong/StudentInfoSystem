using System;
using System.Collections.Generic;

namespace StudentInfoSystem.Common.Models{
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
}