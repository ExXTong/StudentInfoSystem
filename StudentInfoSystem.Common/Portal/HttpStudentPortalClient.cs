using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace StudentInfoSystem.Common.Portal
{
    /// <summary>
    /// 基于 HttpClient + CookieContainer 的学生门户客户端，不依赖 Playwright。
    /// </summary>
    public class HttpStudentPortalClient : IStudentPortalClient, IDisposable
    {
        private static readonly string Chars = "ABCDEFGHJKMNPQRSTWXYZabcdefhijkmnprstwxyz2345678";
        private static readonly System.Threading.SemaphoreSlim GlobalThrottle = new(4, 4);

        private readonly StudentPortalOptions _options;
        private readonly ILogger<HttpStudentPortalClient> _logger;
        private readonly HttpClient _httpClient;
        private readonly CookieContainer _cookieContainer;
        private bool _loggedIn;

        public HttpStudentPortalClient(StudentPortalOptions options, ILogger<HttpStudentPortalClient> logger)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cookieContainer = new CookieContainer();

            var handler = new HttpClientHandler
            {
                CookieContainer = _cookieContainer,
                UseCookies = true,
                AllowAutoRedirect = false
            };

            if (!string.IsNullOrWhiteSpace(_options.Proxy))
            {
                handler.Proxy = new WebProxy(_options.Proxy);
                handler.UseProxy = true;
            }
            else
            {
                handler.UseProxy = false;
            }

            _httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds)
            };

            ApplyBrowserHeaders();
            SeedFingerprintCookies();
        }

        private void ApplyBrowserHeaders()
        {
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept",
                "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language",
                "en,zh-CN;q=0.9,zh;q=0.8,en-GB;q=0.7,en-US;q=0.6");
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Cache-Control", "no-cache");
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Pragma", "no-cache");
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", _options.UserAgent);
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("sec-ch-ua",
                "\"Not=A?Brand\";v=\"99\", \"Microsoft Edge\";v=\"151\", \"Chromium\";v=\"151\"");
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("sec-ch-ua-mobile", "?0");
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("sec-ch-ua-platform", "\"Windows\"");
        }

        private void SeedFingerprintCookies()
        {
            if (string.IsNullOrWhiteSpace(_options.FingerprintCookies))
            {
                return;
            }

            var uri = new Uri(_options.AuthServerBaseUrl);
            foreach (var part in _options.FingerprintCookies.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var idx = part.IndexOf('=');
                if (idx <= 0)
                {
                    continue;
                }

                var name = part[..idx].Trim();
                var value = part[(idx + 1)..].Trim();
                _cookieContainer.Add(new Cookie(name, value, "/", uri.Host));
            }
        }

        private async Task<string> ThrottledGetStringAsync(string url)
        {
            await GlobalThrottle.WaitAsync();
            try
            {
                return await _httpClient.GetStringAsync(url);
            }
            finally
            {
                GlobalThrottle.Release();
            }
        }

        private async Task<HttpResponseMessage> ThrottledSendAsync(HttpRequestMessage request)
        {
            await GlobalThrottle.WaitAsync();
            try
            {
                return await _httpClient.SendAsync(request);
            }
            finally
            {
                GlobalThrottle.Release();
            }
        }

        public async Task<bool> LoginAsync(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                return false;
            }

            var service = Uri.EscapeDataString(_options.ServiceUrl);
            var loginPageUrl = $"{_options.AuthServerBaseUrl}/authserver/login?service={service}";
            var loginPage = await ThrottledGetStringAsync(loginPageUrl);
            var execution = Extract(loginPage, "name=\"execution\"\\s+value=\"([^\"]+)\"");
            var salt = Extract(loginPage, "id=\"pwdEncryptSalt\"\\s+value=\"([^\"]+)\"");

            if (string.IsNullOrEmpty(execution) || string.IsNullOrEmpty(salt))
            {
                _logger.LogError("登录页缺少 execution 或 pwdEncryptSalt");
                return false;
            }

            var encryptedPassword = EncryptPassword(password, salt);
            var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["username"] = username,
                ["password"] = encryptedPassword,
                ["captcha"] = "",
                ["rememberMe"] = "true",
                ["_eventId"] = "submit",
                ["cllt"] = "userNameLogin",
                ["dllt"] = "generalLogin",
                ["lt"] = "",
                ["execution"] = execution
            });

            var request = new HttpRequestMessage(HttpMethod.Post, loginPageUrl)
            {
                Content = form
            };
            request.Headers.TryAddWithoutValidation("Origin", _options.AuthServerBaseUrl);
            request.Headers.TryAddWithoutValidation("Referer", loginPageUrl);
            request.Headers.TryAddWithoutValidation("Sec-Fetch-Dest", "document");
            request.Headers.TryAddWithoutValidation("Sec-Fetch-Mode", "navigate");
            request.Headers.TryAddWithoutValidation("Sec-Fetch-Site", "same-origin");
            request.Headers.TryAddWithoutValidation("Sec-Fetch-User", "?1");
            request.Headers.TryAddWithoutValidation("Upgrade-Insecure-Requests", "1");

            var response = await ThrottledSendAsync(request);
            var location = response.Headers.Location?.ToString() ?? "";

            if ((int)response.StatusCode is 301 or 302 && location.Contains("ticket="))
            {
                var ticketResponse = await _httpClient.GetAsync(location);
                if ((int)ticketResponse.StatusCode is 301 or 302 && ticketResponse.Headers.Location != null)
                {
                    await _httpClient.GetAsync(ticketResponse.Headers.Location.ToString());
                }
                else
                {
                    await _httpClient.GetAsync(location);
                }

                _loggedIn = true;
                _logger.LogInformation("学生门户登录成功");
                return true;
            }

            _logger.LogWarning("学生门户登录失败，状态码: {StatusCode}", response.StatusCode);
            return false;
        }

        public async Task<string> GetHomePageAsync()
        {
            EnsureLoggedIn();
            return await ThrottledGetStringAsync($"{_options.EamsBaseUrl}/eams/homeExt.action");
        }

        public async Task<string> GetStudentDetailAsync()
        {
            EnsureLoggedIn();
            return await ThrottledGetStringAsync($"{_options.EamsBaseUrl}/eams/stdDetail.action");
        }

        public async Task<string> GetHistoryGradeAsync()
        {
            EnsureLoggedIn();
            return await ThrottledGetStringAsync($"{_options.EamsBaseUrl}/eams/teach/grade/course/person!historyCourseGrade.action?projectType=MAJOR");
        }

        public async Task<string> GetGradePageAsync()
        {
            EnsureLoggedIn();
            return await ThrottledGetStringAsync($"{_options.EamsBaseUrl}/eams/teach/grade/course/person.action");
        }

        public async Task<string> GetGradeSearchAsync(string semesterId, string projectType)
        {
            EnsureLoggedIn();
            var url = $"{_options.EamsBaseUrl}/eams/teach/grade/course/person!search.action?semesterId={Uri.EscapeDataString(semesterId)}&projectType={Uri.EscapeDataString(projectType ?? "")}";
            return await ThrottledGetStringAsync(url);
        }

        public async Task<string> GetCourseTablePageAsync()
        {
            EnsureLoggedIn();
            return await ThrottledGetStringAsync($"{_options.EamsBaseUrl}/eams/courseTableForStd.action");
        }

        public async Task<string> GetCourseTableDataAsync(string semesterId, string projectId, string ids, string kind)
        {
            EnsureLoggedIn();
            var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["ignoreHead"] = "1",
                ["setting.kind"] = string.IsNullOrWhiteSpace(kind) ? "std" : kind,
                ["startWeek"] = "",
                ["project.id"] = projectId,
                ["semester.id"] = semesterId,
                ["ids"] = ids
            });

            var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.EamsBaseUrl}/eams/courseTableForStd!courseTable.action")
            {
                Content = form
            };
            request.Headers.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");
            var response = await ThrottledSendAsync(request);
            return await response.Content.ReadAsStringAsync();
        }

        public Task LogoutAsync()
        {
            _loggedIn = false;
            _cookieContainer.GetCookies(new Uri(_options.EamsBaseUrl)).ToList().ToList().ForEach(c => c.Expired = true);
            return Task.CompletedTask;
        }

        private void EnsureLoggedIn()
        {
            if (!_loggedIn)
            {
                throw new InvalidOperationException("尚未登录学生门户");
            }
        }

        private static string Extract(string html, string pattern)
        {
            var m = Regex.Match(html, pattern, RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[1].Value : "";
        }

        internal static string RandomString(int length)
        {
            var sb = new StringBuilder(length);
            for (int i = 0; i < length; i++)
            {
                sb.Append(Chars[Random.Shared.Next(Chars.Length)]);
            }
            return sb.ToString();
        }

        internal static string EncryptPassword(string password, string salt)
        {
            using var aes = Aes.Create();
            aes.Key = Encoding.UTF8.GetBytes(salt);
            aes.IV = Encoding.UTF8.GetBytes(RandomString(16));
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            var plaintext = Encoding.UTF8.GetBytes(RandomString(64) + password);
            using var encryptor = aes.CreateEncryptor();
            var encrypted = encryptor.TransformFinalBlock(plaintext, 0, plaintext.Length);
            return Convert.ToBase64String(encrypted);
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }
    }
}
