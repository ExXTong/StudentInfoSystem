using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using StudentInfoSystem.StudentService.Services;
using StudentInfoSystem.Common.Portal;
using StudentInfoSystem.Common.Middleware;
using StudentInfoSystem.Common.Security;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// 添加服务到容器
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 注册学生门户 HTTP 客户端（每个请求独立会话）
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

// 注册爬虫服务
builder.Services.AddScoped<IStudentInfoCrawlerService, StudentInfoCrawlerService>();

// 注册 StudentInfoService，使用 HttpClient 并注入爬虫服务
builder.Services.AddHttpClient<IStudentInfoService, StudentInfoService>();
builder.Services.AddScoped<IStudentInfoService, StudentInfoService>();

// 添加HtmlAgilityPack依赖项（通过项目文件添加）

// 配置JWT认证
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtConfiguration.GetSigningKey(builder.Configuration)))
        };
    });

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

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();