using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
//using StudentInfoSystem.Common.Services; // 更新引用
using StudentInfoSystem.AuthService.Services; // 添加这一行，引入正确的命名空间
using StudentInfoSystem.Common.Middleware;
using StudentInfoSystem.Common.Portal;
using StudentInfoSystem.Common.Security;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// 配置 Kestrel
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Configure(builder.Configuration.GetSection("Kestrel"));
});

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

// 注册LoginService服务
builder.Services.AddScoped<LoginService>(provider => {
    var config = provider.GetRequiredService<IConfiguration>();
    var portal = provider.GetRequiredService<IStudentPortalClient>();
    var jwtSecret = JwtConfiguration.GetSigningKey(config);
    var issuer = config["Jwt:Issuer"] ?? "StudentInfoSystem";
    var audience = config["Jwt:Audience"] ?? "StudentInfoSystemUsers";
    
    return new LoginService(portal, jwtSecret, issuer, audience, config["Admin:Password"]);
});

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

app.UseAuthentication();
app.UseAuthorization();

// 添加日志中间件
app.Use(async (context, next) =>
{
    Console.WriteLine($"收到请求: {context.Request.Method} {context.Request.Path}");
    await next();
    Console.WriteLine($"响应状态码: {context.Response.StatusCode}");
});

app.UseRouting();

app.MapControllers();

app.Run();