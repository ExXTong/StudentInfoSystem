using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using StudentInfoSystem.ScheduleService.Services;
using StudentInfoSystem.Portal;
using StudentInfoSystem.Common.Middleware;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// 添加服务到容器
builder.Services.AddControllers();

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

// CourseScheduleService作为作用域服务注册，每个HTTP请求一个实例
builder.Services.AddScoped<CourseScheduleService>();

// 添加日志服务
builder.Services.AddLogging();

// 添加Swagger/OpenAPI支持
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "课表服务 API",
        Version = "v1",
        Description = "提供查询学生课表功能的API服务"
    });
});

// 配置JWT认证
/*builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? "DefaultSecretKeyForDevelopment"))
        };
    });*/

// 添加CORS服务
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

var app = builder.Build();

// 使用安全中间件
app.UseMiddleware<ApiSecurityMiddleware>();

// 配置HTTP请求管道
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//app.UseHttpsRedirection();
app.UseCors("AllowAll");

//app.UseAuthentication();
//app.UseAuthorization();

app.MapControllers();

app.Run();