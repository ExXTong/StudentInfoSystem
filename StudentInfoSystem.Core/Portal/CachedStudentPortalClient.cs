using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace StudentInfoSystem.Core.Portal
{
    /// <summary>
    /// 为学生门户客户端增加短期会话缓存，避免同一用户每次请求都重新登录。
    /// 同一用户的操作通过信号量串行化，保证 CookieContainer 安全。
    /// </summary>
    public class CachedStudentPortalClient : IStudentPortalClient
    {
        private static readonly ConcurrentDictionary<string, Entry> Cache = new();
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

        private readonly Func<HttpStudentPortalClient> _factory;
        private string _username = "";
        private Entry? _entry;

        public CachedStudentPortalClient(Func<HttpStudentPortalClient> factory)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }



        public static void ClearUser(string username)
        {
            if (Cache.TryRemove(username, out var entry))
            {
                try { entry.Client.Dispose(); } catch { }
            }
        }

        public static IReadOnlyList<string> GetActiveUsernames()
        {
            return Cache.Keys.ToList();
        }

        public static void ClearAll()
        {
            foreach (var entry in Cache.Values)
            {
                try { entry.Client.Dispose(); } catch { }
            }
            Cache.Clear();
        }

        public async Task<bool> LoginAsync(string username, string password)
        {
            _username = username;

            if (Cache.TryGetValue(username, out var cached) && !cached.IsExpired)
            {
                _entry = cached;
                await cached.Gate.WaitAsync();
                try
                {
                    return true;
                }
                finally
                {
                    cached.Gate.Release();
                }
            }

            var client = _factory();
            var ok = await client.LoginAsync(username, password);
            if (ok)
            {
                var entry = new Entry(client);
                _entry = entry;
                Cache[username] = entry;
            }

            return ok;
        }

        public Task<string> GetHomePageAsync() => ExecuteAsync(c => c.GetHomePageAsync());
        public Task<string> GetStudentDetailAsync() => ExecuteAsync(c => c.GetStudentDetailAsync());
        public Task<string> GetGradePageAsync() => ExecuteAsync(c => c.GetGradePageAsync());
        public Task<string> GetHistoryGradeAsync() => ExecuteAsync(c => c.GetHistoryGradeAsync());
        public Task<string> GetGradeSearchAsync(string semesterId, string projectType) =>
            ExecuteAsync(c => c.GetGradeSearchAsync(semesterId, projectType));
        public Task<string> GetCourseTablePageAsync() => ExecuteAsync(c => c.GetCourseTablePageAsync());
        public Task<string> GetCourseTableDataAsync(string semesterId, string projectId, string ids, string kind) =>
            ExecuteAsync(c => c.GetCourseTableDataAsync(semesterId, projectId, ids, kind));

        public async Task LogoutAsync()
        {
            if (_entry == null)
            {
                return;
            }

            await _entry.Gate.WaitAsync();
            try
            {
                await _entry.Client.LogoutAsync();
            }
            finally
            {
                _entry.Gate.Release();
            }

            Cache.TryRemove(_username, out _);
            _entry = null;
        }

        private async Task<T> ExecuteAsync<T>(Func<IStudentPortalClient, Task<T>> action)
        {
            if (_entry == null)
            {
                throw new InvalidOperationException("尚未登录学生门户");
            }

            await _entry.Gate.WaitAsync();
            try
            {
                return await action(_entry.Client);
            }
            finally
            {
                _entry.Gate.Release();
            }
        }

        private class Entry
        {
            public Entry(HttpStudentPortalClient client)
            {
                Client = client;
                CreatedAt = DateTime.UtcNow;
            }

            public HttpStudentPortalClient Client { get; }
            public SemaphoreSlim Gate { get; } = new(1, 1);
            public DateTime CreatedAt { get; }
            public bool IsExpired => DateTime.UtcNow - CreatedAt > CacheTtl;
        }
    }
}
