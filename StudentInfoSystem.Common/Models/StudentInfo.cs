using System;
using System.Collections.Generic;

namespace StudentInfoSystem.Common.Models
{
    public class StudentInfo
    {
        // 基本学籍信息
        public string StudentId { get; set; }
        public string Name { get; set; }
        public string EnglishName { get; set; }
        public string Gender { get; set; }
        public string Grade { get; set; }
        public string StudyYears { get; set; }
        public string Program { get; set; }
        public string EducationLevel { get; set; }
        public string StudentType { get; set; }
        public string Department { get; set; }
        public string Major { get; set; }
        public string Direction { get; set; }
        public string EnrollmentDate { get; set; }
        public string ExpectedGraduationDate { get; set; }
        public string AdministrativeDepartment { get; set; }
        public string StudyForm { get; set; }
        public string IsRegistered { get; set; }
        public string IsInSchool { get; set; }
        public string Campus { get; set; }
        public string Class { get; set; }
        public string RegistrationEffectiveDate { get; set; }
        public string HasAcademicStatus { get; set; }
        public string AcademicStatus { get; set; }
        public string IsPartTimeJob { get; set; }
        public string Remark { get; set; }
        public string PhotoUrl { get; set; }

        // 学生基本信息
        public string FormerName { get; set; }
        public string Ethnicity { get; set; }
        public string PoliticalStatus { get; set; }
        public string BirthDate { get; set; }
        public string IdType { get; set; }
        public string IdNumber { get; set; }
        public string Birthplace { get; set; }
        public string Country { get; set; }
        public string MaritalStatus { get; set; }
        public string PartyJoinDate { get; set; }
        public string AccountName { get; set; }
        public string Bank { get; set; }
        public string BankAccount { get; set; }

        // 联系信息
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Mobile { get; set; }
        public string Address { get; set; }
        public string HomePhone { get; set; }
        public string HomeAddress { get; set; }
        public string HomeAddressPostcode { get; set; }
        public string TrainStation { get; set; }

        // 考生信息
        public string EntranceLevel { get; set; }
        public string SourceRegion { get; set; }
        public string ExamAdmissionNumber { get; set; }
        public string ExamNumber { get; set; }
        public string GraduationSchoolCode { get; set; }
        public string GraduationSchoolName { get; set; }
        public string GraduationDate { get; set; }
        public string EnrollmentMethod { get; set; }
        public string TrainingMode { get; set; }
        public string StudentSourceType { get; set; }
        public string AdmissionScore { get; set; }
        public string OtherScores { get; set; }

        // 家庭成员
        public List<FamilyMember> FamilyMembers { get; set; } = new List<FamilyMember>();

        /// <summary>
        /// 存储额外的键值对数据
        /// </summary>
        public Dictionary<string, object> AdditionalData { get; set; } = new Dictionary<string, object>();
    }
}