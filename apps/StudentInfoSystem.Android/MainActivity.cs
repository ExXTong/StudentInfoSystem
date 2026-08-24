using Android.App;
using Android.OS;
using Android.Widget;
using Microsoft.Extensions.Logging.Abstractions;
using StudentInfoSystem.Core.Portal;
using StudentInfoSystem.Core.Services;
using System.Text.RegularExpressions;
using StudentInfoSystem.Core.Storage;

namespace StudentInfoSystem.Android;

[Activity(Label = "学生信息服务", MainLauncher = true)]
public class MainActivity : Activity
{
    private EditText? _username;
    private EditText? _password;
    private TextView? _status;
    private Button? _loginButton;
    private Button? _fetchButton;
    private Button? _settingsButton;
    private IStudentPortalClient? _portal;
    private bool _loggedIn;
    private bool _portalLoggedIn;
    private string? _sessionUsername;
    private string? _sessionPassword;
    private readonly LocalDataStore _store = new();
    private readonly SecureCredentialStore _encryptedStore;
    private ListView? _listView;
    private Button? _exportButton;
    private Button? _scheduleButton;
    private Button? _clearButton;
    private string? _lastGradeText;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        _encryptedStore = new SecureCredentialStore(this);

        var layout = new LinearLayout(this)
        {
            Orientation = Orientation.Vertical,
            Padding = new Android.Graphics.Rect(40, 40, 40, 40)
        };

        var title = new TextView(this) { Text = "学生信息服务", TextSize = 24 };
        _username = new EditText(this) { Hint = "学号" };
        _password = new EditText(this) { Hint = "密码", InputType = Android.Text.InputTypes.TextVariationPassword };
        _loginButton = new Button(this) { Text = "登录" };
        _fetchButton = new Button(this) { Text = "获取数据", Enabled = false };
        _settingsButton = new Button(this) { Text = "设置" };
        _exportButton = new Button(this) { Text = "导出成绩" };
        _scheduleButton = new Button(this) { Text = "获取课表" };
        _clearButton = new Button(this) { Text = "清除缓存" };
        _status = new TextView(this) { Text = "未登录" };
        _listView = new ListView(this);

        layout.AddView(title);
        layout.AddView(_username);
        layout.AddView(_password);
        layout.AddView(_loginButton);
        layout.AddView(_fetchButton);
        layout.AddView(_settingsButton);
        layout.AddView(_exportButton);
        layout.AddView(_scheduleButton);
        layout.AddView(_clearButton);
        layout.AddView(_status);
        layout.AddView(_listView, new LinearLayout.LayoutParams(Android.ViewGroup.LayoutParams.MatchParent, 0, 1f));

        _loginButton.Click += OnLoginClick;
        _fetchButton.Click += OnFetchClick;
        _settingsButton.Click += OnSettingsClick;
        _exportButton.Click += OnExportClick;
        _scheduleButton.Click += OnScheduleClick;
        _clearButton.Click += OnClearClick;

        var scrollView = new ScrollView(this);
        scrollView.AddView(layout);
        SetContentView(scrollView);
    }

    private async void OnLoginClick(object? sender, System.EventArgs e)
    {
        var username = _username?.Text?.Trim() ?? "";
        var password = _password?.Text ?? "";

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            _status!.Text = "请输入学号和密码";
            return;
        }

        _status!.Text = "正在验证...";
        _loginButton!.Enabled = false;

        try
        {
            if (_store.VerifyLocalCredentials(username, password))
            {
                _loggedIn = true;
                _portalLoggedIn = false;
                _fetchButton!.Enabled = true;
                _scheduleButton!.Enabled = true;
                _sessionUsername = username;
                _sessionPassword = password;
                Title = $"学生信息服务 - {username}";
                _status.Text = "本地登录成功";
                return;
            }

            var options = new StudentPortalOptions
            {
                AuthServerBaseUrl = "https://authserver.nwupl.edu.cn",
                EamsBaseUrl = "https://tam.nwupl.edu.cn",
                ServiceUrl = "https://tam.nwupl.edu.cn/eams/homeExt.action",
                Proxy = GetPrefs("proxy", ""),
                TimeoutSeconds = 60,
                FingerprintCookies = GetPrefs("cookie", ""),
                UserAgent = "Mozilla/5.0 (Linux; Android 14) AppleWebKit/537.36 Chrome/151.0.0.0 Mobile Safari/537.36"
            };

            _portal = new HttpStudentPortalClient(options, NullLogger<HttpStudentPortalClient>.Instance);
            var ok = await _portal.LoginAsync(username, password);
            if (ok)
            {
                _store.SaveCredentials(username, password);
                _encryptedStore.SavePassword(username, password);
                _loggedIn = true;
                _portalLoggedIn = true;
                _fetchButton!.Enabled = true;
                _scheduleButton!.Enabled = true;
                _sessionUsername = username;
                _sessionPassword = password;
                Title = $"学生信息服务 - {username}";
                _status.Text = "登录成功，已保存本地凭据";
            }
            else
            {
                _status.Text = "登录失败，请检查账号密码";
            }
        }
        catch (System.Exception ex)
        {
            _status.Text = $"登录失败：{ex.Message}";
        }
        finally
        {
            _loginButton.Enabled = true;
        }

    private async System.Threading.Tasks.Task EnsurePortalLoginAsync()
    {
        if (_portalLoggedIn) return;

        if (string.IsNullOrEmpty(_sessionUsername) || string.IsNullOrEmpty(_sessionPassword))
        {
            var saved = _encryptedStore.LoadPassword();
            if (saved == null)
            {
                throw new System.Exception("未找到可用凭据，请先登录");
            }
            _sessionUsername = saved.Value.Username;
            _sessionPassword = saved.Value.Password;
        }

        if (_portal == null)
        {
            var options = new StudentPortalOptions
            {
                AuthServerBaseUrl = "https://authserver.nwupl.edu.cn",
                EamsBaseUrl = "https://tam.nwupl.edu.cn",
                ServiceUrl = "https://tam.nwupl.edu.cn/eams/homeExt.action",
                Proxy = GetPrefs("proxy", ""),
                TimeoutSeconds = 60,
                FingerprintCookies = GetPrefs("cookie", ""),
                UserAgent = "Mozilla/5.0 (Linux; Android 14) AppleWebKit/537.36 Chrome/151.0.0.0 Mobile Safari/537.36"
            };
            _portal = new HttpStudentPortalClient(options, NullLogger<HttpStudentPortalClient>.Instance);
        }

        var ok = await _portal.LoginAsync(_sessionUsername, _sessionPassword);
        if (!ok)
        {
            throw new System.Exception("教务系统登录失败，请重新验证账号密码");
        }
        _portalLoggedIn = true;
    }

    private async void OnFetchClick(object? sender, System.EventArgs e)
    {
        if (_portal == null || !_loggedIn)
        {
            _status!.Text = "请先登录";
            return;
        }

        _status!.Text = "正在获取数据...";
        _fetchButton!.Enabled = false;
        try
        {
            await EnsurePortalLoginAsync();
            var gradeHtml = await _portal!.GetHistoryGradeAsync();
            var grades = GradeParser.ParseGrades(gradeHtml ?? "");

            var detailHtml = await _portal.GetStudentDetailAsync();
            var student = StudentInfoParser.ParseStudentInfoFromHtml(detailHtml ?? "");

            _status.Text = student == null
                ? $"成绩 {grades.Count} 门，学生：未知"
                : $"成绩 {grades.Count} 门\n姓名：{student.Name}\n院系：{student.Department}\n专业：{student.Major}";
            _lastGradeText = string.Join("\n", grades.Select(g => $"{g.CourseName} - {g.GradeValue}"));
            _listView!.Adapter = new ArrayAdapter(this, Android.Resource.Layout.SimpleListItem1,
                grades.Select(g => $"{g.CourseName} - {g.GradeValue}").ToArray());

            var uname = _username?.Text?.Trim() ?? "";
            if (student != null)
            {
                _store.SaveStudentInfo(uname, student);
                _store.SaveGrades(uname, grades);
            }
        }
        catch (System.Exception ex)
        {
            _status.Text = $"获取数据失败：{ex.Message}";
        }
        finally
        {
            _fetchButton.Enabled = true;
        }

    private string GetPrefs(string key, string def)
    {
        var prefs = GetSharedPreferences("settings", FileCreationMode.Private);
        return prefs?.GetString(key, def) ?? def;
    }

    private void SavePrefs(string key, string value)
    {
        var prefs = GetSharedPreferences("settings", FileCreationMode.Private);
        prefs?.Edit()?.PutString(key, value)?.Apply();
    }

    private void OnSettingsClick(object? sender, System.EventArgs e)
    {
        var proxyInput = new EditText(this) { Hint = "代理地址", Text = GetPrefs("proxy", "") };
        var cookieInput = new EditText(this) { Hint = "浏览器指纹 Cookie", Text = GetPrefs("cookie", "") };

        var dialog = new Android.App.AlertDialog.Builder(this);
        dialog.SetTitle("设置");
        var settingsLayout = new LinearLayout(this)
        {
            Orientation = Orientation.Vertical,
            Padding = new Android.Graphics.Rect(40, 20, 40, 20)
        };
        settingsLayout.AddView(proxyInput);
        settingsLayout.AddView(cookieInput);
        dialog.SetView(settingsLayout);
        dialog.SetPositiveButton("保存", (s, e2) =>
        {
            SavePrefs("proxy", proxyInput.Text?.Trim() ?? "");
            SavePrefs("cookie", cookieInput.Text?.Trim() ?? "");
        });
        dialog.SetNegativeButton("取消", (s, e2) => { });
        dialog.Show();
    }

    private void OnExportClick(object? sender, System.EventArgs e)
    {
        if (string.IsNullOrEmpty(_lastGradeText))
        {
            _status!.Text = "没有可导出的成绩";
            return;
        }

        var sendIntent = new Android.Content.Intent(Android.Content.Intent.ActionSend);
        sendIntent.SetType("text/plain");
        sendIntent.PutExtra(Android.Content.Intent.ExtraText, _lastGradeText);
        StartActivity(Android.Content.Intent.CreateChooser(sendIntent, "导出成绩"));
    }

    private async void OnScheduleClick(object? sender, System.EventArgs e)
    {
        if (_portal == null || !_loggedIn)
        {
            _status!.Text = "请先登录";
            return;
        }

        _status!.Text = "正在获取课表...";
        _scheduleButton!.Enabled = false;
        try
        {
            await EnsurePortalLoginAsync();
            var page = await _portal!.GetCourseTablePageAsync();
            var semMatch = Regex.Match(page ?? "", @"name=""semester.id""\s+value=""([^""]*)""");
            var semesterId = semMatch.Success ? semMatch.Groups[1].Value : "194";
            var idsMatch = Regex.Match(page ?? "", @"bg\.form\.addInput\(form,""ids"",""(\d+)""\)");
            var ids = idsMatch.Success ? idsMatch.Groups[1].Value : "676535";
            var html = await _portal.GetCourseTableDataAsync(semesterId, "1", ids, "std");
            var list = SimpleScheduleParser.ParseScheduleSummary(html ?? "");
            _status.Text = $"课表课程 {list.Count} 条";
            _listView!.Adapter = new ArrayAdapter(this, Android.Resource.Layout.SimpleListItem1,
                list.Count == 0 ? new[] { "未解析到课程数据" } : list.ToArray());
        }
        catch (System.Exception ex)
        {
            _status.Text = $"获取课表失败：{ex.Message}";
        }
        finally
        {
            _scheduleButton.Enabled = true;
        }
    }

    private void OnClearClick(object? sender, System.EventArgs e)
    {
        var uname = _username?.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(uname))
        {
            _status!.Text = "请先输入学号";
            return;
        }

        _store.ClearUserData(uname);
        _encryptedStore.Clear();
        _sessionUsername = null;
        _sessionPassword = null;
        _loggedIn = false;
        _portalLoggedIn = false;
        _listView!.Adapter = null;
        _status.Text = "本地缓存和内存凭据已清除";
    }
    }
}
