using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
//using StudentInfoSystem.Common.Services; // 更新引用
using StudentInfoSystem.AuthService.Services; // 添加这一行，引入正确的命名空间
using StudentInfoSystem.Common.Middleware;
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

// 注册BrowserManager作为单例服务
builder.Services.AddSingleton<IBrowserManager, BrowserManager>();

// 注册LoginService服务
builder.Services.AddScoped<LoginService>(provider => {
    var config = provider.GetRequiredService<IConfiguration>();
    var browserManager = provider.GetRequiredService<IBrowserManager>();
    var jwtSecret = config["Jwt:Key"] ?? "DefaultSecretKeyForDevelopment";
    var issuer = config["Jwt:Issuer"] ?? "StudentInfoSystem";
    var audience = config["Jwt:Audience"] ?? "StudentInfoSystemUsers";
    
    return new LoginService(browserManager, jwtSecret, issuer, audience);
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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? "DefaultSecretKeyForDevelopment"))
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