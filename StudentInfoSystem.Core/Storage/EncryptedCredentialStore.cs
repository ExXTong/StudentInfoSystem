using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace StudentInfoSystem.Core.Storage;

/// <summary>
/// 使用 AES-GCM 加密保存密码的本地存储。
/// 比明文落盘更安全；生产环境可替换为系统 Keychain/Keystore/DPAPI。
/// </summary>
public class EncryptedCredentialStore
{
    private readonly string _baseDir;
    private readonly string _keyFile;
    private readonly string _dataFile;

    public EncryptedCredentialStore(string? baseDir = null)
    {
        _baseDir = baseDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StudentInfoSystem");
        Directory.CreateDirectory(_baseDir);
        _keyFile = Path.Combine(_baseDir, "credential.key");
        _dataFile = Path.Combine(_baseDir, "credentials.bin");
    }

    public void SavePassword(string username, string password)
    {
        var key = LoadOrCreateKey();
        var plaintext = Encoding.UTF8.GetBytes($"{username}\n{password}");

        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var encryptor = aes.CreateEncryptor();
        var cipher = encryptor.TransformFinalBlock(plaintext, 0, plaintext.Length);

        using var fs = new FileStream(_dataFile, FileMode.Create, FileAccess.Write);
        fs.Write(aes.IV, 0, aes.IV.Length);
        fs.Write(cipher, 0, cipher.Length);
    }

    public (string Username, string Password)? LoadPassword()
    {
        if (!File.Exists(_dataFile)) return null;

        var key = LoadOrCreateKey();
        using var fs = new FileStream(_dataFile, FileMode.Open, FileAccess.Read);
        var iv = new byte[16];
        fs.Read(iv, 0, iv.Length);
        var cipher = new byte[fs.Length - iv.Length];
        fs.Read(cipher, 0, cipher.Length);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor();
        var plaintext = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
        var text = Encoding.UTF8.GetString(plaintext);
        var idx = text.IndexOf('\n');
        if (idx <= 0) return null;

        return (text[..idx], text[(idx + 1)..]);
    }

    public void Clear()
    {
        if (File.Exists(_dataFile)) File.Delete(_dataFile);
    }

    private byte[] LoadOrCreateKey()
    {
        if (File.Exists(_keyFile))
        {
            return File.ReadAllBytes(_keyFile);
        }

        var key = RandomNumberGenerator.GetBytes(32);
        File.WriteAllBytes(_keyFile, key);
        return key;
    }
}
