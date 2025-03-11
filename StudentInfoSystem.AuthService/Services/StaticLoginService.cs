using Microsoft.Playwright;
using System;
using System.Threading.Tasks;

namespace StudentInfoSystem.AuthService.Services.Static
{
    public static class LoginUtility
    {
        /// <summary>
        /// 使用账号密码登录系统，包含超时处理和重试机制
        /// </summary>
        /// <param name="page">Playwright页面对象</param>
        /// <param name="username">学号/工号</param>
        /// <param name="password">密码</param>
        /// <param name="maxRetries">最大重试次数，默认为3次</param>
        /// <param name="timeoutMs">操作超时时间（毫秒），默认为30秒</param>
        /// <param name="loginTimeoutMs">单次登录操作的超时时间（毫秒），默认为3秒</param>
        /// <param name="useRoleBasedSelectors">是否使用基于角色的选择器（更稳定但可能较慢）</param>
        /// <param name="progressCallback">登录进度回调，可用于更新UI</param>
        /// <returns>登录是否成功</returns>
        public static async Task<bool> LoginAsync(
            IPage page, 
            string username, 
            string password, 
            int maxRetries = 3, 
            int timeoutMs = 30000, 
            int loginTimeoutMs = 3000,
            bool useRoleBasedSelectors = true,
            Action<string, int, int>? progressCallback = null)
        {
            // 记录开始时间用于性能分析
            var startTime = DateTime.Now;
            progressCallback?.Invoke("开始登录流程", 0, maxRetries);
            
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    string attemptMessage = $"尝试第 {attempt}/{maxRetries} 次登录...";
                    Console.WriteLine(attemptMessage);
                    progressCallback?.Invoke(attemptMessage, attempt, maxRetries);
                    
                    // 如果不是第一次尝试，检查网络状态
                    if (attempt > 1)
                    {
                        progressCallback?.Invoke("检查网络连接状态...", attempt, maxRetries);
                        bool isNetworkOnline = await CheckNetworkStatusAsync(page);
                        if (!isNetworkOnline)
                        {
                            string networkMessage = "网络连接异常，等待5秒后重试...";
                            Console.WriteLine(networkMessage);
                            progressCallback?.Invoke(networkMessage, attempt, maxRetries);
                            await Task.Delay(5000);
                        }
                        
                        // 刷新页面重新开始
                        progressCallback?.Invoke("刷新页面准备重新登录...", attempt, maxRetries);
                        try {
                            await page.ReloadAsync(new() { Timeout = timeoutMs, WaitUntil = WaitUntilState.NetworkIdle });
                        }
                        catch {
                            progressCallback?.Invoke("页面刷新失败，准备重新登录...", attempt, maxRetries);
                        }
                    }
                    
                    // 执行单次登录尝试
                    progressCallback?.Invoke("填写登录表单...", attempt, maxRetries);
                    bool loginSuccess = await TryLoginOnceAsync(
                        page, 
                        username, 
                        password, 
                        timeoutMs, 
                        loginTimeoutMs,
                        useRoleBasedSelectors);
                        
                    if (loginSuccess) 
                    {
                        // 计算总耗时
                        TimeSpan duration = DateTime.Now - startTime;
                        string successMessage = $"登录成功！总耗时: {duration.TotalSeconds:F1}秒";
                        Console.WriteLine(successMessage);
                        progressCallback?.Invoke(successMessage, maxRetries, maxRetries);
                        return true;
                    }
                    
                    // 明确处理登录失败的情况
                    if (attempt < maxRetries)
                    {
                        int waitTime = 2000 * attempt;
                        string retryMessage = $"第 {attempt} 次登录尝试失败，将在 {waitTime/1000} 秒后进行第 {attempt+1} 次尝试...";
                        Console.WriteLine(retryMessage);
                        progressCallback?.Invoke(retryMessage, attempt, maxRetries);
                        await Task.Delay(waitTime); // 逐次增加等待时间
                    }
                    else
                    {
                        string failMessage = $"已达到最大重试次数 ({maxRetries})，登录彻底失败";
                        Console.WriteLine(failMessage);
                        progressCallback?.Invoke(failMessage, maxRetries, maxRetries);
                    }
                }
                catch (TimeoutException ex)
                {
                    string timeoutMessage = $"第 {attempt} 次登录尝试超时: {ex.Message}";
                    Console.WriteLine(timeoutMessage);
                    progressCallback?.Invoke(timeoutMessage, attempt, maxRetries);
                    
                    if (attempt == maxRetries)
                    {
                        string finalMessage = "已达到最大重试次数，登录失败";
                        Console.WriteLine(finalMessage);
                        progressCallback?.Invoke(finalMessage, maxRetries, maxRetries);
                    }
                    else
                    {
                        int waitTime = 3000 * attempt;
                        string retryMessage = $"等待 {waitTime/1000} 秒后重试...";
                        Console.WriteLine(retryMessage);
                        progressCallback?.Invoke(retryMessage, attempt, maxRetries);
                        await Task.Delay(waitTime); // 逐次增加等待时间
                    }
                }
                catch (Exception ex)
                {
                    string errorMessage = $"登录过程中发生异常: {ex.Message}";
                    Console.WriteLine(errorMessage);
                    progressCallback?.Invoke(errorMessage, attempt, maxRetries);
                    
                    if (attempt == maxRetries)
                        return false;
                }
            }
            return false;
        }

        /// <summary>
        /// 执行单次登录尝试
        /// </summary>
        private static async Task<bool> TryLoginOnceAsync(
            IPage page, 
            string username, 
            string password, 
            int timeoutMs, 
            int loginTimeoutMs,
            bool useRoleBasedSelectors = true)
        {
            try
            {
                // 创建登录计时器
                var loginTimer = System.Diagnostics.Stopwatch.StartNew();
                
                // 设置页面默认超时时间
                page.SetDefaultTimeout(timeoutMs);

                // 访问登录页面
                await page.GotoAsync("https://tam.nwupl.edu.cn", new() { Timeout = timeoutMs, WaitUntil = WaitUntilState.DOMContentLoaded });

                // 根据选择使用不同的元素交互方法
                if (useRoleBasedSelectors)
                {
                    // 使用基于角色的交互方法
                    if (!await WaitAndActByRoleAsync(page, AriaRole.Textbox, 
                        new() { Name = "请输入学号/工号" }, 
                        async el => {
                            await el.ClickAsync();
                            await el.FillAsync(username);
                        }, loginTimeoutMs))
                        return false;
                    
                    if (!await WaitAndActByRoleAsync(page, AriaRole.Textbox, 
                        new() { Name = "请输入密码" }, 
                        async el => {
                            await el.ClickAsync();
                            await el.FillAsync(password);
                        }, loginTimeoutMs))
                        return false;
                    
                    if (!await WaitAndActAsync(page, "#rememberMe", 
                        async el => await el.CheckAsync(), loginTimeoutMs))
                        return false;
                    
                    if (!await WaitAndActByRoleAsync(page, AriaRole.Link, 
                        new() { Name = "登录", Exact = true }, 
                        async el => {
                            Console.WriteLine("点击登录按钮...");
                            await el.ClickAsync();
                            Console.WriteLine("登录按钮已点击，等待页面响应...");
                        }, loginTimeoutMs))
                        return false;
                }
                else
                {
                    // 使用传统选择器方法
                    if (!await WaitAndActAsync(page, "input[placeholder='请输入学号/工号']", 
                        async el => await el.FillAsync(username), loginTimeoutMs))
                        return false;
                    
                    if (!await WaitAndActAsync(page, "input[placeholder='请输入密码']", 
                        async el => await el.FillAsync(password), loginTimeoutMs))
                        return false;
                    
                    if (!await WaitAndActAsync(page, "#rememberMe", 
                        async el => await el.CheckAsync(), loginTimeoutMs))
                        return false;
                    
                    if (!await WaitAndActAsync(page, "a.login-btn", 
                        async el => await el.ClickAsync(), loginTimeoutMs))
                        return false;
                }

                // 等待页面导航完成，使用登录超时时间
                try
                {
                    // 使用 DOMContentLoaded 而非 NetworkIdle 可以更快完成
                    await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded, new() { Timeout = loginTimeoutMs });
                    Console.WriteLine("页面基本加载完成，继续等待短暂时间...");
                }
                catch (TimeoutException ex)
                {
                    Console.WriteLine($"页面加载超时（超过{loginTimeoutMs / 1000}秒）: {ex.Message}");
                    Console.WriteLine("继续尝试检查登录状态...");
                }

                // 无论上述等待是否成功，都等待短暂时间并检查登录状态
                await page.WaitForTimeoutAsync(2000);
                Console.WriteLine("开始检查登录结果...");

                // 检查登录计时是否已超过限制时间
                if (loginTimer.ElapsedMilliseconds > loginTimeoutMs)
                {
                    Console.WriteLine($"登录操作已经花费 {loginTimer.ElapsedMilliseconds}ms，超过{loginTimeoutMs / 1000}秒限制");
                    Console.WriteLine("即使超时也尝试检查登录状态...");

                    // 在检查登录状态前尝试等待可能的登录标识元素出现
                    try {
                        // 尝试等待页面上任何表示登录状态的元素，如用户信息区域，超时设置较短
                        await page.WaitForSelectorAsync(".user-info, .dashboard, .profile, .eams-header", new() { Timeout = 2000 });
                        Console.WriteLine("检测到可能的登录相关元素...");
                    } catch (TimeoutException) {
                        // 如果找不到特定元素，可能页面还在加载中或结构不同
                        Console.WriteLine("未检测到明确的登录元素，继续检查登录状态...");
                    }

                    // 即使操作超时也要检查登录状态
                    bool isLoggedIn = await IsLoggedIn(page);
                    if (isLoggedIn)
                    {
                        Console.WriteLine("虽然操作超时，但登录已成功！");
                        return true;
                    }
                    else
                    {
                        Console.WriteLine("操作超时且未检测到登录成功，视为失败");
                        return false;
                    }
                }

                // 检查是否有用户名密码错误信息
                try {
                    // 等待一段时间，确保错误信息有机会显示出来
                    await page.WaitForTimeoutAsync(1000);
                    
                    // 使用多种方法检查密码错误提示
                    var credentialsError = await page.GetByText("您提供的用户名或者密码有误").CountAsync() > 0 ||
                                          await page.Locator(".login-error-msg, .error-message, .alert-error")
                                                   .Filter(new() { HasText = "密码" }).CountAsync() > 0 ||
                                          await page.Locator("div,span,p")
                                                   .Filter(new() { HasText = "用户名或者密码" }).CountAsync() > 0;
                    
                    if (credentialsError)
                    {
                        Console.WriteLine("登录失败: 用户名或密码错误");
                        return false;
                    }
                    
                    // 检查是否有其他登录错误
                    var errorElement = await page.QuerySelectorAsync(".auth_error, .error-message, .alert-danger");
                    if (errorElement != null)
                    {
                        string errorMessage = await errorElement.TextContentAsync() ?? "未知错误";
                        Console.WriteLine($"登录失败: {errorMessage}");
                        return false;
                    }
                    
                    // 验证登录成功
                    bool isLoggedIn = await IsLoggedIn(page);
                    if (isLoggedIn) {
                        Console.WriteLine("登录成功！");
                        return true;
                    } else {
                        Console.WriteLine("登录状态验证失败");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"登录过程中发生错误: {ex.Message}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"登录过程中发生错误: {ex.Message}");
                return false;
            }
        }

        private static async Task<bool> WaitAndActAsync(IPage page, string selector, Func<IElementHandle, Task> action, int timeoutMs)
        {
            try
            {
                var element = await page.WaitForSelectorAsync(selector, new() { Timeout = timeoutMs });
                if (element == null)
                {
                    Console.WriteLine($"元素 {selector} 未找到");
                    return false;
                }
                await action(element);
                return true;
            }
            catch (TimeoutException ex)
            {
                Console.WriteLine($"等待元素 {selector} 超时: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 使用角色选择器等待并操作元素
        /// </summary>
        private static async Task<bool> WaitAndActByRoleAsync(IPage page, AriaRole role, PageGetByRoleOptions options, Func<ILocator, Task> action, int timeoutMs)
        {
            try
            {
                // 创建基于角色的定位器
                var locator = page.GetByRole(role, options);
                
                // 等待元素可见
                await locator.WaitForAsync(new() { Timeout = timeoutMs });
                
                // 执行操作
                await action(locator);
                return true;
            }
            catch (TimeoutException ex)
            {
                Console.WriteLine($"等待元素 {role} 超时: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"与元素 {role} 交互失败: {ex.Message}");
                return false;
            }
        }

        private static async Task<bool> IsLoggedIn(IPage page)
        {
            try
            {
                // 优先使用URL检查登录状态
                string currentUrl = page.Url;
                
                // 检查方法1: 通过URL判断 - 如果包含"eams"则表示登录成功
                if (currentUrl.Contains("eams", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"登录成功: 已重定向到教务系统页面 ({currentUrl})");
                    return true;
                }
                
                // 检查方法2: 通过URL排除登录页面
                if (!currentUrl.EndsWith("tam.nwupl.edu.cn") && 
                    !currentUrl.Contains("login") && 
                    !currentUrl.Contains("auth"))
                {
                    Console.WriteLine($"登录成功: 已离开登录页面 ({currentUrl})");
                    return true;
                }

                // 备用检查: 仅当URL检查不确定时才尝试元素检查
                var userElement = await page.QuerySelectorAsync(".user-info, .profile, .dashboard");
                if (userElement != null)
                {
                    Console.WriteLine($"通过页面元素检测到登录成功，当前URL: {currentUrl}");
                    return true;
                }

                Console.WriteLine($"未能通过URL或元素验证登录状态，当前仍在: {currentUrl}");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"检查登录状态时发生错误: {ex.Message}");
                return false;
            }
        }

        private static async Task<bool> CheckNetworkStatusAsync(IPage page)
        {
            try
            {
                await page.GotoAsync("http://www.bing.com", new() { Timeout = 5000 });
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}