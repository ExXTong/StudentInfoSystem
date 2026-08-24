using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using StudentInfoSystem.Core.Security;
using StudentInfoSystem.Core.Storage;

namespace StudentInfoSystem.Desktop;

/// <summary>
/// 桌面端安全凭据存储：
/// Windows 使用 DPAPI，其他平台回退到 AES 加密本地文件。
/// </summary>
public class SecureCredentialStore : ISecureCredentialStore
{
    private readonly EncryptedCredentialStore _fallback = new();
    private readonly string _dataFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "StudentInfoSystem",
        "credentials.dpapi.bin");

    public void SavePassword(string username, string password)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                var plain = Encoding.UTF8.GetBytes($"{username}\n{password}");
                var encrypted = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
                Directory.CreateDirectory(Path.GetDirectoryName(_dataFile)!);
                File.WriteAllBytes(_dataFile, encrypted);
            }
            else
            {
                _fallback.SavePassword(username, password);
            }
        }
        catch
        {
            _fallback.SavePassword(username, password);
        }
    }

    public (string Username, string Password)? LoadPassword()
    {
        try
        {
            if (OperatingSystem.IsWindows() && File.Exists(_dataFile))
            {
                var encrypted = File.ReadAllBytes(_dataFile);
                var plain = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                var text = Encoding.UTF8.GetString(plain);
                var idx = text.IndexOf('\n');
                if (idx <= 0) return null;
                return (text[..idx], text[(idx + 1)..]);
            }
        }
        catch
        {
            // fallback
        }
        return _fallback.LoadPassword();
    }

    public void Clear()
    {
        try
        {
            if (File.Exists(_dataFile)) File.Delete(_dataFile);
        }
        catch { }
        _fallback.Clear();
    }
}
