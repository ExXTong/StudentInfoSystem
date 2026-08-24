using Avalonia.Controls;
using Avalonia.Interactivity;

namespace StudentInfoSystem.Desktop;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;

    public SettingsWindow() : this(new AppSettings())
    {
    }

    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        ProxyBox.Text = settings.Proxy;
        CookieBox.Text = settings.FingerprintCookies;
        SaveButton.Click += OnSaveClick;
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        _settings.Proxy = ProxyBox.Text?.Trim() ?? "";
        _settings.FingerprintCookies = CookieBox.Text?.Trim() ?? "";
        _settings.Save();
        Close();
    }
}
