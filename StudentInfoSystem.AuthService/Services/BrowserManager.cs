using Microsoft.Playwright;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using StudentInfoSystem.Common.Services;
using StudentInfoSystem.AuthService.Services.Static;

namespace StudentInfoSystem.AuthService.Services
{
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
                
                // 使用LoginUtility登录
                var loginSuccess = await LoginUtility.LoginAsync(
                    page,
                    username,
                    password,
                    maxRetries: 2,
                    timeoutMs: 30000,
                    loginTimeoutMs: 5000,
                    useRoleBasedSelectors: true,
                    progressCallback: (message, current, total) => {
                        Console.WriteLine($"[{current}/{total}] {message}");
                    });
                
                return loginSuccess;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"登录时发生错误: {ex.Message}");
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