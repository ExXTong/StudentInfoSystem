using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using StudentInfoSystem.Common.Services;
using StudentInfoSystem.GradeService.Services;
using Microsoft.OpenApi.Models;
using System.IO;
using System;
using System.Reflection;
using StudentInfoSystem.Common.Middleware;
using System.Text;
using StudentInfoSystem.Common.Filters;

using GradeServiceImpl = StudentInfoSystem.GradeService.Services.GradeService;

var builder = WebApplication.CreateBuilder(args);

// 添加服务到容器
builder.Services.AddControllers(options =>
{
    options.Filters.Add<YearRestrictionFilter>();
});

// 清理重复注册，正确注册服务
// 1. 浏览器管理器(单例模式)
builder.Services.AddSingleton<IBrowserManager, BrowserManager>();

// 2. 使用工厂方法注册LoginService，解决字符串参数问题
builder.Services.AddScoped<LoginService>(provider => {
    // 获取浏览器管理器实例
    var browserManager = provider.GetRequiredService<IBrowserManager>();
    var logger = provider.GetRequiredService<ILogger<LoginService>>();
    
    // 从配置获取JWT配置，如果没有则使用默认值
    string jwtSecret = builder.Configuration["Jwt:Key"] ?? 
                      "YourSuperSecretKeyForJWTThatIsLongEnough";
    string issuer = builder.Configuration["Jwt:Issuer"] ?? "StudentInfoSystem";
    string audience = builder.Configuration["Jwt:Audience"] ?? "StudentInfoSystemUsers";
    
    // 创建LoginService实例，提供所有必要的参数
    return new LoginService(browserManager, jwtSecret, issuer, audience);
});

// 3. GradeService实现
builder.Services.AddScoped<GradeServiceImpl>();

// 添加Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { 
        Title = "Student Info System Grade API", 
        Version = "v1",
        Description = "API for retrieving student grade information"
    });
    
    // 添加XML注释文件用于Swagger文档
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

// 添加CORS策略(参考ScheduleService)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});


// 配置日志
builder.Services.AddLogging();


builder.Services.AddHealthChecks();

var app = builder.Build();

// 使用安全中间件
app.UseMiddleware<ApiSecurityMiddleware>();

// 配置HTTP请求管道
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// 启用CORS
app.UseCors("AllowAll");

// 启用路由和授权
app.UseRouting();
//app.UseAuthentication();
//app.UseAuthorization();

// 映射控制器路由
app.MapControllers();

// 初始化浏览器管理器
using (var scope = app.Services.CreateScope())
{
    var browserManager = scope.ServiceProvider.GetRequiredService<IBrowserManager>();
    await browserManager.Initialize();
}

// 运行应用
app.Run();