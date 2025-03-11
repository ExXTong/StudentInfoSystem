using Microsoft.Playwright;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace StudentInfoSystem.Common.Services
{
    /// <summary>
    /// 浏览器实例管理器，确保只创建一个浏览器实例
    /// </summary>
    public class BrowserManager : IBrowserManager, IDisposable
    {
        private IPlaywright _playwright;
        private IBrowser _browser;
        private readonly ConcurrentDictionary<IPage, IBrowserContext> _activePages = new ConcurrentDictionary<IPage, IBrowserContext>();
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private bool _initialized = false;
        private bool _disposed = false;

        public BrowserManager()
        {
            Console.WriteLine("创建BrowserManager实例");
        }

        public async Task Initialize()
        {
            if (!_initialized)
            {
                await _semaphore.WaitAsync();
                try
                {
                    if (!_initialized)
                    {
                        Console.WriteLine("初始化Playwright...");
                        _playwright = await Playwright.CreateAsync();
                        
                        Console.WriteLine("启动主浏览器实例...");
                        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                        {
                            Headless = true,
                            Args = new[] {
                                "--disable-blink-features=AutomationControlled",
                                "--disable-dev-shm-usage", 
                                "--no-sandbox"
                            }
                        });
                        
                        _initialized = true;
                        Console.WriteLine("BrowserManager初始化完成");
                    }
                }
                finally
                {
                    _semaphore.Release();
                }
            }
        }

        public async Task<IPage> GetPageAsync()
        {
            await Initialize();
            
            Console.WriteLine("为请求创建新的浏览器上下文和页面...");
            var context = await _browser.NewContextAsync(new BrowserNewContextOptions
            {
                ViewportSize = new ViewportSize { Width = 1280, Height = 720 },
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/123.0.0.0 Safari/537.36",
                Locale = "zh-CN",
                TimezoneId = "Asia/Shanghai",
                AcceptDownloads = true
            });
            
            var page = await context.NewPageAsync();
            
            // 设置更宽松的超时
            page.SetDefaultTimeout(30000);
            page.SetDefaultNavigationTimeout(30000);
            
            _activePages.TryAdd(page, context);
            Console.WriteLine($"当前活动页面数: {_activePages.Count}");
            
            return page;
        }

        public async Task<bool> LoginAsync(string username, string password, IPage page)
        {
            try
            {
                Console.WriteLine($"使用页面 #{page.GetHashCode()} 执行登录操作");
                
                // 直接实现登录逻辑，而不是调用静态工具类
                return await InternalLoginAsync(
                    page, 
                    username, 
                    password, 
                    maxRetries: 2,
                    timeoutMs: 30000,
                    loginTimeoutMs: 5000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"登录时发生错误: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 内部登录实现方法
        /// </summary>
        private async Task<bool> InternalLoginAsync(
            IPage page,
            string username,
            string password,
            int maxRetries = 3,
            int timeoutMs = 30000,
            int loginTimeoutMs = 3000)
        {
            // 记录开始时间用于性能分析
            var startTime = DateTime.Now;
            Console.WriteLine("开始登录流程");
            
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    Console.WriteLine($"登录尝试 {attempt}/{maxRetries}");
                    
                    // 尝试单次登录
                    bool success = await TryLoginOnceAsync(page, username, password, timeoutMs, loginTimeoutMs);
                    
                    if (success)
                    {
                        var duration = (DateTime.Now - startTime).TotalSeconds;
                        Console.WriteLine($"登录成功！耗时: {duration:F2}秒");
                        return true;
                    }
                    
                    // 如果失败，等待一段时间再重试
                    if (attempt < maxRetries)
                    {
                        int delayMs = 2000 * attempt; // 指数退避
                        Console.WriteLine($"登录失败，{delayMs/1000}秒后重试...");
                        await page.WaitForTimeoutAsync(delayMs);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"登录尝试 {attempt} 出错: {ex.Message}");
                    if (attempt < maxRetries)
                    {
                        // 出错后暂停更长时间
                        await page.WaitForTimeoutAsync(3000 * attempt);
                    }
                }
            }
            
            Console.WriteLine($"登录失败，已尝试 {maxRetries} 次");
            return false;
        }

        /// <summary>
        /// 执行单次登录尝试
        /// </summary>
        private async Task<bool> TryLoginOnceAsync(
            IPage page,
            string username,
            string password,
            int timeoutMs,
            int loginTimeoutMs)
        {
            try
            {
                // 导航到登录页面
                Console.WriteLine("访问登录页面...");
                await page.GotoAsync("https://tam.nwupl.edu.cn/", new PageGotoOptions 
                { 
                    WaitUntil = WaitUntilState.NetworkIdle,
                    Timeout = timeoutMs
                });
                
                // 确认网络状态良好
                if (!await CheckNetworkStatusAsync(page))
                {
                    Console.WriteLine("网络状态不佳，页面加载不完整");
                    return false;
                }
                
                Console.WriteLine("网页加载完成，准备输入登录信息");
                
                // 输入用户名
                Console.WriteLine("输入用户名...");
                await page.FillAsync("input#username", username);
                
                // 输入密码
                Console.WriteLine("输入密码...");
                await page.FillAsync("input#password", password);
                
                // 点击登录按钮
                Console.WriteLine("点击登录按钮...");
                try {
                    // 尝试通过文本内容查找登录按钮
                    var loginButton = page.GetByText("登录", new PageGetByTextOptions { Exact = true });
                    await loginButton.ClickAsync();
                    Console.WriteLine("登录按钮已点击");
                }
                catch (Exception ex) {
                    // 如果通过文本内容找不到，尝试通过原始选择器
                    Console.WriteLine($"通过文本查找登录按钮失败，尝试使用选择器: {ex.Message}");
                    await page.ClickAsync("button[type=login_submit]");
                    Console.WriteLine("通过选择器点击登录按钮");
                }
                
                // 等待登录结果
                Console.WriteLine("等待登录结果...");
                await page.WaitForTimeoutAsync(loginTimeoutMs);
                
                // 检查是否登录成功
                bool loggedIn = await IsLoggedIn(page);
                
                if (loggedIn)
                {
                    Console.WriteLine("检测到已成功登录");
                    return true;
                }
                else
                {
                    Console.WriteLine("登录似乎失败了");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"登录过程中出错: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 检查是否已登录成功
        /// </summary>
        private async Task<bool> IsLoggedIn(IPage page)
        {
            try
            {
                
                // 检查URL是否跳转到成功页面
                var url = page.Url;
                if (url.Contains("homeExt.action") || url.Contains("main.jsp") || url.Contains("home"))
                {
                    return true;
                }
                
                // 检查是否不再是登录页面
                var loginForm = await page.QuerySelectorAsync("form#fm1");
                return loginForm == null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"检查登录状态时出错: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 检查网页加载状态
        /// </summary>
        private async Task<bool> CheckNetworkStatusAsync(IPage page)
        {
            try
            {
                // 检查页面是否包含关键元素
                var usernameField = await page.QuerySelectorAsync("input#username");
                var passwordField = await page.QuerySelectorAsync("input#password");
                
                return usernameField != null && passwordField != null;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> LogoutAsync(IPage page)
        {
            try
            {
                if (page == null)
                {
                    Console.WriteLine("无法注销：提供的浏览器页面为空");
                    return false;
                }
                
                Console.WriteLine($"使用页面 #{page.GetHashCode()} 执行注销操作");
                
                // 访问登出页面
                await page.GotoAsync("https://tam.nwupl.edu.cn", new PageGotoOptions { 
                    WaitUntil = WaitUntilState.NetworkIdle, 
                    Timeout = 20000 
                });
                
                try
                {
                    // 查找并点击退出链接
                    await page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "" }).ClickAsync();
                    
                    // 等待注销完成
                    await page.WaitForTimeoutAsync(1500);
                    
                    Console.WriteLine($"页面 #{page.GetHashCode()} 已成功注销登录");
                    return true;
                }
                catch (PlaywrightException ex)
                {
                    Console.WriteLine($"未找到退出链接，可能已经处于登出状态: {ex.Message}");
                    return true; // 如果找不到退出链接，假定已经处于登出状态
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"注销过程中发生错误: {ex.Message}");
                return false;
            }
        }

        public async Task ReleaseBrowserAsync(IPage page)
        {
            if (_activePages.TryRemove(page, out IBrowserContext context))
            {
                try
                {
                    Console.WriteLine($"关闭并释放页面 #{page.GetHashCode()}");
                    await page.CloseAsync();
                    await context.CloseAsync();
                    Console.WriteLine($"释放后的活动页面数: {_activePages.Count}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"释放页面时发生错误: {ex.Message}");
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // 关闭所有活动页面和上下文
                    foreach (var kvp in _activePages)
                    {
                        try
                        {
                            kvp.Key.CloseAsync().GetAwaiter().GetResult();
                            kvp.Value.CloseAsync().GetAwaiter().GetResult();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"清理时发生错误: {ex.Message}");
                        }
                    }
                    
                    _activePages.Clear();
                    
                    // 关闭浏览器
                    if (_browser != null)
                    {
                        _browser.CloseAsync().GetAwaiter().GetResult();
                        _browser.DisposeAsync().AsTask().GetAwaiter().GetResult();
                    }
                    
                    // 释放Playwright
                    _playwright?.Dispose();
                }

                _disposed = true;
            }
        }
        
        // 实现IBrowserManager接口的异步Dispose方法
        async Task IBrowserManager.Dispose()
        {
            if (!_disposed)
            {
                // 关闭所有活动页面和上下文
                foreach (var kvp in _activePages)
                {
                    try
                    {
                        await kvp.Key.CloseAsync();
                        await kvp.Value.CloseAsync();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"清理时发生错误: {ex.Message}");
                    }
                }
                
                _activePages.Clear();
                
                // 关闭浏览器
                if (_browser != null)
                {
                    await _browser.CloseAsync();
                    await _browser.DisposeAsync();
                }
                
                // 释放Playwright
                _playwright?.Dispose();
                
                _disposed = true;
            }
        }

        ~BrowserManager()
        {
            Dispose(false);
        }
    }
}