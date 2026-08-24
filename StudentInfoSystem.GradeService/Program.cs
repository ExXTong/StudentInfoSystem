using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using StudentInfoSystem.Portal;
using StudentInfoSystem.GradeService.Services;
using Microsoft.OpenApi;
using System.IO;
using System;
using System.Reflection;
using StudentInfoSystem.Common.Middleware;
using StudentInfoSystem.Common.Security;
using System.Text;
using StudentInfoSystem.Common.Filters;

using GradeServiceImpl = StudentInfoSystem.GradeService.Services.GradeService;

var builder = WebApplication.CreateBuilder(args);

// 添加服务到容器
builder.Services.AddControllers(options =>
{
    options.Filters.Add<YearRestrictionFilter>();
});

// 注册学生门户 HTTP 客户端（每个请求独立会话，适合多用户并发）
builder.Services.AddScoped<IStudentPortalClient>(sp =>
{
    var options = new StudentPortalOptions
    {
        AuthServerBaseUrl = builder.Configuration["Portal:AuthServerBaseUrl"] ?? "https://authserver.nwupl.edu.cn",
        EamsBaseUrl = builder.Configuration["Portal:EamsBaseUrl"] ?? "https://tam.nwupl.edu.cn",
        ServiceUrl = builder.Configuration["Portal:ServiceUrl"] ?? "https://tam.nwupl.edu.cn/eams/homeExt.action",
        Proxy = builder.Configuration["Portal:Proxy"] ?? "",
        TimeoutSeconds = int.TryParse(builder.Configuration["Portal:TimeoutSeconds"], out var t) ? t : 60,
        FingerprintCookies = builder.Configuration["Portal:FingerprintCookies"] ?? "",
        UserAgent = builder.Configuration["Portal:UserAgent"] ??
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/151.0.0.0 Safari/537.36 Edg/151.0.0.0"
    };
    return new CachedStudentPortalClient(() => new HttpStudentPortalClient(options, sp.GetRequiredService<ILogger<HttpStudentPortalClient>>()));
});

// GradeService实现
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

// 运行应用
app.Run();