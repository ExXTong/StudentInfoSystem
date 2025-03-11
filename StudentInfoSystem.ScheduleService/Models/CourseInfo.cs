using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace StudentInfoSystem.ScheduleService.Models
{
    /// <summary>
    /// 课程信息模型
    /// </summary>
    public class CourseInfo
    {
        public string CourseId { get; set; }             // 课程ID
        public string CourseCode { get; set; }           // 课程代码，如tb1130014
        public string CourseNumber { get; set; }         // 课程序号，如tb1130014.18
        public string CourseName { get; set; }           // 课程名称
        public double Credits { get; set; }              // 学分
        public string TeacherName { get; set; }          // 教师名称
        public int? TeacherId { get; set; }              // 教师ID
        public string Classroom { get; set; }            // 教室
        public string ClassroomId { get; set; }          // 教室ID
        public string WeekInfo { get; set; }             // 上课周次信息
        public List<int> Weeks { get; set; }             // 上课周次列表
        public int DayOfWeek { get; set; }               // 星期几，1表示周一
        public int StartPeriod { get; set; }             // 开始节次
        public int EndPeriod { get; set; }               // 结束节次
        public string FormattedPeriods { get; set; }     // 格式化的节次信息，例如"第1-3节"或"第1,3,5节"
        public string Remark { get; set; }               // 备注信息

        [JsonIgnore]
        public List<int> Periods { get; set; }           // 课程节次列表

        public CourseInfo()
        {
            Weeks = new List<int>();
            Periods = new List<int>();
        }
    }
}