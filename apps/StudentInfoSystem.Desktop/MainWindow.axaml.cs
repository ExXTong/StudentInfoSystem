using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using StudentInfoSystem.Portal.Services;
using StudentInfoSystem.Core.Session;
using StudentInfoSystem.Core.Storage;
using StudentInfoSystem.Portal;
using System.Text.RegularExpressions;

namespace StudentInfoSystem.Desktop;

public partial class MainWindow : Window
{
    private readonly LocalDataStore _store = new();
    private readonly SecureCredentialStore _encryptedStore = new();
    private readonly AppSettings _settings = AppSettings.Load();
    private readonly PortalSession _session;

    public MainWindow()
    {
        InitializeComponent();
        _session = new PortalSession(CreateOptions(), _store, _encryptedStore);
        FetchButton.IsEnabled = false;
        FetchButton.Click += OnFetchClick;
        SettingsButton.Click += OnSettingsClick;
        ExportButton.Click += OnExportClick;
        ScheduleButton.Click += OnScheduleClick;
        ClearCacheButton.Click += OnClearCacheClick;
    }

    private StudentPortalOptions CreateOptions()
    {
        return new StudentPortalOptions
        {
            AuthServerBaseUrl = _settings.AuthServerBaseUrl,
            EamsBaseUrl = _settings.EamsBaseUrl,
            ServiceUrl = _settings.ServiceUrl,
            Proxy = _settings.Proxy,
            TimeoutSeconds = 60,
            FingerprintCookies = _settings.FingerprintCookies,
            UserAgent = _settings.UserAgent
        };
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
            var result = await _session.LoginAsync(username, password);
            Title = result.Success ? $"学生信息服务 - {username}" : "学生信息服务";
            StatusText.Text = result.Message;
            FetchButton.IsEnabled = result.Success;
            ScheduleButton.IsEnabled = result.Success;
            if (result.Success)
            {
                LoadLocalCache(username);
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

    private async void OnFetchClick(object? sender, RoutedEventArgs e)
    {
        if (!_session.IsLoggedIn)
        {
            StatusText.Text = "请先登录";
            return;
        }

        StatusText.Text = "正在获取数据...";
        FetchButton.IsEnabled = false;
        try
        {
            await _session.EnsurePortalLoginAsync();
            var portal = _session.Portal;
            var gradeHtml = await portal.GetHistoryGradeAsync();
            var grades = GradeParser.ParseGrades(gradeHtml ?? "");

            var detailHtml = await portal.GetStudentDetailAsync();
            var student = StudentInfoParser.ParseStudentInfoFromHtml(detailHtml ?? "");

            ProfileText.Text = student == null
                ? "学生信息获取失败"
                : $"姓名：{student.Name}  学号：{student.StudentId}\n性别：{student.Gender}  院系：{student.Department}\n专业：{student.Major}  班级：{student.Class}";
            DataList.ItemsSource = grades
                .Select(g => $"{g.CourseName}  |  {g.GradeValue}  |  {g.Credits}学分")
                .ToList();

            var username = UsernameBox.Text?.Trim() ?? "";
            if (student != null)
            {
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

    private async void OnScheduleClick(object? sender, RoutedEventArgs e)
    {
        if (!_session.IsLoggedIn)
        {
            StatusText.Text = "请先登录";
            return;
        }

        StatusText.Text = "正在获取课表...";
        ScheduleButton.IsEnabled = false;
        try
        {
            await _session.EnsurePortalLoginAsync();
            var portal = _session.Portal;
            var page = await portal.GetCourseTablePageAsync();
            var semMatch = Regex.Match(page ?? "", @"name=""semester.id""\s+value=""([^""]*)""");
            var semesterId = semMatch.Success ? semMatch.Groups[1].Value : "194";

            var idsMatch = Regex.Match(page ?? "", @"bg\.form\.addInput\(form,""ids"",""(\d+)""\)");
            var ids = idsMatch.Success ? idsMatch.Groups[1].Value : "676535";

            var html = await portal.GetCourseTableDataAsync(semesterId, "1", ids, "std");
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

    private void OnClearCacheClick(object? sender, RoutedEventArgs e)
    {
        var username = UsernameBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(username))
        {
            StatusText.Text = "请先输入学号";
            return;
        }

        _session.Clear();
        DataList.ItemsSource = null;
        ScheduleList.ItemsSource = null;
        ProfileText.Text = "";
        StatusText.Text = "本地缓存和凭据已清除";
    }

    private void OnSettingsClick(object? sender, RoutedEventArgs e)
    {
        var window = new SettingsWindow(_settings);
        window.ShowDialog(this);
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
