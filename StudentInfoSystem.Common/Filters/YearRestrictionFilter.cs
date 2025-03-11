using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using System.Linq;
using System.Reflection;

namespace StudentInfoSystem.Common.Filters
{
    public class YearRestrictionFilter : IAsyncActionFilter
    {
        private readonly ILogger<YearRestrictionFilter> _logger;

        public YearRestrictionFilter(ILogger<YearRestrictionFilter> logger)
        {
            _logger = logger;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            // 获取请求中的用户名
            string username = context.HttpContext.Items["Username"] as string;

            if (string.IsNullOrEmpty(username) || username.Length < 4)
            {
                _logger.LogWarning("无法从请求中获取有效用户名或用户名格式不正确");
                await next();
                return;
            }

            // 提取用户名前四位作为入学年份
            if (!int.TryParse(username.Substring(0, 4), out int enrollmentYear))
            {
                _logger.LogWarning($"无法从用户名 {username} 中提取有效年份");
                await next();
                return;
            }

            // 从请求参数中找出包含年份的字段
            string requestedYear = null;
            foreach (var param in context.ActionArguments)
            {
                if (param.Value == null) continue;

                // 尝试直接获取Year属性
                var yearProperty = param.Value.GetType().GetProperty("Year");
                if (yearProperty != null)
                {
                    requestedYear = yearProperty.GetValue(param.Value) as string;
                    if (!string.IsNullOrEmpty(requestedYear)) break;
                }
                
                // 递归检查模型中是否有Year属性
                requestedYear = ExtractYearFromObject(param.Value);
                if (!string.IsNullOrEmpty(requestedYear)) break;
            }

            if (!string.IsNullOrEmpty(requestedYear))
            {
                // 处理年份格式，例如"2022-2023"或单一年份"2022"
                string yearStr = requestedYear.Split('-')[0];
                if (int.TryParse(yearStr, out int requestYear))
                {
                    if (requestYear < enrollmentYear)
                    {
                        _logger.LogWarning($"用户 {username} (入学年份: {enrollmentYear}) 尝试访问 {requestYear} 年的数据");
                        context.Result = new ObjectResult(new
                        {
                            success = false,
                            message = $"您不能查询入学年份 ({enrollmentYear}) 之前的数据"
                        })
                        {
                            StatusCode = 403
                        };
                        return;
                    }
                }
            }

            await next();
        }

        private string ExtractYearFromObject(object obj)
        {
            if (obj == null) return null;

            // 检查对象的所有属性
            foreach (PropertyInfo prop in obj.GetType().GetProperties())
            {
                // 如果找到名为"Year"的属性
                if (string.Equals(prop.Name, "Year", StringComparison.OrdinalIgnoreCase))
                {
                    return prop.GetValue(obj) as string;
                }
                
                // 递归检查复杂对象
                if (prop.PropertyType.IsClass && 
                    prop.PropertyType != typeof(string) && 
                    !prop.PropertyType.IsValueType)
                {
                    var value = prop.GetValue(obj);
                    if (value != null)
                    {
                        var nestedResult = ExtractYearFromObject(value);
                        if (nestedResult != null)
                        {
                            return nestedResult;
                        }
                    }
                }
            }
            
            return null;
        }
    }
}