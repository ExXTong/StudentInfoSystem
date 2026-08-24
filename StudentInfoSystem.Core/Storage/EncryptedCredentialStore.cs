using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace StudentInfoSystem.Core.Storage;

/// <summary>
/// 使用 AES-GCM（认证加密）保存密码的本地存储。
/// 文件格式 V2：[magic "SGV2"][12B nonce][16B tag][ciphertext]。
/// 密钥保存在同目录 credential.key（32B 随机）。
/// 注意：密钥与密文同机存放时这只是防误看/防篡改，不是对抗本地管理员的方案；
/// 桌面端 Windows 请优先使用 DPAPI（见各端 SecureCredentialStore）。
/// 兼容读取旧版 AES-CBC 无认证格式，读取成功后不自动迁移，下次 SavePassword 时写入 V2。
/// </summary>
public class EncryptedCredentialStore
{
    private static readonly byte[] Magic = { (byte)'S', (byte)'G', (byte)'V', (byte)'2' };
    private const int NonceSize = 12;
    private const int TagSize = 16;

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
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipher = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        using (var gcm = new AesGcm(key, TagSize))
        {
            gcm.Encrypt(nonce, plaintext, cipher, tag);
        }

        using var fs = new FileStream(_dataFile, FileMode.Create, FileAccess.Write);
        fs.Write(Magic, 0, Magic.Length);
        fs.Write(nonce, 0, nonce.Length);
        fs.Write(tag, 0, tag.Length);
        fs.Write(cipher, 0, cipher.Length);
    }

    public (string Username, string Password)? LoadPassword()
    {
        if (!File.Exists(_dataFile)) return null;

        try
        {
            var bytes = File.ReadAllBytes(_dataFile);

            if (bytes.Length > Magic.Length + NonceSize + TagSize
                && bytes[0] == Magic[0] && bytes[1] == Magic[1]
                && bytes[2] == Magic[2] && bytes[3] == Magic[3])
            {
                return DecryptGcm(bytes);
            }

            // 旧版 AES-CBC 格式
            return DecryptLegacyCbc(bytes);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public void Clear()
    {
        if (File.Exists(_dataFile)) File.Delete(_dataFile);
    }

    private (string Username, string Password)? DecryptGcm(byte[] bytes)
    {
        var key = LoadOrCreateKey();
        var nonce = bytes.AsSpan(Magic.Length, NonceSize).ToArray();
        var tag = bytes.AsSpan(Magic.Length + NonceSize, TagSize).ToArray();
        var cipher = bytes.AsSpan(Magic.Length + NonceSize + TagSize).ToArray();
        var plaintext = new byte[cipher.Length];

        using var gcm = new AesGcm(key, TagSize);
        gcm.Decrypt(nonce, cipher, tag, plaintext);
        return Split(plaintext);
    }

    private (string Username, string Password)? DecryptLegacyCbc(byte[] bytes)
    {
        if (bytes.Length <= 16) return null;

        var key = LoadOrCreateKey();
        var iv = new byte[16];
        Array.Copy(bytes, 0, iv, 0, iv.Length);
        var cipher = new byte[bytes.Length - iv.Length];
        Array.Copy(bytes, iv.Length, cipher, 0, cipher.Length);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor();
        var plaintext = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
        return Split(plaintext);
    }

    private static (string Username, string Password)? Split(byte[] plaintext)
    {
        var text = Encoding.UTF8.GetString(plaintext);
        var idx = text.IndexOf('\n');
        if (idx <= 0) return null;

        return (text[..idx], text[(idx + 1)..]);
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
