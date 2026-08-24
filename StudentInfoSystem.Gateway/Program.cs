using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using StudentInfoSystem.Common.Security;
using StudentInfoSystem.Gateway;
using Yarp.ReverseProxy.Transforms;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Security.Claims;
using System.Text.Json;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

// 添加CORS服务
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// 添加JWT验证
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    var jwtConfig = builder.Configuration.GetSection("Jwt");
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtConfig["Issuer"],
        ValidAudience = jwtConfig["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtConfiguration.GetSigningKey(builder.Configuration))),
        ClockSkew = TimeSpan.Zero // 设置为零以严格验证令牌过期时间
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AuthenticatedUser", policy => policy.RequireAuthenticatedUser());
});

// 添加反向代理
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(transforms =>
    {
        // 添加转换以传递认证信息 - 修复为使用单一值
        transforms.AddRequestTransform(async context =>
        {
            if (context.HttpContext.Request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                // 修改这里：使用First()方法获取单个值而不是ToArray()
                context.ProxyRequest.Headers.TryAddWithoutValidation("Authorization", authHeader.First());
            }
        });
    });

// 添加Swagger服务
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseCors();

// 配置HTTP请求管道
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}

//app.UseHttpsRedirection();
app.Use(async (context, next) =>
{
    if (HttpMethods.IsPost(context.Request.Method) && context.Request.Path.StartsWithSegments("/api/auth/login"))
    {
        context.Request.EnableBuffering();
        using var reader = new StreamReader(context.Request.Body, System.Text.Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        context.Request.Body.Position = 0;
        LoginPayload? payload = null;
        try
        {
            payload = JsonSerializer.Deserialize<LoginPayload>(body);
        }
        catch
        {
        }

        AdminApi.RecordLogin(payload?.Username ?? "unknown");
        if (AdminApi.IsUserDisabled(payload?.Username))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { message = "该用户已被管理员禁用" });
            return;
        }
    }
    if (context.Request.Path.StartsWithSegments("/api/schedule/get") ||
        context.Request.Path.StartsWithSegments("/api/grade/query") ||
        context.Request.Path.StartsWithSegments("/api/student/crawler"))
    {
        AdminApi.RecordQuery(context.Request.Path);
    }

    await next();
});


app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapAdminApi();

// 配置反向代理
app.MapReverseProxy(proxyPipeline =>
{
    // 添加中间件来处理请求转发
    proxyPipeline.Use(async (context, next) =>
    {
        // 使用TryAdd而不是Add，避免重复添加
        context.Request.Headers.TryAdd("X-Gateway-Source", "StudentInfoGateway");

        // 如果配置了共享密钥，则传给下游服务用于二次校验
        var gatewaySecret = app.Configuration["Security:GatewaySecret"];
        if (!string.IsNullOrWhiteSpace(gatewaySecret))
        {
            context.Request.Headers.TryAdd("X-Gateway-Secret", gatewaySecret);
        }

        // 如果用户已经通过身份验证，提取用户信息并传递给下游服务
        if (context.User.Identity?.IsAuthenticated == true)
        {
            // 尝试从不同的claim类型中获取用户名
            var usernameClaim = context.User.FindFirst("unique_name") // JWT标准claim
                             ?? context.User.FindFirst("username")     // 自定义claim
                             ?? context.User.FindFirst("sub")          // 原始subject
                             ?? context.User.FindFirst(ClaimTypes.NameIdentifier) // JWT映射后的sub
                             ?? context.User.FindFirst(ClaimTypes.Name);          // JWT映射后的unique_name

            if (usernameClaim != null)
            {
                // 将用户名添加到请求头（使用 TryAdd 避免重复）
                context.Request.Headers.TryAdd("X-User-Name", usernameClaim.Value);
            }
            
            // 添加调试日志
            Console.WriteLine($"已认证用户: {usernameClaim?.Value ?? "未找到用户名"}");
        }
        else
        {
            Console.WriteLine("用户未认证");
        }

        await next();
    });
});

app.Run();
internal class LoginPayload
{
    [System.Text.Json.Serialization.JsonPropertyName("username")]
    public string? Username { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("password")]
    public string? Password { get; set; }
}
