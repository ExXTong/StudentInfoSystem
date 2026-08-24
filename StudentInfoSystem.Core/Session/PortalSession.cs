using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using StudentInfoSystem.Portal;
using StudentInfoSystem.Core.Security;
using StudentInfoSystem.Core.Storage;

namespace StudentInfoSystem.Core.Session;

public class PortalSession
{
    private readonly StudentPortalOptions _options;
    private readonly LocalDataStore _store;
    private readonly ISecureCredentialStore _secureStore;
    private IStudentPortalClient? _portal;
    private string? _sessionUsername;
    private string? _sessionPassword;
    private bool _loggedIn;
    private bool _portalLoggedIn;

    public PortalSession(StudentPortalOptions options, LocalDataStore store, ISecureCredentialStore secureStore)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _secureStore = secureStore ?? throw new ArgumentNullException(nameof(secureStore));
    }

    public bool IsLoggedIn => _loggedIn;

    public async Task<(bool Success, string Message)> LoginAsync(string username, string password)
    {
        if (_store.VerifyLocalCredentials(username, password))
        {
            _sessionUsername = username;
            _sessionPassword = password;
            _loggedIn = true;
            _portalLoggedIn = false;
            return (true, "本地登录成功");
        }

        _portal = new HttpStudentPortalClient(_options, NullLogger<HttpStudentPortalClient>.Instance);
        var ok = await _portal.LoginAsync(username, password);
        if (!ok)
        {
            return (false, "登录失败，请检查账号密码");
        }

        _store.SaveCredentials(username, password);
        _secureStore.SavePassword(username, password);
        _sessionUsername = username;
        _sessionPassword = password;
        _loggedIn = true;
        _portalLoggedIn = true;
        return (true, "登录成功，已保存本地凭据");
    }

    public async Task EnsurePortalLoginAsync()
    {
        if (_portalLoggedIn) return;

        if (string.IsNullOrEmpty(_sessionUsername) || string.IsNullOrEmpty(_sessionPassword))
        {
            var saved = _secureStore.LoadPassword();
            if (saved == null)
            {
                throw new InvalidOperationException("未找到可用凭据，请先登录");
            }
            _sessionUsername = saved.Value.Username;
            _sessionPassword = saved.Value.Password;
        }

        if (_portal == null)
        {
            _portal = new HttpStudentPortalClient(_options, NullLogger<HttpStudentPortalClient>.Instance);
        }

        var ok = await _portal.LoginAsync(_sessionUsername, _sessionPassword);
        if (!ok)
        {
            throw new InvalidOperationException("教务系统登录失败，请重新验证账号密码");
        }
        _portalLoggedIn = true;
    }

    public IStudentPortalClient Portal
    {
        get
        {
            if (_portal == null)
            {
                throw new InvalidOperationException("Portal 尚未初始化");
            }
            return _portal;
        }
    }

    public void Clear()
    {
        _store.ClearUserData(_sessionUsername ?? "default");
        _secureStore.Clear();
        _sessionUsername = null;
        _sessionPassword = null;
        _loggedIn = false;
        _portalLoggedIn = false;
    }
}
