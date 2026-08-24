using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging.Abstractions;
using StudentInfoSystem.Core.Portal;
using StudentInfoSystem.Core.Services;
using StudentInfoSystem.Core.Storage;

namespace StudentInfoSystem.Desktop;

public partial class MainWindow : Window
{
    private IStudentPortalClient? _portal;
    private bool _loggedIn;
    private bool _portalLoggedIn;
    private string? _sessionUsername;
    private string? _sessionPassword;
    private readonly LocalDataStore _store = new();
    private readonly SecureCredentialStore _encryptedStore = new();
    private readonly AppSettings _settings = AppSettings.Load();

    public MainWindow()
    {
        InitializeComponent();
        FetchButton.IsEnabled = false;
        FetchButton.Click += OnFetchClick;
        SettingsButton.Click += OnSettingsClick;
        ExportButton.Click += OnExportClick;
        ScheduleButton.Click += OnScheduleClick;
        ClearCacheButton.Click += OnClearCacheClick;
    }

    private async void OnLoginClick(object? sender, RoutedEventArgs e)
    {
        var username = UsernameBox.Text?.Trim() ?? "";
        var password = PasswordBox.Text ?? "";

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            StatusText.Text = "请输入学号和密码";
            return;
        }

        StatusText.Text = "正在验证...";
        LoginButton.IsEnabled = false;

        try
        {
            if (_store.VerifyLocalCredentials(username, password))
            {
                _loggedIn = true;
                _portalLoggedIn = false;
                FetchButton.IsEnabled = true;
                ScheduleButton.IsEnabled = true;
                Title = $"学生信息服务 - {username}";
                _sessionUsername = username;
                _sessionPassword = password;
                StatusText.Text = "本地登录成功";
                LoadLocalCache(username);
                return;
            }

            var options = new StudentPortalOptions
            {
                AuthServerBaseUrl = _settings.AuthServerBaseUrl,
                EamsBaseUrl = _settings.EamsBaseUrl,
                ServiceUrl = _settings.ServiceUrl,
                Proxy = _settings.Proxy,
                TimeoutSeconds = 60,
                FingerprintCookies = _settings.FingerprintCookies,
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/151.0.0.0 Safari/537.36 Edg/151.0.0.0"
            };

            _portal = new HttpStudentPortalClient(options, NullLogger<HttpStudentPortalClient>.Instance);
            var ok = await _portal.LoginAsync(username, password);
            if (ok)
            {
                _store.SaveCredentials(username, password);
                _encryptedStore.SavePassword(username, password);
                _loggedIn = true;
                _portalLoggedIn = true;
                FetchButton.IsEnabled = true;
                ScheduleButton.IsEnabled = true;
                _sessionUsername = username;
                _sessionPassword = password;
                Title = $"学生信息服务 - {username}";
                StatusText.Text = "登录成功，已保存本地凭据";
                LoadLocalCache(username);
            }
            else
            {
                StatusText.Text = "登录失败，请检查账号密码";
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"登录失败：{ex.Message}";
        }
        finally
        {
            LoginButton.IsEnabled = true;
        }
    }

    private void LoadLocalCache(string username)
    {
        var cachedGrades = _store.LoadGrades(username);
        var cachedProfile = _store.LoadStudentInfo(username);
        if (cachedGrades != null || cachedProfile != null)
        {
            ProfileText.Text = cachedProfile == null
                ? "已加载本地缓存"
                : $"缓存：{cachedProfile.Name}，成绩 {cachedGrades?.Count ?? 0} 门";
            DataList.ItemsSource = cachedGrades?
                .Select(g => $"{g.CourseName}  |  {g.GradeValue}  |  {g.Credits}学分")
                .ToList() ?? new List<string>();
        }
    }

    private async Task EnsurePortalLoginAsync()
    {
        if (_portalLoggedIn) return;

        if (string.IsNullOrEmpty(_sessionUsername) || string.IsNullOrEmpty(_sessionPassword))
        {
            var saved = _encryptedStore.LoadPassword();
            if (saved == null)
            {
                throw new InvalidOperationException("未找到可用凭据，请先登录");
            }
            _sessionUsername = saved.Value.Username;
            _sessionPassword = saved.Value.Password;
        }

        if (_portal == null)
        {
            var options = new StudentPortalOptions
            {
                AuthServerBaseUrl = _settings.AuthServerBaseUrl,
                EamsBaseUrl = _settings.EamsBaseUrl,
                ServiceUrl = _settings.ServiceUrl,
                Proxy = _settings.Proxy,
                TimeoutSeconds = 60,
                FingerprintCookies = _settings.FingerprintCookies,
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/151.0.0.0 Safari/537.36 Edg/151.0.0.0"
            };
            _portal = new HttpStudentPortalClient(options, NullLogger<HttpStudentPortalClient>.Instance);
        }

        var ok = await _portal.LoginAsync(_sessionUsername, _sessionPassword);
        if (!ok)
        {
            throw new InvalidOperationException("教务系统登录失败，请重新验证账号密码");
        }
        _portalLoggedIn = true;
    }

    private async void OnFetchClick(object? sender, RoutedEventArgs e)
    {
        if (!_loggedIn)
        {
            StatusText.Text = "请先登录";
            return;
        }

        StatusText.Text = "正在获取数据...";
        await EnsurePortalLoginAsync();
        FetchButton.IsEnabled = false;
        try
        {
            var gradeHtml = await _portal!.GetHistoryGradeAsync();
            var grades = GradeParser.ParseGrades(gradeHtml ?? "");

            var detailHtml = await _portal.GetStudentDetailAsync();
            var student = StudentInfoParser.ParseStudentInfoFromHtml(detailHtml ?? "");

            ProfileText.Text = student == null
                ? "学生信息获取失败"
                : $"姓名：{student.Name}  学号：{student.StudentId}\n性别：{student.Gender}  院系：{student.Department}\n专业：{student.Major}  班级：{student.Class}";
            DataList.ItemsSource = grades
                .Select(g => $"{g.CourseName}  |  {g.GradeValue}  |  {g.Credits}学分")
                .ToList();

            if (student != null)
            {
                var username = UsernameBox.Text?.Trim() ?? "";
                _store.SaveStudentInfo(username, student);
                _store.SaveGrades(username, grades);
            }

            StatusText.Text = $"成绩 {grades.Count} 门";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"获取数据失败：{ex.Message}";
        }
        finally
        {
            FetchButton.IsEnabled = true;
        }
    }

    private void OnSettingsClick(object? sender, RoutedEventArgs e)
    {
        var window = new SettingsWindow(_settings);
        window.ShowDialog(this);
    }
    private void OnClearCacheClick(object? sender, RoutedEventArgs e)
    {
        var username = UsernameBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(username))
        {
            StatusText.Text = "请先输入学号";
            return;
        }

        _store.ClearUserData(username);
        _encryptedStore.Clear();
        DataList.ItemsSource = null;
        ProfileText.Text = "";
        StatusText.Text = "本地缓存已清除";
    }

    private async void OnScheduleClick(object? sender, RoutedEventArgs e)
    {
        if (!_loggedIn)
        {
            StatusText.Text = "请先登录";
            return;
        }

        StatusText.Text = "正在获取课表...";
        await EnsurePortalLoginAsync();
        ScheduleButton.IsEnabled = false;
        try
        {
            var page = await _portal!.GetCourseTablePageAsync();
            var semMatch = Regex.Match(page ?? "", @"name=""semester.id""\s+value=""([^""]*)""");
            var semesterId = semMatch.Success ? semMatch.Groups[1].Value : "194";

            var idsMatch = Regex.Match(page ?? "", @"bg\.form\.addInput\(form,""ids"",""(\d+)""\)");
            var ids = idsMatch.Success ? idsMatch.Groups[1].Value : "676535";

            var html = await _portal.GetCourseTableDataAsync(semesterId, "1", ids, "std");
            var list = SimpleScheduleParser.ParseScheduleSummary(html ?? "");

            ScheduleList.ItemsSource = list.Count == 0
                ? new List<string> { "未解析到课程数据" }
                : list;
            StatusText.Text = $"课表课程 {list.Count} 条";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"获取课表失败：{ex.Message}";
        }
        finally
        {
            ScheduleButton.IsEnabled = true;
        }
    }

    private async void OnExportClick(object? sender, RoutedEventArgs e)
    {
        var items = DataList.ItemsSource?.Cast<string>().ToList() ?? new List<string>();
        if (items.Count == 0)
        {
            StatusText.Text = "没有可导出的成绩";
            return;
        }

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "导出成绩",
            DefaultExtension = "txt",
            FileTypeChoices = new[] { new FilePickerFileType("文本文件") { Patterns = new[] { "*.txt" } } }
        });
        if (file != null)
        {
            await using var stream = await file.OpenWriteAsync();
            await using var writer = new StreamWriter(stream);
            foreach (var item in items)
            {
                await writer.WriteLineAsync(item);
            }
            StatusText.Text = "成绩已导出";
        }
    }

}
