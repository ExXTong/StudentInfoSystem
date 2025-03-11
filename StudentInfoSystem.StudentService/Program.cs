using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using StudentInfoSystem.StudentService.Services;
using StudentInfoSystem.Common.Services;
using StudentInfoSystem.Common.Middleware;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// 添加服务到容器
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 注册浏览器管理器为单例
builder.Services.AddSingleton<IBrowserManager, BrowserManager>();

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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? "DefaultSecretKeyForDevelopment"))
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