namespace StudentInfoSystem.ScheduleService.Models
{
    /// <summary>
    /// 课表查询请求
    /// </summary>
    public class ScheduleRequest
    {
        /// <summary>
        /// 用户名（学号/工号）
        /// </summary>
        public string Username { get; set; }
        
        /// <summary>
        /// 密码
        /// </summary>
        public string Password { get; set; }
        
        /// <summary>
        /// 课表类型: std=学生课表, class=班级课表
        /// </summary>
        public string TableType { get; set; } = "std";
        
        /// <summary>
        /// 学年，如"2024-2025"
        /// </summary>
        public string Year { get; set; }
        
        /// <summary>
        /// 学期，"1"=第一学期，"2"=第二学期
        /// </summary>
        public string Term { get; set; }
    }
}