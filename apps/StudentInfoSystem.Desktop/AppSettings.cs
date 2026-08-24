using System;
using System.IO;
using System.Text.Json;

namespace StudentInfoSystem.Desktop;

public class AppSettings
{
    public string Proxy { get; set; } = "";
    public string FingerprintCookies { get; set; } = "";
    public string AuthServerBaseUrl { get; set; } = "https://authserver.nwupl.edu.cn";
    public string EamsBaseUrl { get; set; } = "https://tam.nwupl.edu.cn";
    public string ServiceUrl { get; set; } = "https://tam.nwupl.edu.cn/eams/homeExt.action";
    public string UserAgent { get; set; } =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/151.0.0.0 Safari/537.36 Edg/151.0.0.0";

    private static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "StudentInfoSystem",
        "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch
        {
            // ignore
        }
        return new AppSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
