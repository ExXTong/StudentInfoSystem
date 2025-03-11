using Microsoft.Playwright;
using StudentInfoSystem.Common.Models;
using StudentInfoSystem.Common.Services;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace StudentInfoSystem.StudentService.Services
{
    /// <summary>
    /// 学生信息爬虫服务，负责从教务系统获取学生信息
    /// </summary>
    public class StudentInfoCrawlerService : IStudentInfoCrawlerService
    {
        private readonly IBrowserManager _browserManager;
        private readonly IConfiguration _configuration;
        private readonly ILogger<StudentInfoCrawlerService> _logger;
        private readonly SemaphoreSlim _semaphore = new(1, 1); // 用于防止并发爬取

        public StudentInfoCrawlerService(
            IBrowserManager browserManager, 
            IConfiguration configuration, 
            ILogger<StudentInfoCrawlerService> logger)
        {
            _browserManager = browserManager;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// 使用学生自己的凭据获取信息
        /// </summary>
        public async Task<StudentInfo> GetStudentInfoByScrapingAsync(string username, string password)
        {
            IPage page = null;
            try
            {
                // 获取信息前先获取锁，确保同一时间只有一个爬虫任务
                await _semaphore.WaitAsync();

                _logger.LogInformation("开始使用用户 {Username} 凭据获取学生信息", username);
                
                // 获取新的页面实例用于爬取信息
                page = await _browserManager.GetPageAsync();
                _logger.LogInformation("已创建新的浏览器页面用于爬取信息");

                // 使用浏览器管理器登录
                _logger.LogInformation("为爬取页面执行登录操作");
                var loginSuccess = await _browserManager.LoginAsync(username, password, page);

                if (!loginSuccess)
                {
                    _logger.LogWarning("爬取页面登录失败，无法获取学生信息");
                    return null;
                }

                // 登录成功后的爬取逻辑
                try
                {
                    // 刷新页面确保获取最新状态
                    _logger.LogInformation("刷新页面获取最新状态");
                    await page.ReloadAsync(new PageReloadOptions { WaitUntil = WaitUntilState.NetworkIdle });

                    // 访问学籍信息页面
                    _logger.LogInformation("访问学籍信息页面");
                    await page.GotoAsync("https://tam.nwupl.edu.cn/eams/homeExt.action", 
                        new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
                    
                    // 这里开始实际的信息爬取逻辑
                    _logger.LogInformation("开始爬取学生信息");

                    // 等待并点击"我的"链接
                    _logger.LogInformation("等待'我的'链接可用");
                    var myLink = page.GetByRole(AriaRole.Link, new() { Name = "我的", Exact = true });
                    await myLink.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
                    
                    _logger.LogInformation("点击'我的'按钮");
                    await myLink.ClickAsync();
                    
                    // 等待"学籍信息"链接出现并点击
                    _logger.LogInformation("等待'学籍信息'链接可用");
                    var studentInfoLink = page.GetByRole(AriaRole.Link, new() { Name = "学籍信息" });
                    await studentInfoLink.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
                    
                    _logger.LogInformation("点击'学籍信息'链接");
                    await studentInfoLink.ClickAsync();
                    await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                    
                    // 主动关闭下拉菜单，点击页面空白区域
                    _logger.LogInformation("关闭下拉菜单");
                    var mainTop = page.Locator("#main-top");
                    await mainTop.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
                    await mainTop.ClickAsync();

                    // 等待iframe加载完成
                    await page.WaitForSelectorAsync("iframe[name=\"iframeMain\"]");
                    
                    // 获取iframe内容
                    var frame = page.Frame("iframeMain");
                    if (frame == null)
                    {
                        _logger.LogError("无法找到学籍信息iframe，请检查网页结构是否有变化");
                        return null;
                    }
                    
                    // 获取学生基本信息页面
                    _logger.LogInformation("正在获取学生基本信息");
                    await frame.GetByRole(AriaRole.Link, new() { Name = "学生基本信息" }).ClickAsync();
                    await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                    
                    // 获取HTML内容
                    var basicInfoHtml = await frame.EvaluateAsync<string>("() => document.documentElement.outerHTML");
                    
                    // 使用解析器解析学生信息
                    _logger.LogInformation("解析学生基本信息");
                    var studentInfo = StudentInfoParser.ParseStudentInfoFromHtml(basicInfoHtml);

                    if (studentInfo != null)
                    {
                        // 确保学号与用户名一致
                        if (string.IsNullOrEmpty(studentInfo.StudentId))
                        {
                            studentInfo.StudentId = username;
                        }
                        
                        _logger.LogInformation("成功获取学生 {Username} 信息", username);
                    }
                    else
                    {
                        _logger.LogWarning("未能成功解析学生信息");
                        studentInfo = new StudentInfo { StudentId = username };
                    }
                    
                    return studentInfo;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "获取学生信息时发生错误: {Message}", ex.Message);
                    return null;
                }
                finally
                {
                    // 爬取完成后执行注销
                    _logger.LogInformation("执行注销操作");
                    try
                    {
                        await LogoutAsync(page);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "注销时发生错误: {Message}", ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "爬取学生信息时发生异常: {Message}", ex.Message);
                return null;
            }
            finally
            {
                if (page != null)
                {
                    // 释放浏览器页面
                    await _browserManager.ReleaseBrowserAsync(page);
                    _logger.LogInformation("已释放浏览器页面");
                }
                
                _semaphore.Release();
            }
        }

        /// <summary>
        /// 执行注销操作
        /// </summary>
        private async Task LogoutAsync(IPage page)
        {
            try
            {
                // 尝试通过直接调用浏览器管理器的注销方法
                var logoutSuccess = await _browserManager.LogoutAsync(page);
                
                if (!logoutSuccess)
                {
                    _logger.LogInformation("未找到注销按钮，尝试访问注销URL");
                    // 如果找不到注销链接，直接访问注销URL
                    await page.GotoAsync("https://tam.nwupl.edu.cn/eams/logout.action", 
                        new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "执行注销操作失败: {Message}", ex.Message);
                throw; // 重新抛出异常以便上层处理
            }
        }
    }
}