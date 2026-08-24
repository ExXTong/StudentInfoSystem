using Foundation;
using UIKit;
using Microsoft.Extensions.Logging.Abstractions;
using StudentInfoSystem.Portal;
using StudentInfoSystem.Core.Services;
using System.Text.RegularExpressions;
using StudentInfoSystem.Core.Storage;

namespace StudentInfoSystem.iOS;

public class Application
{
    static void Main(string[] args)
    {
        UIApplication.Main(args, null, typeof(AppDelegate));
    }
}

public class AppDelegate : UIApplicationDelegate
{
    public override UIWindow? Window { get; set; }

    public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
    {
        Window = new UIWindow(UIScreen.MainScreen.Bounds);
        Window.RootViewController = new ViewController();
        Window.MakeKeyAndVisible();
        return true;
    }
}

public class ViewController : UIViewController
{
    private UITextField? _username;
    private UITextField? _password;
    private UILabel? _status;
    private UIButton? _loginButton;
    private UIButton? _fetchButton;
    private UIButton? _settingsButton;
    private IStudentPortalClient? _portal;
    private bool _loggedIn;
    private bool _portalLoggedIn;
    private string? _sessionUsername;
    private string? _sessionPassword;
    private string? _lastGradeText;
    private UIButton? _exportButton;
    private UIButton? _scheduleButton;
    private UIButton? _clearButton;
    private readonly LocalDataStore _store = new();
    private readonly SecureCredentialStore _encryptedStore = new();
    private UITextView? _textView;

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();

        var label = new UILabel
        {
            Text = "学生信息服务",
            Font = UIFont.BoldSystemFontOfSize(28),
            TextAlignment = UITextAlignment.Center,
            Frame = new CoreGraphics.CGRect(0, 100, View.Bounds.Width, 50)
        };

        _username = new UITextField
        {
            Placeholder = "学号",
            Frame = new CoreGraphics.CGRect(40, 180, View.Bounds.Width - 80, 44),
            BorderStyle = UITextBorderStyle.RoundedRect
        };

        _password = new UITextField
        {
            Placeholder = "密码",
            SecureTextEntry = true,
            Frame = new CoreGraphics.CGRect(40, 240, View.Bounds.Width - 80, 44),
            BorderStyle = UITextBorderStyle.RoundedRect
        };

        _loginButton = new UIButton(UIButtonType.System)
        {
            Frame = new CoreGraphics.CGRect(40, 310, View.Bounds.Width - 80, 44)
        };
        _loginButton.SetTitle("登录", UIControlState.Normal);
        _loginButton.TouchUpInside += OnLoginClick;

        _fetchButton = new UIButton(UIButtonType.System)
        {
            Frame = new CoreGraphics.CGRect(40, 370, View.Bounds.Width - 80, 44),
            Enabled = false
        };
        _fetchButton.SetTitle("获取数据", UIControlState.Normal);
        _fetchButton.TouchUpInside += OnFetchClick;

        _scheduleButton = new UIButton(UIButtonType.System)
        {
            Frame = new CoreGraphics.CGRect(40, 430, View.Bounds.Width - 80, 44)
        };
        _scheduleButton.SetTitle("获取课表", UIControlState.Normal);
        _scheduleButton.TouchUpInside += OnScheduleClick;

        _exportButton = new UIButton(UIButtonType.System)
        {
            Frame = new CoreGraphics.CGRect(40, 490, View.Bounds.Width - 80, 44)
        };
        _exportButton.SetTitle("导出成绩", UIControlState.Normal);
        _exportButton.TouchUpInside += OnExportClick;

        _clearButton = new UIButton(UIButtonType.System)
        {
            Frame = new CoreGraphics.CGRect(40, 550, View.Bounds.Width - 80, 44)
        };
        _clearButton.SetTitle("清除缓存", UIControlState.Normal);
        _clearButton.TouchUpInside += OnClearClick;

        _settingsButton = new UIButton(UIButtonType.System)
        {
            Frame = new CoreGraphics.CGRect(40, 610, View.Bounds.Width - 80, 44)
        };
        _settingsButton.SetTitle("设置", UIControlState.Normal);
        _settingsButton.TouchUpInside += OnSettingsClick;

        _status = new UILabel
        {
            Text = "未登录",
            TextAlignment = UITextAlignment.Center,
            Frame = new CoreGraphics.CGRect(40, 670, View.Bounds.Width - 80, 44)
        };

        _textView = new UITextView
        {
            Frame = new CoreGraphics.CGRect(40, 730, View.Bounds.Width - 80, 220),
            Editable = false,
            Font = UIFont.SystemFontOfSize(14)
        };

        View.AddSubview(label);
        View.AddSubview(_username);
        View.AddSubview(_password);
        View.AddSubview(_loginButton);
        View.AddSubview(_fetchButton);
        View.AddSubview(_scheduleButton);
        View.AddSubview(_exportButton);
        View.AddSubview(_clearButton);
        View.AddSubview(_settingsButton);
        View.AddSubview(_status);
        View.AddSubview(_textView);
    }

    private async void OnLoginClick(object? sender, EventArgs e)
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
                Proxy = NSUserDefaults.StandardUserDefaults.StringForKey("proxy") ?? "",
                TimeoutSeconds = 60,
                FingerprintCookies = NSUserDefaults.StandardUserDefaults.StringForKey("cookie") ?? "",
                UserAgent = "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 Mobile/15E148"
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
        catch (Exception ex)
        {
            _status.Text = $"登录失败: {ex.Message}";
        }
        finally
        {
            _loginButton.Enabled = true;
        }
    }

    private async System.Threading.Tasks.Task EnsurePortalLoginAsync()
    {
        if (_portalLoggedIn) return;

        if (string.IsNullOrEmpty(_sessionUsername) || string.IsNullOrEmpty(_sessionPassword))
        {
            var saved = _encryptedStore.LoadPassword();
            if (saved == null)
            {
                throw new Exception("未找到可用凭据，请先登录");
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
                Proxy = NSUserDefaults.StandardUserDefaults.StringForKey("proxy") ?? "",
                TimeoutSeconds = 60,
                FingerprintCookies = NSUserDefaults.StandardUserDefaults.StringForKey("cookie") ?? "",
                UserAgent = "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 Mobile/15E148"
            };
            _portal = new HttpStudentPortalClient(options, NullLogger<HttpStudentPortalClient>.Instance);
        }

        var ok = await _portal.LoginAsync(_sessionUsername, _sessionPassword);
        if (!ok)
        {
            throw new Exception("教务系统登录失败，请重新验证账号密码");
        }
        _portalLoggedIn = true;
    }

    private async void OnFetchClick(object? sender, EventArgs e)
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
            _textView!.Text = _lastGradeText;

            var uname = _username?.Text?.Trim() ?? "";
            if (student != null)
            {
                _store.SaveStudentInfo(uname, student);
                _store.SaveGrades(uname, grades);
            }
        }
        catch (Exception ex)
        {
            _status.Text = $"获取数据失败: {ex.Message}";
        }
        finally
        {
            _fetchButton.Enabled = true;
        }
    }

    private async void OnScheduleClick(object? sender, EventArgs e)
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
            _textView!.Text = list.Count == 0 ? "未解析到课程数据" : string.Join("\n", list);
        }
        catch (Exception ex)
        {
            _status.Text = $"获取课表失败: {ex.Message}";
        }
        finally
        {
            _scheduleButton.Enabled = true;
        }
    }

    private void OnExportClick(object? sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_lastGradeText))
        {
            _status!.Text = "没有可导出的成绩";
            return;
        }

        var activity = new UIActivityViewController(new NSObject[] { new NSString(_lastGradeText) }, null);
        if (activity.PopoverPresentationController != null)
        {
            activity.PopoverPresentationController.SourceView = _exportButton;
        }
        PresentViewController(activity, true, null);
    }

    private void OnSettingsClick(object? sender, EventArgs e)
    {
        var alert = UIAlertController.Create("设置", null, UIAlertControllerStyle.Alert);
        alert.AddTextField((field) => {
            field.Placeholder = "代理地址";
            field.Text = NSUserDefaults.StandardUserDefaults.StringForKey("proxy") ?? "";
        });
        alert.AddTextField((field) => {
            field.Placeholder = "浏览器指纹 Cookie";
            field.Text = NSUserDefaults.StandardUserDefaults.StringForKey("cookie") ?? "";
        });
        alert.AddAction(UIAlertAction.Create("保存", UIAlertActionStyle.Default, (action) => {
            NSUserDefaults.StandardUserDefaults.SetString(alert.TextFields?[0]?.Text?.Trim() ?? "", "proxy");
            NSUserDefaults.StandardUserDefaults.SetString(alert.TextFields?[1]?.Text?.Trim() ?? "", "cookie");
        }));
        alert.AddAction(UIAlertAction.Create("取消", UIAlertActionStyle.Cancel, null));
        PresentViewController(alert, true, null);
    }

    private void OnClearClick(object? sender, EventArgs e)
    {
        var uname = _username?.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(uname))
        {
            _status!.Text = "请先输入学号";
            return;
        }

        _store.ClearUserData(uname);
        _store.ClearCredentials();
        _encryptedStore.Clear();
        _sessionUsername = null;
        _sessionPassword = null;
        _loggedIn = false;
        _portalLoggedIn = false;
        _textView!.Text = "";
        _status.Text = "本地缓存和内存凭据已清除";
    }
}
