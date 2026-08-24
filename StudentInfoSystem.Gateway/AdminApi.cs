using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StudentInfoSystem.Common.Portal;

namespace StudentInfoSystem.Gateway;

public static class AdminApi
{
    private static readonly ConcurrentDictionary<string, AnnouncementItem> Announcements = new();
    private static readonly ConcurrentDictionary<string, string> Settings = new();
    private static readonly AdminStats Stats = new();
    private static readonly ConcurrentDictionary<string, bool> DisabledUsers = new();
    private static readonly ConcurrentQueue<LoginRecord> LoginHistory = new();

    public static void MapAdminApi(this WebApplication app)
    {
        app.MapGet("/api/announcements", () => Results.Ok(Announcements.Values.OrderByDescending(a => a.CreatedAt)));

        var adminGroup = app.MapGroup("/api/admin").RequireAuthorization("AuthenticatedUser");
        adminGroup.MapGet("/users", (HttpContext ctx, IConfiguration cfg) =>
            IsAdmin(ctx, cfg) ? Results.Ok(new { users = CachedStudentPortalClient.GetActiveUsernames() }) : Results.Forbid());

        adminGroup.MapPost("/cache/clear", (HttpContext ctx, IConfiguration cfg) =>
        {
            if (!IsAdmin(ctx, cfg)) return Results.Forbid();
            CachedStudentPortalClient.ClearAll();
            return Results.Ok(new { success = true, message = "缓存已清除" });
        });

        adminGroup.MapGet("/stats", (HttpContext ctx, IConfiguration cfg) =>
            IsAdmin(ctx, cfg) ? Results.Ok(Stats) : Results.Forbid());

        adminGroup.MapGet("/announcements", (HttpContext ctx, IConfiguration cfg) =>
            IsAdmin(ctx, cfg)
                ? Results.Ok(Announcements.Values.OrderByDescending(a => a.CreatedAt))
                : Results.Forbid());

        adminGroup.MapPost("/announcements", (HttpContext ctx, IConfiguration cfg, AnnouncementInput input) =>
        {
            if (!IsAdmin(ctx, cfg)) return Results.Forbid();
            var item = new AnnouncementItem
            {
                Id = Guid.NewGuid().ToString("N"),
                Title = input.Title,
                Content = input.Content,
                CreatedAt = DateTime.Now
            };
            Announcements[item.Id] = item;
            return Results.Ok(item);
        });

        adminGroup.MapDelete("/announcements/{id}", (HttpContext ctx, IConfiguration cfg, string id) =>
        {
            if (!IsAdmin(ctx, cfg)) return Results.Forbid();
            Announcements.TryRemove(id, out _);
            return Results.Ok(new { success = true });
        });

        adminGroup.MapGet("/settings", (HttpContext ctx, IConfiguration cfg) =>
            IsAdmin(ctx, cfg) ? Results.Ok(Settings) : Results.Forbid());

        adminGroup.MapPost("/settings", (HttpContext ctx, IConfiguration cfg, Dictionary<string, string> input) =>
        {
            if (!IsAdmin(ctx, cfg)) return Results.Forbid();
            foreach (var kv in input) Settings[kv.Key] = kv.Value;
            return Results.Ok(Settings);
        });

        adminGroup.MapGet("/users/disabled", (HttpContext ctx, IConfiguration cfg) =>
            IsAdmin(ctx, cfg) ? Results.Ok(DisabledUsers.Keys.OrderBy(x => x)) : Results.Forbid());

        adminGroup.MapPost("/users/{username}/disable", (HttpContext ctx, IConfiguration cfg, string username) =>
        {
            if (!IsAdmin(ctx, cfg)) return Results.Forbid();
            DisabledUsers[username] = true;
            return Results.Ok(new { success = true });
        });

        adminGroup.MapPost("/users/{username}/enable", (HttpContext ctx, IConfiguration cfg, string username) =>
        {
            if (!IsAdmin(ctx, cfg)) return Results.Forbid();
            DisabledUsers.TryRemove(username, out _);
            return Results.Ok(new { success = true });
        });

        adminGroup.MapPost("/users/{username}/reset", (HttpContext ctx, IConfiguration cfg, string username) =>
        {
            if (!IsAdmin(ctx, cfg)) return Results.Forbid();
            CachedStudentPortalClient.ClearUser(username);
            return Results.Ok(new { success = true });
        });

        adminGroup.MapGet("/login-history", (HttpContext ctx, IConfiguration cfg) =>
            IsAdmin(ctx, cfg) ? Results.Ok(LoginHistory.ToArray()) : Results.Forbid());
    }

    public class LoginRecord
    {
        public string Username { get; set; } = "";
        public DateTime Time { get; set; }
    }

    public static bool IsUserDisabled(string? username)
    {
        return !string.IsNullOrEmpty(username) && DisabledUsers.ContainsKey(username);
    }

    public static void ClearUser(string username)
    {
        // handled in CachedStudentPortalClient.ClearUser
    }

    public static void RecordLogin(string username)
    {
        Stats.TotalLogins++;
        Stats.LastLogin = username;
        Stats.LastLoginTime = DateTime.Now;
        LoginHistory.Enqueue(new LoginRecord { Username = username, Time = DateTime.Now });
        while (LoginHistory.Count > 100) LoginHistory.TryDequeue(out _);
    }

    public static void RecordQuery(string type)
    {
        Stats.TotalQueries++;
    }

    private static bool IsAdmin(HttpContext ctx, IConfiguration cfg)
    {
        var user = ctx.User;
        if (user.Identity?.IsAuthenticated != true) return false;
        var username = user.FindFirst("unique_name")?.Value
                       ?? user.FindFirst(ClaimTypes.Name)?.Value
                       ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                       ?? "";
        var admins = (cfg["Admin:Users"] ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return admins.Contains(username);
    }

    public class AnnouncementItem
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string Content { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }

    public class AnnouncementInput
    {
        public string Title { get; set; } = "";
        public string Content { get; set; } = "";
    }

    public class AdminStats
    {
        public long TotalLogins { get; set; }
        public long TotalQueries { get; set; }
        public string? LastLogin { get; set; }
        public DateTime? LastLoginTime { get; set; }
    }
}
