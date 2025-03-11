using System.Collections.Generic;

namespace StudentInfoSystem.ScheduleService.Models
{
    /// <summary>
    /// 课表查询响应
    /// </summary>
    public class ScheduleResponse
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool Success { get; set; }
        
        /// <summary>
        /// 错误消息
        /// </summary>
        public string ErrorMessage { get; set; }
        
        /// <summary>
        /// 课程列表
        /// </summary>
        public List<CourseInfo> Courses { get; set; } = new List<CourseInfo>();
    }
}