using Microsoft.Playwright;
using System.Threading.Tasks;

namespace StudentInfoSystem.Common.Services
{
    /// <summary>
    /// 浏览器管理器接口
    /// </summary>
    public interface IBrowserManager
    {
        /// <summary>
        /// 获取一个可用的Playwright页面实例
        /// </summary>
        /// <returns>Playwright页面实例</returns>
        Task<IPage> GetPageAsync();
        
        /// <summary>
        /// 使用提供的凭据登录系统
        /// </summary>
        /// <param name="username">用户名</param>
        /// <param name="password">密码</param>
        /// <param name="page">页面实例</param>
        /// <returns>是否登录成功</returns>
        Task<bool> LoginAsync(string username, string password, IPage page);
        
        /// <summary>
        /// 从系统中注销
        /// </summary>
        /// <param name="page">要使用的页面实例</param>
        /// <returns>是否注销成功</returns>
        Task<bool> LogoutAsync(IPage page);
        
        /// <summary>
        /// 释放页面实例回到池中
        /// </summary>
        /// <param name="page">要释放的页面</param>
        Task ReleaseBrowserAsync(IPage page);
        
        /// <summary>
        /// 初始化浏览器池
        /// </summary>
        Task Initialize();
        
        /// <summary>
        /// 释放所有资源
        /// </summary>
        Task Dispose();
    }
}