using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HtmlAgilityPack;
using System.Text.Json;
using System.Linq;
using StudentInfoSystem.Common.Models;

namespace StudentInfoSystem.Common.Services
{
    public static class StudentInfoParser
    {
        /// <summary>
        /// 从HTML页面解析学生信息
        /// </summary>
        public static StudentInfo ParseStudentInfoFromHtml(string htmlContent)
        {
            var studentInfo = new StudentInfo();

            try
            {
                var doc = new HtmlDocument();
                doc.LoadHtml(htmlContent);

                // 查找学籍信息表格
                var table = doc.DocumentNode.SelectSingleNode("//table[@id='studentInfoTb']");
                if (table == null)
                {
                    return null;
                }

                // 提取表格中的信息
                var rows = table.SelectNodes(".//tr");
                if (rows == null)
                {
                    return studentInfo;
                }

                Dictionary<string, string> infoMap = new Dictionary<string, string>();
                foreach (var row in rows)
                {
                    var cells = row.SelectNodes(".//td");
                    if (cells == null || cells.Count < 2)
                    {
                        continue;
                    }

                    for (int i = 0; i < cells.Count; i += 2)
                    {
                        if (i + 1 < cells.Count)
                        {
                            string key = cells[i].InnerText.Trim().TrimEnd('：');
                            string value = cells[i + 1].InnerText.Trim();
                            
                            if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value))
                            {
                                infoMap[key] = value;
                            }
                        }
                    }
                }

                // 提取照片URL
                var photoImg = doc.DocumentNode.SelectSingleNode("//td[@id='photoImg']/img");
                if (photoImg != null)
                {
                    studentInfo.PhotoUrl = photoImg.GetAttributeValue("src", "");
                }

                // 填充学籍信息
                studentInfo.StudentId = GetValueOrDefault(infoMap, "学号");
                studentInfo.Name = GetValueOrDefault(infoMap, "姓名");
                studentInfo.EnglishName = GetValueOrDefault(infoMap, "英文名");
                studentInfo.Gender = GetValueOrDefault(infoMap, "性别");
                studentInfo.Grade = GetValueOrDefault(infoMap, "年级");
                studentInfo.StudyYears = GetValueOrDefault(infoMap, "学制");
                studentInfo.Program = GetValueOrDefault(infoMap, "项目");
                studentInfo.EducationLevel = GetValueOrDefault(infoMap, "学历层次");
                studentInfo.StudentType = GetValueOrDefault(infoMap, "学生类别");
                studentInfo.Department = GetValueOrDefault(infoMap, "院系");
                studentInfo.Major = GetValueOrDefault(infoMap, "专业");
                studentInfo.Direction = GetValueOrDefault(infoMap, "方向");
                studentInfo.EnrollmentDate = GetValueOrDefault(infoMap, "入校时间");
                studentInfo.ExpectedGraduationDate = GetValueOrDefault(infoMap, "毕业时间");
                studentInfo.AdministrativeDepartment = GetValueOrDefault(infoMap, "行政管理院系");
                studentInfo.StudyForm = GetValueOrDefault(infoMap, "学习形式");
                studentInfo.IsRegistered = GetValueOrDefault(infoMap, "是否在籍");
                studentInfo.IsInSchool = GetValueOrDefault(infoMap, "是否在校");
                studentInfo.Campus = GetValueOrDefault(infoMap, "所属校区");
                studentInfo.Class = GetValueOrDefault(infoMap, "所属班级");
                studentInfo.RegistrationEffectiveDate = GetValueOrDefault(infoMap, "学籍生效日期");
                studentInfo.HasAcademicStatus = GetValueOrDefault(infoMap, "是否有学籍");
                studentInfo.AcademicStatus = GetValueOrDefault(infoMap, "学籍状态");
                studentInfo.IsPartTimeJob = GetValueOrDefault(infoMap, "是否在职");
                studentInfo.Remark = GetValueOrDefault(infoMap, "备注");

                // 提取更多个人信息
                ExtractPersonalInfo(doc, studentInfo);
                
                // 提取联系方式
                ExtractContactInfo(doc, studentInfo);
                
                // 提取家庭成员信息
                ExtractFamilyInfo(doc, studentInfo);

                return studentInfo;
            }
            catch (Exception)
            {
                return studentInfo;
            }
        }

        /// <summary>
        /// 提取个人基本信息
        /// </summary>
        private static void ExtractPersonalInfo(HtmlDocument doc, StudentInfo info)
        {
            var personalInfoTable = doc.DocumentNode.SelectSingleNode("//table[contains(@class, 'infoTable')][1]");
            if (personalInfoTable != null)
            {
                var rows = personalInfoTable.SelectNodes(".//tr");
                if (rows != null)
                {
                    Dictionary<string, string> infoMap = new Dictionary<string, string>();
                    ExtractTableData(rows, infoMap);

                    info.FormerName = GetValueOrDefault(infoMap, "曾用名");
                    info.Ethnicity = GetValueOrDefault(infoMap, "民族");
                    info.PoliticalStatus = GetValueOrDefault(infoMap, "政治面貌");
                    info.BirthDate = GetValueOrDefault(infoMap, "出生日期");
                    info.IdType = GetValueOrDefault(infoMap, "证件类型");
                    info.IdNumber = GetValueOrDefault(infoMap, "证件号码");
                    info.Birthplace = GetValueOrDefault(infoMap, "籍贯");
                    info.Country = GetValueOrDefault(infoMap, "国家");
                }
            }
        }

        /// <summary>
        /// 提取联系方式信息
        /// </summary>
        private static void ExtractContactInfo(HtmlDocument doc, StudentInfo info)
        {
            var contactInfoTable = doc.DocumentNode.SelectSingleNode("//table[contains(@class, 'infoTable')][2]");
            if (contactInfoTable != null)
            {
                var rows = contactInfoTable.SelectNodes(".//tr");
                if (rows != null)
                {
                    Dictionary<string, string> infoMap = new Dictionary<string, string>();
                    ExtractTableData(rows, infoMap);

                    info.Email = GetValueOrDefault(infoMap, "电子邮件");
                    info.Phone = GetValueOrDefault(infoMap, "联系电话");
                    info.Mobile = GetValueOrDefault(infoMap, "移动电话");
                    info.Address = GetValueOrDefault(infoMap, "联系地址");
                    info.HomePhone = GetValueOrDefault(infoMap, "家庭电话");
                    info.HomeAddress = GetValueOrDefault(infoMap, "家庭地址");
                    info.HomeAddressPostcode = GetValueOrDefault(infoMap, "家庭地址邮编");
                }
            }
        }

        /// <summary>
        /// 提取家庭成员信息
        /// </summary>
        private static void ExtractFamilyInfo(HtmlDocument doc, StudentInfo info)
        {
            var familyTable = doc.DocumentNode.SelectSingleNode("//table[contains(@class, 'infoContactTable')]");
            if (familyTable != null)
            {
                var familyRows = familyTable.SelectNodes(".//tr");
                if (familyRows != null && familyRows.Count > 1) // 跳过表头
                {
                    for (int i = 1; i < familyRows.Count; i++)
                    {
                        var cells = familyRows[i].SelectNodes(".//td");
                        if (cells != null && cells.Count >= 7)
                        {
                            var member = new FamilyMember
                            {
                                Name = cells[0].InnerText.Trim(),
                                Relationship = cells[1].InnerText.Trim(),
                                IsGuardian = cells[2].InnerText.Trim(),
                                Phone = cells.Count > 5 ? cells[5].InnerText.Trim() : "",
                                WorkUnit = cells.Count > 6 ? cells[6].InnerText.Trim() : ""
                            };
                            info.FamilyMembers.Add(member);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 从表格行中提取数据到字典
        /// </summary>
        private static void ExtractTableData(HtmlNodeCollection rows, Dictionary<string, string> infoMap)
        {
            foreach (var row in rows)
            {
                var cells = row.SelectNodes(".//td");
                if (cells == null || cells.Count < 2) continue;

                for (int i = 0; i < cells.Count; i += 2)
                {
                    if (i + 1 < cells.Count)
                    {
                        string key = cells[i].InnerText.Trim().TrimEnd('：');
                        string value = cells[i + 1].InnerText.Trim();
                        
                        if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value))
                        {
                            infoMap[key] = value;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 获取字典中的值，如果不存在则返回默认值
        /// </summary>
        private static string GetValueOrDefault(Dictionary<string, string> dict, string key, string defaultValue = "")
        {
            return dict.TryGetValue(key, out string value) ? value : defaultValue;
        }

        /// <summary>
        /// 将学生信息保存为JSON文件
        /// </summary>
        public static void SaveAsJson(StudentInfo studentInfo, string filePath)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string jsonString = JsonSerializer.Serialize(studentInfo, options);
            File.WriteAllText(filePath, jsonString, Encoding.UTF8);
        }

        /// <summary>
        /// 将学生信息保存为CSV文件
        /// </summary>
        public static void SaveAsCsv(StudentInfo studentInfo, string filePath)
        {
            var csv = new StringBuilder();
            
            // 添加基本信息
            csv.AppendLine("字段,值");
            csv.AppendLine($"学号,{studentInfo.StudentId}");
            csv.AppendLine($"姓名,{studentInfo.Name}");
            csv.AppendLine($"英文姓名,{studentInfo.EnglishName}");
            // ... 其他字段

            // 添加家庭成员信息
            csv.AppendLine("\n家庭成员信息:");
            csv.AppendLine("姓名,关系,是否监护人,证件类型,证件号码,联系电话,工作单位,工作地址");
            foreach (var member in studentInfo.FamilyMembers)
            {
                csv.AppendLine($"{member.Name},{member.Relationship},{member.IsGuardian},{member.IdType}," +
                               $"{member.IdNumber},{member.Phone},{member.WorkUnit},{member.WorkAddress}");
            }

            File.WriteAllText(filePath, csv.ToString(), Encoding.UTF8);
        }
    }
}