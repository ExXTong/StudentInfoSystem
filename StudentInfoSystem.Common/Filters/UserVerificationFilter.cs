using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using System.Reflection;

namespace StudentInfoSystem.Common.Filters
{
    public class UserVerificationFilter : IAsyncActionFilter
    {
        private readonly ILogger<UserVerificationFilter> _logger;

        public UserVerificationFilter(ILogger<UserVerificationFilter> logger)
        {
            _logger = logger;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            // 从HttpContext获取JWT中的用户名
            string jwtUsername = context.HttpContext.Items["Username"] as string;

            if (string.IsNullOrEmpty(jwtUsername))
            {
                _logger.LogWarning("未能从请求中获取JWT用户名");
                context.Result = new UnauthorizedObjectResult(new { message = "未授权的请求" });
                return;
            }

            // 从请求参数中找出包含Username的对象
            string requestUsername = null;
            foreach (var param in context.ActionArguments)
            {
                if (param.Value == null) continue;

                // 尝试直接获取Username属性
                var usernameProperty = param.Value.GetType().GetProperty("Username");
                if (usernameProperty != null)
                {
                    requestUsername = usernameProperty.GetValue(param.Value) as string;
                    if (!string.IsNullOrEmpty(requestUsername)) break;
                }
                
                // 递归检查模型中是否有Username属性
                requestUsername = ExtractUsernameFromObject(param.Value);
                if (!string.IsNullOrEmpty(requestUsername)) break;
            }

            if (!string.IsNullOrEmpty(requestUsername) && requestUsername != jwtUsername)
            {
                _logger.LogWarning($"用户名不匹配: JWT中为 {jwtUsername}，请求中为 {requestUsername}");
                context.Result = new ObjectResult(new
                {
                    success = false,
                    message = "请求的用户名与授权的用户名不一致"
                })
                {
                    StatusCode = 403
                };
                return;
            }

            await next();
        }

        private string ExtractUsernameFromObject(object obj)
        {
            if (obj == null) return null;

            // 检查对象的所有属性
            foreach (PropertyInfo prop in obj.GetType().GetProperties())
            {
                // 如果找到名为"Username"的属性
                if (string.Equals(prop.Name, "Username", StringComparison.OrdinalIgnoreCase))
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
                        var nestedResult = ExtractUsernameFromObject(value);
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