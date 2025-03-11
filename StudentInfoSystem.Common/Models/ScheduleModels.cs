using System;
using System.Collections.Generic;

namespace StudentInfoSystem.Common.Models
{
    /// <summary>
    /// 课程表请求参数
    /// </summary>
    public class ScheduleRequest
    {
        /// <summary>
        /// 用户名/学号
        /// </summary>
        public string Username { get; set; }
        
        /// <summary>
        /// 密码
        /// </summary>
        public string Password { get; set; }
        
        /// <summary>
        /// 课表类型: std=学生课表, class=班级课表
        /// </summary>
        public string TableType { get; set; }
        
        /// <summary>
        /// 学年，如"2024-2025"
        /// </summary>
        public string Year { get; set; }
        
        /// <summary>
        /// 学期，"1"=第一学期，"2"=第二学期
        /// </summary>
        public string Term { get; set; }
    }

    /// <summary>
    /// 课程表响应结果
    /// </summary>
    public class ScheduleResponse
    {
        /// <summary>
        /// 操作是否成功
        /// </summary>
        public bool Success { get; set; }
        
        /// <summary>
        /// 响应消息
        /// </summary>
        public string Message { get; set; }
        
        /// <summary>
        /// 保存目录
        /// </summary>
        public string SaveDirectory { get; set; }
        
        /// <summary>
        /// JSON文件路径
        /// </summary>
        public string JsonFilePath { get; set; }
        
        /// <summary>
        /// CSV文件路径
        /// </summary>
        public string CsvFilePath { get; set; }
        
        /// <summary>
        /// 课程列表
        /// </summary>
        public List<CourseInfoLite> Courses { get; set; }
    }
}